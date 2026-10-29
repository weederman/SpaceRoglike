using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 해적 소탕 임무 실행 담당 (공통 부분은 MissionDirector).
///  1) 의뢰인 수락 대사 → 목적지 비콘을 정한다 (임무 레벨 범위의 비콘 중 지금 위치에서 가장 가까운 곳).
///     지도(M)를 열면 목적지가 골라져 있고 "임무 목적지"로 표시된다. 가는 동안은 자유롭게 도킹·워프할 수 있다.
///  2) 그 비콘에 도착하면(한 번만) 잠시 뒤 해적 함대가 나타나 플레이어를 습격한다. 이때부터는 도킹·워프가 막힌다.
///  3) 지휘함이 있으면 정해진 시간까지 살아 있을 때 증원을 부른다 (먼저 격파하면 증원 차단).
///     표적이 있으면 체력이 낮아질 때 도주하고, 충분히 멀어지면 워프로 달아난다 → 실패.
///  4) 목표(전원 격파 / 표적 격파)를 이루면 남은 적은 물러나고 → 성공 대사 → 보수 지급.
/// 프리팹: Assets/Resources/Missions/SweepMissionDirector.prefab (배치 수치)
/// </summary>
public class SweepMissionDirector : MissionDirector
{
    [Header("소탕")]
    [Tooltip("적이 나타나는 거리 (플레이어 기준). 화면 밖")]
    [SerializeField, Min(100f)] private float _enemySpawnDistance = 900f;
    [Tooltip("함대가 퍼져 나타나는 각도")]
    [SerializeField, Range(0f, 180f)] private float _fleetSpread = 70f;

    private SweepMissionData _data;
    private StarSystem.Location _destination = StarSystem.Location.None;
    private bool _arrived;
    private bool _finished;
    private PirateShip _commander;
    private PirateShip _target;
    // 격파된 함선은 곧 사라지므로(null) 격파 여부는 따로 기억한다
    private bool _commanderDown;
    private bool _targetDown;
    private int _spawned;
    private int _killed;

    // 가는 동안은 자유롭고, 교전이 시작되면 끝날 때까지 떠날 수 없다
    protected override bool TravelBlocked => _arrived && !_finished;

    protected override bool Setup(MissionData data)
    {
        _data = data as SweepMissionData;
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
        yield return PlayStory(_data.acceptSequence);

        StarSystem system = StarSystem.Instance;
        _destination = system != null ? PickBeacon(system) : StarSystem.Location.None;
        if (_destination.IsNone)
        {
            Hud.Show(_data.title, _data.objective);
            yield return Fail($"레벨 {_data.beaconLevels.x}~{_data.beaconLevels.y} 워프 비콘을 찾지 못했습니다.");
            yield break;
        }

        // 이동: 목적지 비콘에 도착할 때까지 (지도에 표시)
        StarMapUI.MissionTarget = _destination;
        string destinationName = system.LocationName(_destination);
        Hud.Show(_data.title, _data.objective);
        Hud.SetStatus($"목적지 {destinationName}");
        Hud.HideBar("M 지도에서 목적지로 워프하세요");
        while (system.CurrentArea != _destination || StarSystemWarp.IsWarping || StationDocking.IsDocked)
            yield return null;

        // 도착: 함대 등장 (도착은 한 번만 처리한다 → 함대가 두 번 나타나지 않는다)
        _arrived = true;
        StarMapUI.MissionTarget = StarSystem.Location.None;
        Hud.SetStatus("적대 신호 접근 중");
        Hud.HideBar();
        yield return new WaitForSeconds(_data.arrivalDelay);

        SpawnFleet(_data.fleet);
        if (_target != null)
            CombatHud.ShowWaypoint(_target.transform, "표적");
        else if (_commander != null)
            CombatHud.ShowWaypoint(_commander.transform, "지휘함");

        // 교전
        float elapsed = 0f;
        bool reinforced = false;
        bool fleeing = false;
        while (true)
        {
            // 지휘함이 버티면 증원 요청
            if (!reinforced && HasCommander && elapsed >= _data.reinforceAfterSeconds)
            {
                reinforced = true;
                if (!_commanderDown)
                {
                    SpawnFleet(_data.reinforcements);
                    Hud.ShowNotice("지휘함이 증원을 불렀습니다");
                }
            }

            // 표적 도주
            if (_target != null && IsAlive(_target))
            {
                float hp = _target.Hull.CurrentHp / _target.Hull.MaxHp;
                if (!fleeing && hp <= _data.fleeBelowHpRatio)
                {
                    fleeing = true;
                    _target.SetMode(PirateShip.Mode.Flee);
                    Hud.ShowNotice("표적이 전장을 이탈하려 합니다");
                }

                if (fleeing && HorizontalDistance(_target.transform.position, Player.position) > _data.escapeDistance)
                {
                    _target.SetMode(PirateShip.Mode.Warp);
                    _finished = true;
                    yield return Fail("표적이 워프로 도주했습니다.");
                    yield break;
                }
            }

            if (IsComplete(reinforced))
                break;

            UpdateHud(elapsed, reinforced, fleeing);
            yield return null;
            elapsed += Time.deltaTime;
        }

        _finished = true;
        var lines = new List<string> { $"격파 {_killed}척" };
        yield return Succeed(_data, _data.reward, $"보수 +{_data.reward:N0} CR", lines);
    }

