using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 튜토리얼 장면 5("희귀 광맥 발견 — 채굴") 연출 담당. "tutorial_mining"(조작하며 듣는 시퀀스)의 cue를 받아
/// 광맥 소행성을 놓고, 채굴 진행을 스토리 신호로 알리고, 해적 증원을 등장/후퇴시킨다.
///
///  s5_ore_show          라스가 보낸 좌표(소행성 지대 안쪽)에 광맥 소행성을 놓고 목적지 표시를 띄운다
///  s5_vein_reveal       광맥 소행성 표면의 보랏빛 광맥이 드러난다
///  s5_pirates_arrive    해적선들이 레이더 신호 방향에서 나타나 멀찍이 멈춘다 (쏘지 않는다)
///  s5_pirates_retreat   해적선들이 차례로 워프해 물러난다 (기본 튜토리얼형: 채굴을 끝내면 후퇴)
///  s6_return            (장면 6 귀환) 남은 광맥 소행성과 해적선을 정리한다
///
/// 보내는 신호 (JSON의 waitFor에 쓴다):
///  ore_reached   플레이어가 광맥 소행성 가까이 도착했다
///  ore_65        화물칸이 의뢰 목표치(적재 한도)의 65% 이상 찼다
///  ore_full      화물칸이 가득 찼다
/// (채굴 빔이 고정되면 MiningLaser가 "mining_locked", 광맥을 지정하면 TargetingSystem이 "ore_locked"를 보낸다)
///
/// cue는 모두 즉시 끝나는 상태 전환이라 큐 없이 바로 처리한다. 프리팹: Assets/Resources/Directors/Cue/MiningTutorialDirector.prefab
/// </summary>
public class MiningTutorialDirector : MonoBehaviour
{
    private const string OreShowCue = "s5_ore_show";
    private const string RevealCue = "s5_vein_reveal";
    private const string PiratesArriveCue = "s5_pirates_arrive";
    private const string PiratesRetreatCue = "s5_pirates_retreat";
    private const string ReturnCue = "s6_return";

    private const string ReachedSignal = "ore_reached";
    private const string Ore65Signal = "ore_65";
    private const string OreFullSignal = "ore_full";

    [Header("광맥 소행성")]
    [SerializeField] private Sprite _oreSprite;
    [Tooltip("긴 변 기준 월드 크기")]
    [SerializeField, Min(1f)] private float _oreSize = 120f;
    [Tooltip("매장량. 화물칸 적재 한도보다 넉넉하게 두어 가득 채울 수 있게 한다.")]
    [SerializeField, Min(1f)] private float _oreAmount = 140f;
    [Tooltip("광맥 소행성이 놓이는 방향 (위쪽=북쪽=0도, 시계 방향)과 플레이어로부터의 거리")]
    [SerializeField] private float _oreAngle = 35f;
    [SerializeField, Min(1f)] private float _oreDistance = 650f;
    [Tooltip("이 거리(소행성 가장자리까지) 안으로 들어오면 도착으로 본다")]
    [SerializeField, Min(1f)] private float _reachDistance = 220f;
    [SerializeField] private string _waypointLabel = "광물 좌표";
    [Tooltip("이 신호가 나온 비율 (0~1)")]
    [SerializeField, Range(0f, 1f)] private float _progressSignalRatio = 0.65f;

    [Header("해적 증원")]
    [SerializeField] private GameObject _piratePrefab;
    [SerializeField] private GameObject _warpOutPrefab;
    [Tooltip("해적선이 나타나는 방향 (RadarPing의 신호 각도와 맞춘다)")]
    [SerializeField] private float[] _pirateAngles = { 20f, 75f, -40f };
    [SerializeField, Min(1f)] private float _pirateSpawnDistance = 900f;
    [Tooltip("멀찍이 멈추는 거리 (플레이어 기준)")]
    [SerializeField, Min(1f)] private float _pirateHoldDistance = 240f;
    [SerializeField, Min(0f)] private float _retreatInterval = 0.6f;
    [SerializeField] private PirateShip.Tuning _pirateTuning = new PirateShip.Tuning();

    private readonly List<PirateShip> _pirates = new List<PirateShip>();
    private MineableAsteroid _ore;
    private SpaceshipController _player;
    private bool _reached;
    private bool _signaled65;
    private bool _signaledFull;

