using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 채굴 임무 실행 담당 (공통 부분은 MissionDirector).
///  1) 수락하면 납품처(수락한 정거장)와 목적지 비콘(임무 레벨 범위에서 가장 가까운 곳)을 정한다. 지도에 목적지를 표시한다.
///  2) 플레이어는 광석을 캐서(OreDeposits의 광맥) 납품처에 도킹한 뒤 미션 탭에서 '납품'한다 (MissionBoardWindow → Deliver).
///     여러 번 나눠 납품할 수 있고, 납품량 단계에 따라 보너스가 바로 나온다. 시장가 지급 임무면 납품할 때마다 시장가를 받는다.
///  3) 목적지에서 채굴하는 동안에는 일정 간격마다 확률로 해적 순찰대가 나타나 습격한다.
///  4) 목표량을 채우면 임무 보너스를 받고 끝난다.
/// 이동·도킹을 막지 않는다 (왕복해야 하므로). 프리팹: Assets/Resources/Missions/MiningMissionDirector.prefab
/// </summary>
public class MiningMissionDirector : MissionDirector
{
    [Header("순찰대")]
    [Tooltip("순찰대가 나타나는 거리 (플레이어 기준). 화면 밖")]
    [SerializeField, Min(100f)] private float _patrolSpawnDistance = 900f;
    [SerializeField, Range(0f, 180f)] private float _patrolSpread = 50f;

    private MiningMissionData _data;
    private StationZone _station;
    private StarSystem.Location _destination = StarSystem.Location.None;
    private int _delivered;
    private int _marketPaid;
    private int _stageBonusPaid;
    private int _nextStage;
    private float _elapsed;
    private float _nextPatrolCheck;
    private bool _finished;

    /// <summary>지금까지 납품한 양</summary>
    public int Delivered => _delivered;
    public MiningMissionData Data => _data;
    /// <summary>납품하는 정거장 (임무를 받은 곳)</summary>
    public StationZone Station => _station;

    protected override bool TravelBlocked => false;

    protected override bool Setup(MissionData data)
    {
        _data = data as MiningMissionData;
        return _data != null;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (StarMapUI.MissionTarget == _destination)
            StarMapUI.MissionTarget = StarSystem.Location.None;
    }

    // ── 진행 ───────────────────────────────────────────

    protected override IEnumerator Run()
    {
        _station = StationZone.Current != null ? StationZone.Current : StationZone.Home;
        yield return PlayStory(_data.acceptSequence);

        StarSystem system = StarSystem.Instance;
        _destination = system != null ? PickBeacon(system) : StarSystem.Location.None;
        if (!_destination.IsNone)
            StarMapUI.MissionTarget = _destination;

        Hud.Show(_data.title, _data.objective);
        _nextPatrolCheck = _data.patrolCheckSeconds;

        while (_delivered < _data.amount)
        {
            _elapsed += Time.deltaTime;
            UpdatePatrol(system);
            UpdateHud(system);
            yield return null;
        }

        _finished = true;
        if (StarMapUI.MissionTarget == _destination)
            StarMapUI.MissionTarget = StarSystem.Location.None;

        var lines = new List<string>();
        if (_marketPaid > 0)
            lines.Add($"광석 대금 {_marketPaid:N0} CR (납품할 때 지급)");
        if (_stageBonusPaid > 0)
            lines.Add($"납품량 보너스 {_stageBonusPaid:N0} CR (납품할 때 지급)");

        yield return Succeed(_data, _data.reward, $"임무 보너스 +{_data.reward:N0} CR", lines);
    }

    private void UpdateHud(StarSystem system)
    {
        int carried = Mathf.FloorToInt(CarriedForMission());
        Hud.SetBar("납품", (float)_delivered / _data.amount, $"{_delivered:N0} / {_data.amount:N0}");

        string stationName = _station != null ? _station.DisplayName : "정거장";
        if (carried > 0 && StationDocking.IsDocked && StationZone.Current == _station)
            Hud.SetStatus("미션 탭에서 납품하세요");
        else if (carried > 0)
            Hud.SetStatus($"납품처 {stationName} · 실은 {_data.OreLabel} {carried}");
        else if (!_destination.IsNone && system != null)
            Hud.SetStatus($"채굴지 {system.LocationName(_destination)}");
        else
            Hud.SetStatus($"{_data.OreLabel} 채굴");

        int alive = AliveEnemies();
        Hud.SetEnemies(alive > 0 ? alive : -1);
    }

    // ── 납품 ───────────────────────────────────────────

    /// <summary>화물칸에 실린 이 임무에 인정되는 광석의 양</summary>
    public float CarriedForMission()
    {
        CargoHold cargo = CargoHold.Instance;
        if (cargo == null)
            return 0f;

        return string.IsNullOrEmpty(_data.oreId) ? cargo.Ore : cargo.Amount(_data.oreId);
    }