    /// <summary>이 임무에 지휘함이 나왔는지 (격파됐어도 true)</summary>
    private bool HasCommander => _commander != null || _commanderDown;

    private bool IsComplete(bool reinforced)
    {
        if (_data.goal == SweepMissionData.Goal.DestroyTarget)
            return _targetDown;

        // 전원 격파: 아직 올 수 있는 증원이 있으면 기다린다
        bool reinforcementsPending = !reinforced && HasCommander && !_commanderDown && _data.reinforcements.Length > 0;
        return !reinforcementsPending && AliveEnemies() == 0;
    }

    private void UpdateHud(float elapsed, bool reinforced, bool fleeing)
    {
        Hud.SetEnemies(AliveEnemies());

        if (_target != null && IsAlive(_target))
        {
            Hud.SetBar("표적 선체", _target.Hull.CurrentHp / _target.Hull.MaxHp);
            Hud.SetStatus(fleeing ? "표적 도주 중 · 추격하라" : "표적을 격파하라");
        }
        else
        {
            Hud.SetBar("소탕", _spawned > 0 ? (float)_killed / _spawned : 0f, $"{_killed}/{_spawned}");

            if (!HasCommander || _data.reinforcements.Length == 0)
                Hud.SetStatus("해적 소탕");
            else if (reinforced)
                Hud.SetStatus(_commanderDown ? "지휘함 격파" : "증원 도착 · 지휘함 생존");
            else if (_commanderDown)
                Hud.SetStatus("지휘함 격파 · 증원 차단");
            else
                Hud.SetTimer("증원 요청까지", _data.reinforceAfterSeconds - elapsed);
        }
    }

    // ── 목적지 ─────────────────────────────────────────

    /// <summary>임무 레벨 범위의 비콘 중 지금 위치에서 가장 가까운 곳 (지금 있는 비콘은 뺀다)</summary>
    private StarSystem.Location PickBeacon(StarSystem system)
    {
        StarSystemData data = system.Data;
        Vector2 here = system.ToSystem(PlayerWorldPosition());
        StarSystem.Location best = StarSystem.Location.None;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < data.beacons.Length; i++)
        {
            int level = data.beacons[i].level;
            var location = new StarSystem.Location(StarSystem.LocationKind.Beacon, i);
            if (level < _data.beaconLevels.x || level > _data.beaconLevels.y || location == system.CurrentArea)
                continue;

            float d = Vector2.Distance(here, data.BeaconPosition(i));
            if (d < bestDistance)
            {
                bestDistance = d;
                best = location;
            }
        }

        return best;
    }

    // ── 적 ─────────────────────────────────────────────

    /// <summary>플레이어 앞쪽 화면 밖에서 함대가 부채꼴로 나타나 습격해 온다.</summary>
    private void SpawnFleet(SweepMissionData.Group[] groups)
    {
        int total = 0;
        foreach (SweepMissionData.Group g in groups)
            total += g.count;
        if (total == 0)
            return;

        Vector3 forward = Player.forward;
        forward.y = 0f;
        float baseAngle = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg + Random.Range(-40f, 40f);

        int n = 0;
        foreach (SweepMissionData.Group g in groups)
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

                _spawned++;
                if (enemy.Hull != null)
                    enemy.Hull.OnBroken += () => _killed++;

                if (_commander == null && !_commanderDown && g.shipClass == _data.commanderClass && enemy.Hull != null)
                {
                    _commander = enemy;
                    enemy.Hull.OnBroken += () => _commanderDown = true;
                }

                if (_target == null && !_targetDown && g.shipClass == _data.targetClass && enemy.Hull != null)
                {
                    _target = enemy;
                    enemy.Hull.OnBroken += () => _targetDown = true;
                }
            }
        }
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
