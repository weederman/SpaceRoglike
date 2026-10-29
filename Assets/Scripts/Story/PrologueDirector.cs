using System.Collections;
using System.Collections.Generic;
using FORGE3D;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 시작 시 프롤로그("prologue" 시퀀스)와 함께 재생되는 연출 담당.
/// StoryPlayer가 JSON의 cue 이름을 알려 주면, 그에 맞는 화면 연출을 한다.
///
///  show_warp_route          터널 속 워프 항행 + 출발지/도착지 표시
///  warp_storm               터널이 파랑→빨강, 표시가 경고창으로, 워프 아웃으로 터널 밖으로 이탈
///  screen_shake_whiteout    화면 흔들림 → 화이트아웃 → (그 사이 터널을 걷어내고 일반 우주로 복귀)
///  show_warp_module_broken  둥둥 떠 있는 함선 + 워프 모듈 고장 설명창
///  patrol_ships_approach    설명창을 닫고, 자치령 순찰함 2척이 좌우에서 접근해 함선을 둘러싼다
///  ship_seized              순찰함이 견인 광선으로 함선을 붙잡고(압류 경고창), 화면이 어두워진다
///  corvette_received        까만 화면에서 "보상 함선 지급" 안내. 함선이 새 함선(코르벳)으로 바뀐다
///  station_start            정거장 앞(선체 바깥, 워프 도착 자리)에서 밝아지며 시작
/// 터널에서 빠져나와 순찰함을 만나는 곳은 모항 정거장 선체 앞 바깥(_spaceSceneClearance)이다.
///
/// 연출은 큐에 쌓아 한 번에 하나씩 순서대로 실행한다. 대사를 빨리 넘겨도 연출이 겹치지 않는다.
/// Esc로 건너뛰면 남은 연출을 건너뛰고 곧바로 일반 우주로 복귀한다.
///
/// 이 컴포넌트는 Assets/Resources/Directors/PrologueDirector.prefab에 있고, 게임 씬이 로드될 때 한 번 자동 생성된다(씬 배치 불필요).
/// 에디터/개발 빌드에서는 개발자 모드의 "prologue off"로 끌 수 있다.
/// </summary>
public class PrologueDirector : MonoBehaviour
{
    public const string SequenceId = "prologue";
    public const string SkipPrefKey = "dev.skipPrologue";

    // SoundTable 이름
    private const string AlarmSound = "alarm";
    private const string WhiteoutSound = "whiteout";
    private const string InWarpSound = "inwarp";

    // 워프 엔진음은 경보음과 동시에 반복되므로 반복음 주인(키)을 따로 둔다
    private readonly object _inWarpLoop = new object();
    private const string ResourcePath = "Directors/PrologueDirector";

    private static bool _started;

    private enum Phase { Tunnel, Storm, Exit, Space, Docked }

    [Header("에셋 (프리팹에서 연결)")]
    [SerializeField] private GameObject _tunnelPrefab;
    [SerializeField] private GameObject[] _blueClusters;
    [SerializeField] private GameObject[] _redClusters;
    [Tooltip("워프 아웃 이펙트. 폭풍 색에 맞춰 red 변형(linear)을 쓴다.")]
    [SerializeField] private GameObject _warpOutPrefab;
    [SerializeField] private WarpClusterFlow _flow;
    [Tooltip("터널 빛이 함선을 비추는 느낌을 주는 조명. 터널 색(파랑→빨강)을 따라간다.")]
    [SerializeField] private Light _stageLight;
    [SerializeField] private Vector3 _stageLightOffset = new Vector3(0f, -45f, -70f);

    [Header("UI (프리팹에서 연결)")]
    [SerializeField] private CanvasGroup _routePanel;
    [SerializeField] private Slider _routeProgress;
    [SerializeField] private CanvasGroup _warningPopup;
    [SerializeField] private CanvasGroup _brokenPopup;
    [Tooltip("까만 화면 위에 떠야 하므로 Overlay보다 아래(나중) 순서에 둔다.")]
    [SerializeField] private CanvasGroup _receivedPopup;
    [SerializeField] private Image _overlay;
    [Tooltip("경고창 본문. 폭풍 때와 압류 때 문구를 바꿔 쓴다.")]
    [SerializeField] private TMP_Text _warningBody;
    [TextArea(1, 3)]
    [SerializeField] private string _seizedWarningText = "함선이 압류되었습니다.\n자치령 제7순찰전대";