    private void OnEnable()
    {
        StoryPlayer.OnCue += HandleCue;
    }

    private void OnDisable()
    {
        StoryPlayer.OnCue -= HandleCue;
        CombatHud.HideWaypoint();
    }

    private void HandleCue(string cue)
    {
        switch (cue)
        {
            case OreShowCue:
                ShowOre();
                break;
            case RevealCue:
                if (_ore == null)
                    ShowOre();
                if (_ore != null)
                    _ore.Reveal(StoryPlayer.IsSkipping);
                break;
            case PiratesArriveCue:
                SpawnPirates(StoryPlayer.IsSkipping);
                break;
            case PiratesRetreatCue:
                StartCoroutine(Retreat());
                break;
            case ReturnCue:
                Cleanup();
                break;
        }
    }

    private void Update()
    {
        if (_ore != null && !_reached && EnsurePlayer())
        {
            Vector3 d = _ore.transform.position - _player.transform.position;
            d.y = 0f;
            if (d.magnitude - _ore.Radius <= _reachDistance)
            {
                _reached = true;
                CombatHud.HideWaypoint();
                StoryPlayer.Signal(ReachedSignal);
            }
        }

        CargoHold cargo = CargoHold.Instance;
        if (_ore == null || cargo == null)
            return;

        if (!_signaled65 && cargo.Ratio >= _progressSignalRatio)
        {
            _signaled65 = true;
            StoryPlayer.Signal(Ore65Signal);
        }

        if (!_signaledFull && cargo.IsFull)
        {
            _signaledFull = true;
            StoryPlayer.Signal(OreFullSignal);
        }
    }

    // ── 광맥 ──────────────────────────────────────────

    private void ShowOre()
    {
        if (_ore != null || !EnsurePlayer())
            return;

        float rad = _oreAngle * Mathf.Deg2Rad;
        Vector3 pos = _player.transform.position + new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * _oreDistance;

        var go = new GameObject("OreAsteroid");
        go.transform.position = pos;
        _ore = go.AddComponent<MineableAsteroid>();
        _ore.Init(_oreSprite, _oreSize, _oreAmount);

        CombatHud.ShowWaypoint(_ore.transform, _waypointLabel);
    }

    // ── 해적 증원 ─────────────────────────────────────

    private void SpawnPirates(bool snap)
    {
        if (_pirates.Count > 0 || !EnsurePlayer())
            return;

        Vector3 center = _player.transform.position;
        for (int i = 0; i < _pirateAngles.Length; i++)
        {
            float rad = _pirateAngles[i] * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));

            GameObject go = Instantiate(_piratePrefab, center + dir * _pirateSpawnDistance, Quaternion.LookRotation(-dir));
            go.name = $"PirateReinforcement_{i + 1}";

            var pirate = go.AddComponent<PirateShip>();
            pirate.Init(_pirateTuning, _player.transform, _warpOutPrefab);
            pirate.Hold(center + dir * _pirateHoldDistance, snap);
            pirate.OnWarpedOut += p => _pirates.Remove(p);
            _pirates.Add(pirate);
        }
    }

    private IEnumerator Retreat()
    {
        foreach (PirateShip pirate in _pirates.ToArray())
        {
            if (pirate == null)
                continue;

            if (StoryPlayer.IsSkipping)
            {
                Destroy(pirate.gameObject);
                continue;
            }

            pirate.SetMode(PirateShip.Mode.Warp);
            yield return new WaitForSeconds(_retreatInterval);
        }
    }

    // ── 정리 ──────────────────────────────────────────

    /// <summary>정거장으로 귀환할 때: 광맥 소행성과 남은 해적선을 치운다.</summary>
    private void Cleanup()
    {
        StopAllCoroutines();
        CombatHud.HideWaypoint();

        if (_ore != null)
            Destroy(_ore.gameObject);
        _ore = null;

        foreach (PirateShip pirate in _pirates)
        {
            if (pirate != null)
                Destroy(pirate.gameObject);
        }

        _pirates.Clear();
    }

    private bool EnsurePlayer()
    {
        if (_player == null)
            _player = FindObjectOfType<SpaceshipController>();

        return _player != null;
    }
}
