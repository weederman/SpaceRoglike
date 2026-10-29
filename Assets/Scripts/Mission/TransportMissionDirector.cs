using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FORGE3D;
using UnityEngine;

/// <summary>
/// 운송 임무 실행 담당 (공통 부분은 MissionDirector).
///  1) 수락하면 임무를 받은 정거장(출발지)에서 화물을 화물칸에 싣고, 도착지 정거장을 정한다(지도에 표시).
///  2) 도착지 지역에 들어오면 적 함대가 한 번만 나타난다(도착 여부를 기억해 다시 생기지 않는다). 남은 적이 있는 동안은 도킹·워프를 막는다.
///  3) 적을 모두 격파하면 도착지에 도킹해 미션 탭에서 납품한다 (MissionBoardWindow → Deliver). 납품하면 성공.
///  - 실패 조건(failBelowHullRatio)이 있으면 화물을 싣고 있는 동안 선체가 그 비율 아래로 내려갈 때 실패.
///  - 끝나면(성공·실패·중단) 화물칸의 임무 화물을 내린다. 진행 상태는 다른 임무처럼 저장하지 않는다.
/// 프리팹: Assets/Resources/Missions/TransportMissionDirector.prefab
/// </summary>
public class TransportMissionDirector : MissionDirector
{
    [Header("도착 함대")]
    [Tooltip("적이 나타나는 거리 (플레이어 기준, 정거장 반대쪽)")]
    [SerializeField, Min(100f)] private float _enemySpawnDistance = 900f;
    [SerializeField, Range(0f, 180f)] private float _fleetSpread = 70f;

    private TransportMissionData _data;
    private StationZone _origin;
    private StationZone _destinationZone;
    private StarSystem.Location _destination = StarSystem.Location.None;
    private bool _arrived;
    private bool _cleared;
    private bool _delivered;
    private bool _finished;

    public TransportMissionData Data => _data;
    /// <summary>납품하는 정거장 (도착지)</summary>
    public StationZone Destination => _destinationZone;
    /// <summary>도착지 지역에 들어와 함대가 나타났는지(나타날 차례인지)</summary>
    public bool Arrived => _arrived;
    /// <summary>도착지 함대를 모두 격파했는지</summary>
    public bool Cleared => _cleared;

    // 도착한 뒤 함대를 격파하기 전까지는 도킹·워프를 막는다 (납품은 격파 뒤에만)
    protected override bool TravelBlocked => _arrived && !_cleared && !_finished;

    protected override bool Setup(MissionData data)
    {
        _data = data as TransportMissionData;
        return _data != null;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (StarMapUI.MissionTarget == _destination)
            StarMapUI.MissionTarget = StarSystem.Location.None;

        // 실패·중단이면 실은 화물을 내린다 (납품했으면 이미 내렸다)
        if (CargoHold.Instance != null)
            CargoHold.Instance.UnloadMissionCargo();
    }

    // ── 진행 ───────────────────────────────────────────

    protected override IEnumerator Run()
    {
        _origin = StationZone.Current != null ? StationZone.Current : StationZone.Home;
        yield return PlayStory(_data.acceptSequence);

        Hud.Show(_data.title, _data.objective);

        StarSystem system = StarSystem.Instance;
        _destinationZone = PickDestination();
        if (system == null || _destinationZone == null)
        {
            _finished = true;
            yield return Fail("도착지로 쓸 정거장을 찾지 못했습니다.");
            yield break;
        }

        if (CargoHold.Instance == null || !CargoHold.Instance.LoadMissionCargo(_data.cargoName, _data.cargoAmount))
        {
            _finished = true;
            yield return Fail("화물칸에 화물을 실을 자리가 없습니다.");
            yield break;
        }

        _destination = new StarSystem.Location(StarSystem.LocationKind.Station, _destinationZone.StationIndex);
        StarMapUI.MissionTarget = _destination;
        Hud.ShowNotice($"{_data.cargoName} {_data.cargoAmount}개를 실었습니다");

        // 이동: 도착지 정거장 지역에 들어올 때까지
        Hud.HideBar("M 지도에서 도착지로 워프하세요");
        // (붙어 있는 정거장은 지역 판정이 다른 쪽에 머무를 수 있어 거리로도 본다)
        while ((system.CurrentArea != _destination && !system.IsNear(_destination, Player.position))
               || StarSystemWarp.IsWarping || StationDocking.IsDocked)
        {
            if (CheckCargoDamaged())
                yield break;

            Hud.SetStatus($"도착지 {_destinationZone.DisplayName} · {_data.cargoName} {_data.cargoAmount}개");
            yield return null;
        }

        // 도착: 함대 등장 (한 번만)
        _arrived = true;
        StarMapUI.MissionTarget = StarSystem.Location.None;
        Hud.SetStatus("적대 신호 접근 중");
        Hud.HideBar();
        float wait = _data.arrivalDelay;
        while (wait > 0f)
        {
            if (CheckCargoDamaged())
                yield break;
            wait -= Time.deltaTime;
            yield return null;
        }

        int spawned = SpawnFleet();
        Hud.ShowNotice("화물을 노리는 적이 나타났습니다");

        // 교전: 모두 격파할 때까지
        while (AliveEnemies() > 0)
        {
            if (CheckCargoDamaged())
                yield break;

            int alive = AliveEnemies();
            Hud.SetEnemies(alive);
            Hud.SetBar("격파", spawned > 0 ? (float)(spawned - alive) / spawned : 1f, $"{spawned - alive}/{spawned}");
            Hud.SetStatus("적을 모두 격파해야 납품할 수 있습니다");
            yield return null;
        }

        // 납품: 도착지에 도킹해 미션 탭에서
        _cleared = true;
        Hud.SetEnemies(-1);
        Hud.HideBar();
        Hud.ShowNotice("주변 적 격파 · 납품할 수 있습니다");
        while (!_delivered)
        {
            if (CheckCargoDamaged())
                yield break;

            Hud.SetStatus(StationDocking.IsDocked && StationZone.Current == _destinationZone
                ? "미션 탭에서 납품하세요"
                : $"{_destinationZone.DisplayName}에 도킹해 납품하세요");
            yield return null;
        }

        _finished = true;
        yield return Succeed(_data, _data.reward, $"보수 +{_data.reward:N0} CR",
            new[] { $"{_data.cargoName} {_data.cargoAmount}개 납품" });
    }