    /// <summary>지금 이 정거장에서 납품할 수 있는 양 (못 하면 0과 이유)</summary>
    public int Deliverable(StationZone zone, out string reason)
    {
        reason = null;
        if (_finished)
            reason = "임무를 마쳤습니다.";
        else if (zone == null || zone != _station)
            reason = $"납품처는 {(_station != null ? _station.DisplayName : "임무를 받은 정거장")}입니다.";
        else if (CarriedForMission() < 1f)
            reason = $"화물칸에 {_data.OreLabel}이 없습니다.";

        return reason == null ? Mathf.Min(Mathf.FloorToInt(CarriedForMission()), _data.amount - _delivered) : 0;
    }

    /// <summary>화물칸의 광석을 납품한다. 납품한 양을 돌려준다.</summary>
    public int Deliver(StationZone zone)
    {
        int want = Deliverable(zone, out _);
        if (want <= 0)
            return 0;

        CargoHold cargo = CargoHold.Instance;
        int delivered = 0;
        int market = 0;

        // 종류 상관없는 임무는 비싼 광석부터가 아니라 실린 순서대로 낸다 (시장가는 종류별로 계산)
        IEnumerable<string> ores = string.IsNullOrEmpty(_data.oreId)
            ? cargo.Contents.Select(p => p.Key).ToList()
            : new List<string> { _data.oreId };

        foreach (string ore in ores)
        {
            if (delivered >= want)
                break;

            int taken = Mathf.FloorToInt(cargo.Take(ore, want - delivered));
            delivered += taken;
            market += taken * OreCatalog.PriceOf(ore);
        }

        _delivered += delivered;

        int credits = 0;
        if (_data.payMarketValue)
        {
            credits += market;
            _marketPaid += market;
        }

        // 납품량 단계 보너스
        while (_nextStage < _data.stages.Length && _delivered >= _data.stages[_nextStage].delivered)
        {
            int bonus = _data.stages[_nextStage].bonus;
            credits += bonus;
            _stageBonusPaid += bonus;
            Hud.ShowNotice($"납품 {_data.stages[_nextStage].delivered:N0} 달성 · 보너스 +{bonus:N0} CR");
            _nextStage++;
        }

        if (credits > 0 && CreditWallet.Instance != null)
            CreditWallet.Instance.Add(credits);

        return delivered;
    }

    // ── 해적 순찰대 ────────────────────────────────────

    private void UpdatePatrol(StarSystem system)
    {
        if (_data.patrol.Length == 0 || Player == null || _elapsed < _nextPatrolCheck)
            return;

        _nextPatrolCheck = _elapsed + _data.patrolCheckSeconds;

        // 목적지에서 채굴 중일 때만, 앞선 순찰대가 남아 있지 않을 때만
        bool atDestination = system != null && system.CurrentArea == _destination && !StationDocking.IsDocked && !StarSystemWarp.IsWarping;
        bool mining = MiningLaser.Instance != null && MiningLaser.Instance.IsMining;
        if (!atDestination || !mining || AliveEnemies() > 0)
            return;

        float chance = Mathf.Clamp01(_data.patrolChance + _data.patrolChancePerMinute * (_elapsed / 60f));
        if (Random.value >= chance)
            return;

        SpawnPatrol();
        Hud.ShowNotice("해적 순찰대 접근");
    }

    private void SpawnPatrol()
    {
        int total = _data.patrol.Sum(g => g.count);
        float baseAngle = Random.Range(0f, 360f);
        int n = 0;
        foreach (MiningMissionData.Group g in _data.patrol)
        {
            for (int i = 0; i < g.count; i++, n++)
            {
                float t = total > 1 ? (float)n / (total - 1) - 0.5f : 0f;
                float angle = (baseAngle + t * _patrolSpread) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                Vector3 position = Player.position + dir * _patrolSpawnDistance;
                position.y = Player.position.y;
                SpawnEnemy(g.shipClass, position, -dir, Player);
            }
        }
    }

    // ── 목적지 ─────────────────────────────────────────

    private StarSystem.Location PickBeacon(StarSystem system)
    {
        StarSystemData data = system.Data;
        Vector2 here = system.ToSystem(PlayerWorldPosition());
        StarSystem.Location best = StarSystem.Location.None;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < data.beacons.Length; i++)
        {
            int level = data.beacons[i].level;
            if (level < _data.beaconLevels.x || level > _data.beaconLevels.y)
                continue;

            float d = Vector2.Distance(here, data.BeaconPosition(i));
            if (d < bestDistance)
            {
                bestDistance = d;
                best = new StarSystem.Location(StarSystem.LocationKind.Beacon, i);
            }
        }

        return best;
    }
}