    [Header("순찰함 / 압류 / 정거장")]
    [Tooltip("자치령 순찰함 모델 (Escort)")]
    [SerializeField] private GameObject _patrolShipPrefab;
    [SerializeField] private float _patrolScale = 1.3f;
    [Tooltip("모델이 +Z(앞)를 보도록 보정하는 회전 (Y 각도)")]
    [SerializeField] private Vector3 _patrolModelEuler = Vector3.zero;
    [Tooltip("멈춰 서는 자리 (함선 기준 X는 좌우 ±, Z는 앞쪽)")]
    [SerializeField] private Vector2 _patrolHoldXZ = new Vector2(95f, 60f);
    [Tooltip("접근을 시작하는 먼 자리 (함선 기준)")]
    [SerializeField] private Vector2 _patrolStartXZ = new Vector2(330f, 400f);
    [SerializeField] private float _patrolApproachSeconds = 5f;
    [Tooltip("터널에서 빠져나와 순찰함을 만나는 자리: 모항 정거장 선체 끝에서 이만큼 떨어진 곳 (정거장 앞, 워프 도착과 같은 쪽). 항성계가 없으면 씬에 놓인 함선 자리.")]
    [SerializeField, Min(0f)] private float _spaceSceneClearance = 1500f;
    [Tooltip("정거장 위치 기준으로 함선을 놓을 자리 (Y는 함선 높이를 맞추기 위해). 항성계가 있으면 XZ는 쓰지 않고 정거장 선체 바깥의 워프 도착 자리에 둔다.")]
    [SerializeField] private Vector3 _dockOffset = new Vector3(0f, 25f, -55f);

    [Header("무대")]
    [Tooltip("터널 무대의 위치. 실제 게임 공간과 겹치지 않게 멀리 둔다. (Y는 함선 높이를 따른다)")]
    [SerializeField] private Vector2 _stageXZ = new Vector2(0f, -40000f);
    [SerializeField] private float _swayAmount = 3f;

    [Header("카메라: 함선보다 약간 아래, 뒤에서 쫓아가는 바텀업 뷰")]
    [SerializeField] private Vector3 _cameraOffset = new Vector3(0f, -16f, -105f);
    [SerializeField] private Vector3 _lookOffset = new Vector3(0f, 5f, 30f);
    [SerializeField] private float _cameraFollow = 3f;
    [SerializeField] private float _fovStart = 58f;
    [SerializeField] private float _fovWarp = 72f;

    [Header("폭풍 / 워프 아웃")]
    [Tooltip("터널 중심에서 이 방향으로 터널 벽을 뚫고 나간다 (스테이지 기준)")]
    [SerializeField] private Vector3 _exitDirection = new Vector3(0.85f, 0.25f, 0.45f);
    [Tooltip("터널 중심축에서 이만큼 멀어지면 터널 밖으로 나간 것으로 본다")]
    [SerializeField] private float _exitDistance = 100f;
    [SerializeField] private float _tintSeconds = 1.6f;
    [SerializeField] private float _stormSpeedMultiplier = 1.9f;

    [Header("흔들림")]
    [SerializeField] private float _shakePosition = 1.4f;
    [SerializeField] private float _shakeAngle = 1.2f;

    // ── 상태 ──
    private SpaceshipController _ship;
    private Rigidbody _shipRb;
    private CameraController _cameraController;
    private Camera _cam;
    private InfiniteSpaceGrid _grid;
    private readonly List<Renderer> _hiddenShipRenderers = new List<Renderer>();

    private Vector3 _stageOrigin;
    private Vector3 _homePos;
    private Quaternion _homeRot;
    private float _originalFov;

    private GameObject _tunnel;
    private GameObject _warpOut;
    private F3DWarpJump _warpOutJump;
    private readonly List<TintTarget> _tints = new List<TintTarget>();
    private readonly Dictionary<CanvasGroup, Vector3> _baseScale = new Dictionary<CanvasGroup, Vector3>();

    private Phase _phase = Phase.Tunnel;
    private bool _stageActive;
    private bool _storyEnded;
    private bool _finished;
    private readonly Queue<string> _cues = new Queue<string>();

    private Vector3 _camPos;
    private float _warpStart;
    private float _routeStart;
    private bool _routeRunning;
    private bool _warningPulse;
    private float _shake;
    private float _shakeTarget;
    private float _speedTarget = 1f;
    private float _spaceStart;
    private float _swayWeight = 1f;

    private struct TintTarget
    {
        public Material material;
        public Color a0, b0, a1, b1;
    }

    private Color _lightBlue;
    private Color _lightRed;

    // 순찰함 / 압류
    private sealed class Patrol
    {
        public Transform transform;
        public Vector3 start;
        public Vector3 hold;
        public LineRenderer beam;
        public Light light;
        public float phase;
    }

    private readonly List<Patrol> _patrols = new List<Patrol>();
    private Material _beamMaterial;
    private float _approach;            // 0 → 1: 순찰함 접근 진행도
    private float _beamStrength;        // 0 → 1: 견인 광선 세기
    private string _stormWarningText;
    private bool _newShipApplied;
    private bool _dockApplied;

    private static readonly int TintAId = Shader.PropertyToID("_TintA");
    private static readonly int TintBId = Shader.PropertyToID("_TintB");

    // ── 시작 ──────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_started)
            return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (PlayerPrefs.GetInt(SkipPrefKey, 0) == 1)
            return;
#endif

        // 저장을 불러와 이어 하면 프롤로그 없이 저장한 곳에서 시작한다 (SaveSystem)
        if (SaveSystem.WillContinue)
            return;

        // 플레이어 함선과 추적 카메라가 있는 게임 씬에서만
        if (FindObjectOfType<SpaceshipController>() == null || FindObjectOfType<CameraController>() == null)
            return;