    /// <summary>화물 파손 실패 조건을 보고, 실패면 실패 처리를 시작하고 true</summary>
    private bool CheckCargoDamaged()
    {
        if (_finished || _data.failBelowHullRatio <= 0f || Player == null)
            return false;

        HullHealth hull = Player.GetComponentInChildren<HullHealth>();
        if (hull == null || hull.MaxHp <= 0f || hull.CurrentHp / hull.MaxHp >= _data.failBelowHullRatio)
            return false;

        _finished = true;
        if (CargoHold.Instance != null)
            CargoHold.Instance.UnloadMissionCargo();
        StartCoroutine(Fail($"선체가 {_data.failBelowHullRatio * 100f:0}% 아래로 손상되어 화물({_data.cargoName})이 파손되었습니다."));
        return true;
    }

    // ── 납품 ───────────────────────────────────────────

    /// <summary>지금 이 정거장에서 납품할 수 있는지 (못 하면 이유)</summary>
    public bool CanDeliver(StationZone zone, out string reason)
    {
        reason = null;
        if (_finished || _delivered)
            reason = "임무를 마쳤습니다.";
        else if (_destinationZone == null || zone != _destinationZone)
            reason = $"납품처는 {(_destinationZone != null ? _destinationZone.DisplayName : "도착지 정거장")}입니다.";
        else if (!_cleared)
            reason = "도착지 주변의 적을 모두 격파해야 납품할 수 있습니다.";
        else if (CargoHold.Instance == null || CargoHold.Instance.MissionCargo < _data.cargoAmount)
            reason = "화물칸에 운송 화물이 없습니다.";
        return reason == null;
    }

    /// <summary>화물을 납품한다. 성공하면 임무가 끝난다.</summary>
    public bool Deliver(StationZone zone)
    {
        if (!CanDeliver(zone, out _))
            return false;

        CargoHold.Instance.UnloadMissionCargo();
        _delivered = true;
        return true;
    }

    // ── 도착지 / 함대 ──────────────────────────────────

    /// <summary>출발지에서 가까운 순으로 늘어놓은 다른 정거장 중 임무의 구간에서 무작위</summary>
    private StationZone PickDestination()
    {
        Vector3 from = _origin != null ? _origin.transform.position : PlayerWorldPosition();
        List<StationZone> stations = StationZone.All
            .Where(z => z != null && z != _origin && z.StationIndex >= 0)
            .OrderBy(z => Vector3.Distance(from, z.transform.position))
            .ToList();
        if (stations.Count == 0)
            return null;

        int last = stations.Count - 1;
        int min = Mathf.Clamp(Mathf.FloorToInt(_data.destinationRank.x * last), 0, last);
        int max = Mathf.Clamp(Mathf.CeilToInt(_data.destinationRank.y * last), min, last);
        return stations[Random.Range(min, max + 1)];
    }

    /// <summary>정거장 반대쪽(열린 공간)에서 함대가 다가온다. 만든 수를 돌려준다.</summary>
    private int SpawnFleet()
    {
        int total = _data.fleet.Sum(g => g.count);
        if (total == 0 || Player == null)
            return 0;

        Vector3 away = Player.position - _destinationZone.transform.position;
        away.y = 0f;
        if (away.sqrMagnitude < 1f)
            away = Player.forward;
        float baseAngle = Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg + Random.Range(-40f, 40f);

        int n = 0;
        int spawned = 0;
        foreach (TransportMissionData.Group g in _data.fleet)
        {
            for (int i = 0; i < g.count; i++, n++)
            {
                float t = total > 1 ? (float)n / (total - 1) - 0.5f : 0f;
                float angle = (baseAngle + t * _fleetSpread) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                Vector3 position = Player.position + dir * (_enemySpawnDistance + Random.Range(-60f, 60f));
                position.y = Player.position.y;

                PirateShip enemy = SpawnEnemy(g.shipClass, position, -dir, Player);
                if (enemy == null)
                    continue;

                spawned++;
                if (!Mathf.Approximately(g.damageMultiplier, 1f))
                {
                    foreach (F3DFXController fx in enemy.GetComponentsInChildren<F3DFXController>(true))
                    {
                        if (fx.damageOverride >= 0f)
                            fx.damageOverride *= g.damageMultiplier;
                    }
                }
            }
        }

        return spawned;
    }
}
