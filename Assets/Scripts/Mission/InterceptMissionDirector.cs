using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 수송선 요격 임무 실행 담당 (메인 스토리 ① 5. 「흔적을 쫓아서」, 공통 부분은 MissionDirector).
///  1) 수락하면 목적지 비콘을 정하고 지도에 표시한다.
///  2) 도착하면 플레이어 앞쪽에 군수품 수송선과 호위 함대(지휘함 포함)가 나타난다. 수송선은 달아나고 호위는 플레이어를 노린다.
///  3) 지휘함이 처음 맞거나 일정 시간이 지나면 적 지휘관 통신이 들린다. 지휘함을 격파하면 통신 기록 (선택 목표).
///  4) 수송선을 격파하면 고급 동력 코어를 얻고 성공 → FirstShipQuest가 최종 조립 단계로. 시간 안에 못 잡으면 수송선이 워프로 달아나 실패.
/// 교전이 시작되면 끝날 때까지 도킹·지도 워프가 막힌다. 프리팹: Assets/Resources/Missions/InterceptMissionDirector.prefab
/// </summary>
public class InterceptMissionDirector : MissionDirector
{
    [Header("함대 배치")]
    [Tooltip("도착한 플레이어 앞쪽으로 수송선까지의 거리")]
    [SerializeField, Min(100f)] private float _transportDistance = 800f;
    [Tooltip("호위가 수송선 둘레에 퍼지는 거리")]
    [SerializeField, Min(10f)] private float _escortSpread = 180f;

    private InterceptMissionData _data;
    private StarSystem.Location _destination = StarSystem.Location.None;
    private PirateShip _transport;
    private PirateShip _commander;
    private readonly List<PirateShip> _escorts = new List<PirateShip>();
    private bool _engaged;
    private bool _finished;
    private bool _transportDown;
    private bool _commanderLinePlayed;

    protected override bool TravelBlocked => _engaged && !_finished;

    protected override bool Setup(MissionData data)
    {
        _data = data as InterceptMissionData;
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

        FirstShipQuestData quest = FirstShipQuest.Data;
        Hud.Show(_data.title, _data.objective);

        StarSystem system = StarSystem.Instance;
        _destination = system != null ? NearestBeacon(system, _data.beaconLevels) : StarSystem.Location.None;
        if (_destination.IsNone)
        {
            _finished = true;
            yield return Fail($"레벨 {_data.beaconLevels.x}~{_data.beaconLevels.y} 워프 비콘을 찾지 못했습니다.");
            yield break;
        }

        // 이동
        StarMapUI.MissionTarget = _destination;
        Hud.SetStatus($"목적지 {system.LocationName(_destination)}");
        Hud.HideBar("M 지도에서 목적지로 워프하세요");
        while (!IsAt(system, _destination))
            yield return null;

        // 도착: 수송선과 호위 함대
        _engaged = true;
        StarMapUI.MissionTarget = StarSystem.Location.None;
        Hud.SetStatus("수송선 신호 포착");
        Hud.HideBar();
        yield return new WaitForSeconds(_data.arrivalDelay);

        SpawnConvoy();
        if (_transport == null)
        {
            _finished = true;
            yield return Fail("수송선을 만들지 못했습니다. (EnemyShips.asset의 수송선 함종)");
            yield break;
        }

        CombatHud.ShowWaypoint(_transport.transform, "수송선");
        Hud.ShowNotice("군수품 수송선 발견 · 달아나기 전에 격파하라");

        // 교전
        float left = _data.escapeSeconds;
        float elapsed = 0f;
        while (!_transportDown)
        {
            if (left <= 0f && IsAlive(_transport))
            {
                _finished = true;
                _transport.SetMode(PirateShip.Mode.Warp);
                yield return Fail("수송선이 워프로 달아났습니다. 라스에게서 다시 출발할 수 있습니다.");
                yield break;
            }

            UpdateCommanderLine(elapsed);
            UpdateHud(left, quest);
            left -= Time.deltaTime;
            elapsed += Time.deltaTime;
            yield return null;
        }

        _finished = true;
        CombatHud.HideWaypoint();
        FirstShipQuest.CompleteCoreStage();

        var lines = new List<string> { $"{quest.coreName} 1개 확보" };
        if (FirstShipQuest.HasCommsLog)
            lines.Add($"{quest.commsLogName} 회수");
        string success = FirstShipQuest.HasCommsLog || string.IsNullOrEmpty(_data.successSequenceNoLog)
            ? _data.successSequence
            : _data.successSequenceNoLog;
        yield return Succeed(_data, 0, $"{quest.coreName} 확보", lines, success);
    }

    private void UpdateHud(float left, FirstShipQuestData quest)
    {
        Hud.SetEnemies(AliveEnemies());
        if (IsAlive(_transport))
            Hud.SetBar("수송선 선체", _transport.Hull.CurrentHp / _transport.Hull.MaxHp);

        if (left <= 30f)
            Hud.SetTimer("수송선 워프까지", left);
        else if (_commander != null && IsAlive(_commander) && !FirstShipQuest.HasCommsLog)
            Hud.SetStatus("수송선 격파 · (선택) 지휘함에서 통신 기록 회수");
        else
            Hud.SetTimer("수송선 워프까지", left);
    }

    // 지휘관 통신: 지휘함이 처음 맞거나 일정 시간이 지나면 한 번
    private void UpdateCommanderLine(float elapsed)
    {
        if (_commanderLinePlayed || string.IsNullOrEmpty(_data.commanderSequence) || StoryPlayer.IsPlaying)
            return;

        bool hit = _commander != null && _commander.Hull != null && _commander.Hull.CurrentHp < _commander.Hull.MaxHp;
        if (!hit && elapsed < _data.commanderLineDelay)
            return;

        _commanderLinePlayed = true;
        StoryPlayer.Play(_data.commanderSequence);
    }

    // ── 함대 ───────────────────────────────────────────

    private void SpawnConvoy()
    {
        Vector3 forward = Player.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
        Vector3 center = Player.position + forward * _transportDistance;
        center.y = Player.position.y;

        _transport = SpawnEnemy(_data.transportClass, center, forward, Player);
        if (_transport != null)
        {
            _transport.SetMode(PirateShip.Mode.Flee);
            _transport.Hull.OnBroken += () => _transportDown = true;
        }

        if (!string.IsNullOrEmpty(_data.commanderClass))
        {
            _commander = SpawnEnemy(_data.commanderClass, center - forward * _escortSpread, -forward, Player);
            if (_commander != null)
                _commander.Hull.OnBroken += () =>
                {
                    FirstShipQuest.SetCommsLog();
                    Hud.ShowNotice($"{FirstShipQuest.Data.commsLogName} 회수");
                };
        }

        int total = _data.escorts.Sum(g => g.count);
        int n = 0;
        foreach (InterceptMissionData.Group g in _data.escorts)
        {
            for (int i = 0; i < g.count; i++, n++)
            {
                float angle = (float)n / Mathf.Max(1, total) * Mathf.PI * 2f;
                Vector3 offset = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * _escortSpread;
                PirateShip escort = SpawnEnemy(g.shipClass, center + offset, -forward, Player);
                if (escort != null)
                    _escorts.Add(escort);
            }
        }
    }
}