        var prefab = Resources.Load<PrologueDirector>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"[Prologue] Assets/Resources/{ResourcePath}.prefab을 찾지 못해 프롤로그 연출을 건너뜁니다.");
            return;
        }

        _started = true;
        Instantiate(prefab);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _started = false;
    }

    private void Awake()
    {
        _ship = FindObjectOfType<SpaceshipController>();
        _shipRb = _ship.GetComponent<Rigidbody>();
        _cameraController = FindObjectOfType<CameraController>();
        _cam = Camera.main;
        _grid = FindObjectOfType<InfiniteSpaceGrid>();

        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            GameFonts.ApplyKorean(text);

        if (_warningBody != null)
            _stormWarningText = _warningBody.text;

        foreach (CanvasGroup group in new[] { _routePanel, _warningPopup, _brokenPopup, _receivedPopup })
        {
            _baseScale[group] = group.transform.localScale;
            group.alpha = 0f;
            group.gameObject.SetActive(false);
        }

        EnterStage();
        StartCoroutine(Run());
    }

    private void OnDestroy()
    {
        StoryPlayer.OnCue -= HandleCue;
        StoryPlayer.OnSequenceEnded -= HandleEnded;
        GameAudio.StopLoop(this);
        GameAudio.StopLoop(_inWarpLoop);

        ClearPatrols();

        foreach (TintTarget tint in _tints)
        {
            if (tint.material != null)
                Destroy(tint.material);
        }
    }

    /// <summary>일반 게임 공간에서 터널 무대로 옮긴다. 첫 화면이 그려지기 전에(씬 오브젝트의 Start보다 먼저) 끝낸다.</summary>
    private void EnterStage()
    {
        _homePos = _ship.transform.position;
        _homeRot = _ship.transform.rotation;

        // 씬에 놓인 함선 자리는 아주 큰 정거장 선체 안이므로, 터널에서 빠져나와 순찰함을 만나는 곳은 정거장 앞 바깥으로 한다
        StationZone home = StationZone.Home;
        if (StarSystem.Instance != null && home != null)
        {
            Vector3 outside = StarSystem.Instance.StationApproach(home, _spaceSceneClearance);
            _homePos = new Vector3(outside.x, _homePos.y, outside.z);
        }

        _stageOrigin = new Vector3(_stageXZ.x, _homePos.y, _stageXZ.y);
        _originalFov = _cam.fieldOfView;

        // 프롤로그 동안 쓰지 않을 것들을 잠가 둔다. Start가 아직 안 돈 컴포넌트는 다시 켤 때 Start가 돈다
        // (CameraController는 Start에서 함선 뒤로 바로 자리를 잡는다).
        _ship.enabled = false;
        _cameraController.enabled = false;
        if (_grid != null) _grid.gameObject.SetActive(false);

        PlayerHud.Hidden = true;

        // 프롤로그 동안은 주인공의 원래 함선(UTI 화물선)의 모습. 보상 함선을 받으면(ApplyNewShip) 소형 전투선이 된다.
        PlayerShipAppearance.For(_ship).Set(PlayerShipAppearance.Look.Freighter);

        _shipRb.velocity = Vector3.zero;
        _shipRb.angularVelocity = Vector3.zero;
        _shipRb.isKinematic = true;
        _ship.transform.SetPositionAndRotation(_stageOrigin, Quaternion.identity);
        _shipRb.position = _stageOrigin;

        // 터널 + 성운 흐름
        _tunnel = Instantiate(_tunnelPrefab, _stageOrigin, Quaternion.identity);
        CollectTints(_tunnel);

        if (_stageLight != null)
        {
            // 조명 색은 터널 색을 따라간다 (파랑 → 빨강)
            _lightBlue = _stageLight.color;
            _lightRed = ToRed(_lightBlue);
            _stageLight.transform.position = _stageOrigin + _stageLightOffset;
        }

        _flow.transform.position = _stageOrigin;
        _flow.transform.rotation = Quaternion.identity;
        _flow.SetPalette(_blueClusters);
        _flow.SpeedMultiplier = 0.35f;
        _flow.Prewarm();

        _warpStart = Time.time;
        _stageActive = true;
        GameAudio.StartLoop(InWarpSound, _inWarpLoop); // 터널을 달리는 동안 워프 엔진음

        _overlay.color = Color.black;
        _overlay.gameObject.SetActive(true);

        _cam.fieldOfView = _fovStart;
        _camPos = _stageOrigin + _cameraOffset;
        ApplyCamera(true);
    }

    private void CollectTints(GameObject tunnel)
    {
        foreach (Renderer r in tunnel.GetComponentsInChildren<Renderer>())
        {
            if (r.sharedMaterial == null || r.sharedMaterial.shader.name != "FORGE3D/WarpTunnel")
                continue;

            // renderer.material은 복제본을 만들어 주므로 에셋의 머티리얼은 바뀌지 않는다
            Material m = r.material;
            Color a = m.GetColor(TintAId);
            Color b = m.GetColor(TintBId);

            _tints.Add(new TintTarget
            {
                material = m,
                a0 = a,
                b0 = b,
                a1 = ToRed(a),
                b1 = ToRed(b)
            });
        }
    }

    /// <summary>파랑 계열 색을 같은 밝기의 빨강 계열로 (R↔B 교환, 녹색은 줄여 주황기를 뺀다).</summary>
    private static Color ToRed(Color c)
    {
        return new Color(c.b, c.g * 0.45f, c.r, c.a);
    }

    // ── 큐 ────────────────────────────────────────────

    private void HandleCue(string cue)
    {
        // 프롤로그가 끝난 뒤 이어지는 장면(술집 등)의 cue는 이 연출과 상관없다 (쌓아 두지 않는다)
        if (!_storyEnded)
            _cues.Enqueue(cue);
    }

    private void HandleEnded(string sequenceId, bool skipped)
    {
        if (sequenceId != SequenceId)
            return;

        _storyEnded = true;

        // 건너뛰기: 진행 중인 연출을 멈추고, 끝까지 봤을 때와 같은 최종 상태(정거장에 붙은 새 함선)로 바로 만든다
        if (skipped)
        {
            StopAllCoroutines();
            FinalizeWorld();
            StartCoroutine(FinishWhenStoryIdle());
        }
    }

    /// <summary>프롤로그 연출이 만드는 최종 상태: 터널/순찰함은 없고, 새 함선이 정거장에 붙어 있고, 연출 UI는 모두 닫히고 경보음도 꺼짐.</summary>
    private void FinalizeWorld()
    {
        // 건너뛰기(StopAllCoroutines)로 경고창 깜빡임(PulsePopup)이 끝까지 돌지 못하면 경보음 반복을 끌 기회가 없다
        _warningPulse = false;
        GameAudio.StopLoop(this);

        RestoreWorld();
        ClearPatrols();
        ApplyNewShip();
        ApplyStationStart();

        _overlay.gameObject.SetActive(false);
        _routePanel.gameObject.SetActive(false);
        _warningPopup.gameObject.SetActive(false);
        _brokenPopup.gameObject.SetActive(false);
        _receivedPopup.gameObject.SetActive(false);
    }

    /// <summary>
    /// 프롤로그 뒤에 이어지는 시퀀스(then, 예: 튜토리얼 장면 1)가 모두 끝나면 플레이어에게 조작을 돌려준다.
    /// 이어지는 장면 동안 적이 스폰되거나 함선이 움직이면 안 되기 때문이다.
    /// 단, 조작하며 듣는 시퀀스(control, 예: 장면 4 전투)가 시작되면 그때 돌려준다 (그 뒤로도 재생 중이 유지되므로).
    /// </summary>
    private IEnumerator FinishWhenStoryIdle()
    {
        while (StoryPlayer.IsPlaying && !StoryPlayer.IsControlMode)
            yield return null;

        Finish();
    }

    private IEnumerator Run()
    {
        // 씬의 다른 오브젝트들이 첫 프레임을 지나도록 한 프레임 기다린다
        yield return null;

        StoryPlayer.OnCue += HandleCue;
        StoryPlayer.OnSequenceEnded += HandleEnded;

        if (!StoryPlayer.Play(SequenceId))
        {
            RestoreWorld();
            Finish();
            yield break;
        }

        StartCoroutine(FadeOverlay(Color.black, 1f, 0f, 1.4f));

        while (!(_storyEnded && _cues.Count == 0))
        {
            if (_cues.Count > 0)
                yield return RunCue(_cues.Dequeue());
            else
                yield return null;
        }

        // 프롤로그 연출은 여기까지. 이어지는 장면이 있으면 그것이 끝난 뒤에 조작을 돌려준다
        FinalizeWorld();
        yield return FinishWhenStoryIdle();
    }

    private IEnumerator RunCue(string cue)
    {
        switch (cue)
        {
            case "show_warp_route":
                yield return StepShowRoute();
                break;
            case "warp_storm":
                yield return StepStorm();
                break;
            case "screen_shake_whiteout":
                yield return StepWhiteout();
                break;
            case "show_warp_module_broken":
                yield return StepBrokenPopup();
                break;
            case "patrol_ships_approach":
                yield return StepPatrolApproach();
                break;
            case "ship_seized":
                yield return StepSeized();
                break;
            case "corvette_received":
                yield return StepReceived();
                break;
            case "station_start":
                yield return StepStationStart();
                break;
        }
    }

    // ── 연출 단계 ──────────────────────────────────────

    private IEnumerator StepShowRoute()
    {
        _phase = Phase.Tunnel;
        _routeStart = Time.time;
        _routeRunning = true;
        _routeProgress.value = 0f;
        yield return ShowPopup(_routePanel, 0.7f);
    }

    private IEnumerator StepStorm()
    {
        _phase = Phase.Storm;

        // 색: 파랑 → 빨강. 새로 나오는 성운도 붉은 계열로
        StartCoroutine(TintTo(1f, _tintSeconds));
        _flow.SetPalette(_redClusters);
        _speedTarget = _stormSpeedMultiplier;
        _shakeTarget = 0.35f;

        // 출발/도착 표시가 경고창으로 바뀐다
        _routeRunning = false;
        StartCoroutine(HidePopup(_routePanel, 0.35f));
        yield return ShowPopup(_warningPopup, 0.45f);
        StartCoroutine(PulsePopup(_warningPopup));

        // 워프 아웃: 터널 벽 쪽으로 충전하다가 바깥으로 쏘아져 나간다
        Vector3 exitDir = _exitDirection.normalized;
        _warpOut = Instantiate(_warpOutPrefab, _ship.transform.position, Quaternion.LookRotation(exitDir));
        _warpOutJump = _warpOut.GetComponent<F3DWarpJump>();
        if (_warpOutJump != null)
            _warpOutJump.DebugLoop = false; // 프리팹은 4초마다 반복 재생하도록 되어 있다 (Start 전에 끈다)

        _phase = Phase.Exit;

        float timeout = Time.time + 7f;
        while (Time.time < timeout && AxisDistance(_ship.transform.position) < _exitDistance)
            yield return null;

        SetShipVisible(false);
    }

    private IEnumerator StepWhiteout()
    {
        GameAudio.Play(WhiteoutSound);
        _shakeTarget = 1f;
        _warningPulse = false;
        StartCoroutine(HidePopup(_warningPopup, 0.35f));
        yield return new WaitForSeconds(0.5f);

        yield return FadeOverlay(Color.white, 0f, 1f, 1.0f);

        // 화면이 하얗게 가려진 사이에 터널을 걷어내고 일반 우주로 돌아온다
        RestoreWorld();
        _shake = 0f;
        _shakeTarget = 0f;
        yield return new WaitForSeconds(0.35f);

        yield return FadeOverlay(Color.white, 1f, 0f, 1.3f);
    }

    private IEnumerator StepBrokenPopup()
    {
        yield return ShowPopup(_brokenPopup, 0.5f);
    }

    /// <summary>자치령 순찰함 2척이 멀리서 날아와 함선 앞쪽 좌우를 막아선다.</summary>
    private IEnumerator StepPatrolApproach()
    {
        StartCoroutine(HidePopup(_brokenPopup, 0.4f));
        SpawnPatrols();

        for (float t = 0f; t < _patrolApproachSeconds; t += Time.deltaTime)
        {
            _approach = t / _patrolApproachSeconds;
            yield return null;
        }

        _approach = 1f;
    }

    /// <summary>순찰함이 견인 광선으로 함선을 붙잡고 압류 경고창이 뜬 뒤, 화면이 까맣게 어두워진다.</summary>
    private IEnumerator StepSeized()
    {
        StartCoroutine(RampBeams(0.7f));
        SetHeadlights(false);

        if (_warningBody != null)
            _warningBody.text = _seizedWarningText;

        yield return ShowPopup(_warningPopup, 0.45f);
        StartCoroutine(PulsePopup(_warningPopup));
        yield return new WaitForSeconds(1.4f);

        _warningPulse = false;
        yield return FadeOverlay(Color.black, 0f, 1f, 1.0f);
    }

    /// <summary>까만 화면 뒤에서 순찰함을 치우고 함선을 새 함선(코르벳)으로 바꾼 뒤, 보상 함선 지급 안내를 띄운다.</summary>
    private IEnumerator StepReceived()
    {
        _warningPulse = false;
        _warningPopup.alpha = 0f;
        _warningPopup.gameObject.SetActive(false);
        if (_warningBody != null)
            _warningBody.text = _stormWarningText;

        ClearPatrols();
        ApplyNewShip();

        yield return ShowPopup(_receivedPopup, 0.5f);
        yield return new WaitForSeconds(2.0f);
        yield return HidePopup(_receivedPopup, 0.4f);
    }

    /// <summary>함선을 정거장 구역 가장자리에 붙여 놓고 밝아진다.</summary>
    private IEnumerator StepStationStart()
    {
        ApplyStationStart();
        yield return FadeOverlay(Color.black, 1f, 0f, 1.6f);
    }

    // ── 순찰함 / 새 함선 / 정거장 ─────────────────────

    private void SpawnPatrols()
    {
        if (_patrolShipPrefab == null || _patrols.Count > 0)
            return;

        if (_beamMaterial == null)
            _beamMaterial = new Material(Shader.Find("Sprites/Default"));

        Vector3 center = _ship.transform.position;

        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;

            var root = new GameObject(i == 0 ? "PatrolShip_Left" : "PatrolShip_Right");
            root.transform.localScale = Vector3.one * _patrolScale;

            GameObject model = Instantiate(_patrolShipPrefab, root.transform);
            model.transform.localRotation = Quaternion.Euler(_patrolModelEuler);

            // 엔진 꼬리 (지나온 자리에 푸른 빛줄기)
            var trailGo = new GameObject("Trail");
            trailGo.transform.SetParent(root.transform, false);
            var trail = trailGo.AddComponent<TrailRenderer>();
            trail.material = _beamMaterial;
            trail.time = 0.8f;
            trail.widthMultiplier = 7f;
            trail.startColor = new Color(0.6f, 0.85f, 1f, 0.8f);
            trail.endColor = new Color(0.2f, 0.4f, 1f, 0f);

            // 붙잡을 때 쓰는 견인 광선 (평소엔 꺼져 있다)
            var beamGo = new GameObject("TractorBeam");
            beamGo.transform.SetParent(root.transform, false);
            var beam = beamGo.AddComponent<LineRenderer>();
            beam.useWorldSpace = true;
            beam.positionCount = 2;
            beam.material = _beamMaterial;
            beam.enabled = false;

            // 위협적인 붉은 점멸 등
            var lightGo = new GameObject("WarningLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 12f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.25f, 0.15f);
            light.range = 70f;

            var patrol = new Patrol
            {
                transform = root.transform,
                start = center + new Vector3(side * _patrolStartXZ.x, 0f, _patrolStartXZ.y),
                hold = center + new Vector3(side * _patrolHoldXZ.x, 0f, _patrolHoldXZ.y),
                beam = beam,
                light = light,
                phase = Random.value * 6.28f
            };

            root.transform.position = patrol.start;
            _patrols.Add(patrol);
        }

        _approach = 0f;
        _beamStrength = 0f;
    }

    private void AnimatePatrols()
    {
        if (_patrols.Count == 0)
            return;

        Vector3 shipPos = _ship.transform.position;
        float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(_approach), 3f);

        foreach (Patrol p in _patrols)
        {
            Vector3 pos = Vector3.Lerp(p.start, p.hold, k);
            if (_approach >= 1f)
                pos += new Vector3(Mathf.Sin(Time.time * 0.6f + p.phase) * 2f, 0f, Mathf.Cos(Time.time * 0.5f + p.phase) * 2f);

            // 날아오는 동안은 진행 방향을, 다 와서는 함선 쪽을 바라본다
            Quaternion travel = Quaternion.LookRotation(p.hold - p.start);
            Vector3 toShip = shipPos - pos;
            toShip.y = 0f;
            Quaternion face = toShip.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toShip) : travel;
            float turn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, _approach));

            p.transform.SetPositionAndRotation(pos, Quaternion.Slerp(travel, face, turn));

            p.light.intensity = 1.2f + 2.2f * Mathf.Clamp01(Mathf.Sin(Time.time * 6f + p.phase) + 0.3f);

            bool beamOn = _beamStrength > 0.01f;
            p.beam.enabled = beamOn;
            if (beamOn)
            {
                p.beam.SetPosition(0, pos);
                p.beam.SetPosition(1, shipPos);
                p.beam.widthMultiplier = (2.5f + 1.2f * Mathf.Sin(Time.time * 14f + p.phase)) * _beamStrength;
                Color c = new Color(1f, 0.35f, 0.2f, 0.9f * _beamStrength);
                p.beam.startColor = c;
                p.beam.endColor = c;
            }
        }
    }

    private IEnumerator RampBeams(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            _beamStrength = t / seconds;
            yield return null;
        }

        _beamStrength = 1f;
    }

    private void ClearPatrols()
    {
        foreach (Patrol p in _patrols)
        {
            if (p.transform != null)
                Destroy(p.transform.gameObject);
        }

        _patrols.Clear();
        _approach = 0f;
        _beamStrength = 0f;

        if (_beamMaterial != null)
        {
            Destroy(_beamMaterial);
            _beamMaterial = null;
        }
    }

    private void SetHeadlights(bool on)
    {
        foreach (Light l in _ship.GetComponentsInChildren<Light>(true))
        {
            if (l.type == LightType.Spot)
                l.enabled = on;
        }
    }

    /// <summary>원래 함선(화물선) 모습을 보상으로 받은 새 함선(소형 전투선)으로 바꾼다. 여러 번 불러도 안전하다.</summary>
    private void ApplyNewShip()
    {
        if (_newShipApplied)
            return;

        _newShipApplied = true;

        PlayerShipAppearance.For(_ship).Set(PlayerShipAppearance.Look.Fighter);

        SetHeadlights(true);
    }

    /// <summary>함선을 정거장 구역 가장자리에 놓는다. 정거장은 모델 없이 구역(StationZone)만 있어서 그 위치를 기준으로 한다.</summary>
    private void ApplyStationStart()
    {
        if (_dockApplied)
            return;

        _dockApplied = true;

        // 항성계가 있으면 정거장 선체 바깥의 워프 도착 자리 (정거장이 커서 중심 기준으로 재면 선체 안에 들어간다)
        StationZone zone = StationZone.Home;
        Vector3 pos = zone != null ? zone.transform.position + _dockOffset : _homePos;
        if (zone != null && StarSystem.Instance != null)
        {
            Vector3 outside = StarSystem.Instance.StationArrival(zone);
            pos = new Vector3(outside.x, pos.y, outside.z);
        }

        _ship.transform.SetPositionAndRotation(pos, Quaternion.identity);
        _shipRb.position = pos;
        _shipRb.rotation = Quaternion.identity;

        // 멀리 순간이동했으니 카메라도 미끄러져 오지 않고 바로 따라 붙게 한다
        _cameraController.SnapToTarget();

        _phase = Phase.Docked;
    }

    // ── 무대 정리 / 복귀 ───────────────────────────────

    /// <summary>터널 무대를 걷어내고 함선을 원래 자리의 일반 우주로 되돌린다. 여러 번 불러도 안전하다.</summary>
    private void RestoreWorld()
    {
        if (!_stageActive)
            return;

        _stageActive = false;
        GameAudio.StopLoop(_inWarpLoop);

        if (_tunnel != null) Destroy(_tunnel);
        if (_warpOut != null) Destroy(_warpOut);
        if (_flow != null) Destroy(_flow.gameObject);
        if (_stageLight != null) Destroy(_stageLight.gameObject);

        SetShipVisible(true);
        _ship.transform.SetPositionAndRotation(_homePos, _homeRot);
        _shipRb.position = _homePos;
        _shipRb.rotation = _homeRot;

        _cam.fieldOfView = _originalFov;
        if (_grid != null) _grid.gameObject.SetActive(true);

        // 다시 켜면 CameraController.Start가 돌아 카메라가 함선 뒤 원래 위치로 바로 자리 잡는다
        _cameraController.enabled = true;

        _phase = Phase.Space;
        _spaceStart = Time.time;
    }

    /// <summary>프롤로그 연출을 끝내고 플레이어에게 조작을 돌려준다.</summary>
    private void Finish()
    {
        if (_finished)
            return;

        _finished = true;

        StoryPlayer.OnCue -= HandleCue;
        StoryPlayer.OnSequenceEnded -= HandleEnded;

        // 건너뛰어도 같은 최종 상태가 되도록: 순찰함 정리, 새 함선, 정거장에 붙은 채로 시작
        ClearPatrols();
        ApplyNewShip();
        ApplyStationStart();

        _overlay.gameObject.SetActive(false);
        _routePanel.gameObject.SetActive(false);
        _warningPopup.gameObject.SetActive(false);
        _brokenPopup.gameObject.SetActive(false);
        _receivedPopup.gameObject.SetActive(false);

        _shipRb.isKinematic = false;
        _shipRb.velocity = Vector3.zero;
        _shipRb.angularVelocity = Vector3.zero;
        _ship.enabled = true;

        PlayerHud.Hidden = false;

        Destroy(gameObject);
    }

    private void SetShipVisible(bool visible)
    {
        if (visible)
        {
            foreach (Renderer r in _hiddenShipRenderers)
            {
                if (r != null)
                    r.enabled = true;
            }
            _hiddenShipRenderers.Clear();
            return;
        }

        foreach (Renderer r in _ship.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled)
                continue;

            r.enabled = false;
            _hiddenShipRenderers.Add(r);
        }
    }

    // ── 프레임마다 ─────────────────────────────────────

    private void Update()
    {
        if (_finished)
            return;

        // 항행 중 표시줄 (끝까지 차지 않고 천천히 차오른다)
        if (_routeRunning)
            _routeProgress.value = 0.72f * (1f - Mathf.Exp(-(Time.time - _routeStart) / 12f));

        _shake = Mathf.MoveTowards(_shake, _shakeTarget, Time.deltaTime * 1.5f);

        if (_stageActive)
            _flow.SpeedMultiplier = Mathf.MoveTowards(_flow.SpeedMultiplier, Mathf.Lerp(1f, _speedTarget, _phase >= Phase.Storm ? 1f : 0f), Time.deltaTime * 0.6f);
    }

    private void LateUpdate()
    {
        if (_finished)
            return;

        if (_stageActive)
        {
            AnimateShip();
            ApplyCamera(false);
        }
        else if (_phase == Phase.Space)
        {
            FloatShip();
            AnimatePatrols();
        }
    }

    /// <summary>터널 속 함선: 가볍게 흔들리다가, 워프 아웃이 시작되면 이펙트가 정한 위치를 따라간다.</summary>
    private void AnimateShip()
    {
        Transform ship = _ship.transform;
        float t = Time.time - _warpStart;

        if (_phase == Phase.Exit && _warpOutJump != null && _warpOutJump.ShipPos != null)
        {
            ship.position = _warpOutJump.ShipPos.position;
            ship.rotation = Quaternion.Slerp(ship.rotation, Quaternion.LookRotation(_exitDirection.normalized), Time.deltaTime * 2.5f);
        }
        else
        {
            // 속도감을 위해 좌우로 천천히 흔들리며 약간 기울어진다
            _swayWeight = Mathf.MoveTowards(_swayWeight, _phase == Phase.Storm ? 0f : 1f, Time.deltaTime * 0.8f);
            Vector3 sway = new Vector3(Mathf.Sin(t * 0.9f) * _swayAmount, Mathf.Sin(t * 1.3f) * _swayAmount * 0.5f, 0f) * _swayWeight;
            ship.position = _stageOrigin + sway;

            Quaternion tilt = Quaternion.Euler(Mathf.Sin(t * 1.1f) * 2f, Mathf.Sin(t * 0.7f) * 3f, -Mathf.Sin(t * 0.9f) * 6f * _swayWeight);
            ship.rotation = Quaternion.Slerp(ship.rotation, tilt, Time.deltaTime * 3f);

            if (_phase == Phase.Storm)
                ship.rotation = Quaternion.Slerp(ship.rotation, Quaternion.LookRotation(_exitDirection.normalized), Time.deltaTime * 0.8f);
        }

        _shipRb.position = ship.position;
        _shipRb.rotation = ship.rotation;
    }

    /// <summary>워프를 끝내고 우주에 둥둥 떠 있는 함선 (조작은 아직 돌려주지 않는다).</summary>
    private void FloatShip()
    {
        float t = Time.time - _spaceStart;
        Vector3 pos = _homePos + new Vector3(Mathf.Sin(t * 0.45f) * 2.5f, Mathf.Sin(t * 0.7f) * 0.5f, t * 0.5f);
        Quaternion rot = Quaternion.Euler(Mathf.Sin(t * 0.4f) * 1.5f, Mathf.Sin(t * 0.25f) * 8f, Mathf.Sin(t * 0.35f) * 4f) * _homeRot;

        _ship.transform.SetPositionAndRotation(pos, rot);
        _shipRb.position = pos;
        _shipRb.rotation = rot;
    }

    /// <summary>함선 뒤·아래에서 올려다보며 쫓아가는 카메라 + 흔들림.</summary>
    private void ApplyCamera(bool snap)
    {
        Vector3 shipPos = _ship.transform.position;
        Vector3 wantPos = shipPos + _cameraOffset;

        _camPos = snap ? wantPos : Vector3.Lerp(_camPos, wantPos, 1f - Mathf.Exp(-_cameraFollow * Time.deltaTime));

        Vector3 lookAt = shipPos + _lookOffset;
        Quaternion rot = Quaternion.LookRotation(lookAt - _camPos, Vector3.up);

        // 워프가 깊어질수록 시야를 넓혀 속도감을 준다
        float warp = Mathf.Clamp01((Time.time - _warpStart) / 5f);
        float stormBoost = _phase >= Phase.Storm ? 1f : 0f;
        float targetFov = Mathf.Lerp(_fovStart, _fovWarp, Mathf.Max(warp * 0.6f, stormBoost));
        _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov, Time.deltaTime * 1.5f);

        Vector3 shakePos = Vector3.zero;
        Vector3 shakeRot = Vector3.zero;
        if (_shake > 0.001f)
        {
            float n = Time.time * 28f;
            shakePos = new Vector3(Noise(n, 1f), Noise(n, 2f), Noise(n, 3f)) * (_shakePosition * _shake);
            shakeRot = new Vector3(Noise(n, 4f), Noise(n, 5f), Noise(n, 6f)) * (_shakeAngle * _shake);
        }

        _cam.transform.SetPositionAndRotation(_camPos + shakePos, rot * Quaternion.Euler(shakeRot));
    }

    private static float Noise(float t, float seed)
    {
        return Mathf.PerlinNoise(t, seed * 17.3f) * 2f - 1f;
    }

    private float AxisDistance(Vector3 worldPos)
    {
        // 터널은 스테이지 +Z축을 따라 뻗어 있으므로, 중심축에서의 거리는 XY 평면 거리다
        return new Vector2(worldPos.x - _stageOrigin.x, worldPos.y - _stageOrigin.y).magnitude;
    }

    // ── 공용 트윈 ──────────────────────────────────────

    /// <summary>터널 색을 파랑(0) → 빨강(1) 방향으로 seconds초 동안 바꾼다.</summary>
    private IEnumerator TintTo(float target, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            SetTint(Mathf.Lerp(0f, target, t / seconds));
            yield return null;
        }

        SetTint(target);
    }

    private void SetTint(float k)
    {
        foreach (TintTarget tint in _tints)
        {
            if (tint.material == null)
                continue;

            tint.material.SetColor(TintAId, Color.Lerp(tint.a0, tint.a1, k));
            tint.material.SetColor(TintBId, Color.Lerp(tint.b0, tint.b1, k));
        }

        if (_stageLight != null)
            _stageLight.color = Color.Lerp(_lightBlue, _lightRed, k);
    }

    private IEnumerator FadeOverlay(Color color, float from, float to, float seconds)
    {
        _overlay.gameObject.SetActive(true);

        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            color.a = Mathf.Lerp(from, to, t / seconds);
            _overlay.color = color;
            yield return null;
        }

        color.a = to;
        _overlay.color = color;
        _overlay.gameObject.SetActive(to > 0f);
    }

    private IEnumerator ShowPopup(CanvasGroup group, float seconds)
    {
        Vector3 baseScale = _baseScale[group];
        group.gameObject.SetActive(true);

        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / seconds, 3f);
            group.alpha = k;
            group.transform.localScale = baseScale * Mathf.Lerp(0.88f, 1f, k);
            yield return null;
        }

        group.alpha = 1f;
        group.transform.localScale = baseScale;
    }

    private IEnumerator HidePopup(CanvasGroup group, float seconds)
    {
        if (!group.gameObject.activeSelf)
            yield break;

        float from = group.alpha;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            group.alpha = Mathf.Lerp(from, 0f, t / seconds);
            yield return null;
        }

        group.alpha = 0f;
        group.gameObject.SetActive(false);
    }

    /// <summary>경고창이 깜빡이며 경보음과 함께 위급함을 알린다. _warningPulse를 끄면 멈춘다(닫히는 페이드와 알파를 다투지 않도록).</summary>
    private IEnumerator PulsePopup(CanvasGroup group)
    {
        _warningPulse = true;
        GameAudio.StartLoop(AlarmSound, this);

        while (_warningPulse && group.gameObject.activeSelf)
        {
            group.alpha = 0.78f + 0.22f * (0.5f + 0.5f * Mathf.Sin(Time.time * 9f));
            yield return null;
        }

        GameAudio.StopLoop(this);
    }
}
