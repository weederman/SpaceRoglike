using System.Collections;
using System.Collections.Generic;
using FORGE3D;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 장면 3("첫 워프") 연출 담당. "tutorial_warp" 시퀀스의 cue를 받아 화면 연출을 한다.
///
///  s3_depart        함선이 정거장에서 빠져나가 속도를 올린다 (정거장이 뒤로 멀어진다)
///  s3_beacon_show   앞쪽에 워프 비콘이 나타난다. 이 줄의 waitFor 때문에 플레이어가 비콘을 클릭해야 다음으로 넘어간다
///  s3_warp_charge   엔진이 푸른빛으로 차오른다 (충전)
///  s3_warp_go       앞쪽에 워프 게이트(워프 아웃 이펙트)가 열리고, 함선이 감속 없이 게이트로 돌진해 그대로 푸른 워프 터널로 들어간다
///                   (터널 안에서는 워프 엔진음이 반복된다)
///  s3_warp_arrive   하얗게 번쩍인 뒤 소행성 지대에 도착한다 (암석과 선박 잔해가 흩어진 곳)
///
/// 장면 6("귀환", 시퀀스 "tutorial_return")의 귀환 워프도 같은 터널 무대를 쓰므로 여기서 한다:
///  s6_return        워프 아웃 → 짧은 터널 → 정거장 격납고 자리에 도착. 소행성 지대는 치운다.
///
/// 연출이 끝났음을 스토리에 신호로 알린다 (JSON의 waitFor로 대사가 연출보다 앞서 가지 않게):
///  warp_tunnel      워프 게이트로 돌진해 터널에 들어갔다 (장면 3, 6)
///  warp_arrived     장면 3 도착 연출이 끝났다
///  return_arrived   장면 6 귀환 연출이 끝났다
///                   정거장 구역 안에 서므로 대사가 끝나면 상점(정비 메뉴)이 열린다. 조작은 시퀀스가 끝날 때 돌려준다.
///
/// cue는 큐에 쌓아 한 번에 하나씩 순서대로 실행한다. 레이더 연출(radar_show/hide)은 RadarPing이 따로 받는다.
/// Esc로 건너뛰면 남은 연출을 건너뛰고 곧바로 도착한 뒤의 상태가 된다. 프리팹: Assets/Resources/Directors/Cue/FirstWarpDirector.prefab
/// </summary>
public class FirstWarpDirector : MonoBehaviour
{
    private const string SequenceId = "tutorial_warp";
    private const string CuePrefix = "s3_";
    private const string ReturnSequenceId = "tutorial_return";
    private const string ReturnCue = "s6_return";
    // SoundTable 이름
    private const string InWarpSound = "inwarp";
    private const string WarpStartSound = "warp_start";
    private const string WarpEndSound = "warp_end";

    // 연출이 끝났음을 알리는 스토리 신호
    private const string TunnelSignal = "warp_tunnel";
    private const string ArrivedSignal = "warp_arrived";
    private const string ReturnedSignal = "return_arrived";

    [Header("에셋 (프리팹에서 연결)")]
    [SerializeField] private WarpTunnelStage _stage;
    [SerializeField] private GameObject _beaconPrefab;
    [Tooltip("워프 아웃 이펙트 (푸른색 linear)")]
    [SerializeField] private GameObject _warpOutPrefab;
    [SerializeField] private AsteroidField _asteroidFieldPrefab;

    [Header("UI (프리팹에서 연결)")]
    [SerializeField] private RectTransform _canvasRect;
    [SerializeField] private RectTransform _beaconMarker;
    [SerializeField] private TMP_Text _beaconHint;
    [SerializeField] private Image _flash;
    [Tooltip("워프 충전 때 함선을 푸르게 비추는 조명")]
    [SerializeField] private Light _chargeLight;

    [Header("출항")]
    [SerializeField] private Vector3 _departDirection = Vector3.right;
    [SerializeField] private float _departDistance = 250f;
    [SerializeField] private float _departSeconds = 6.5f;
    [Tooltip("출발 지점에서 비콘까지의 거리")]
    [SerializeField] private float _beaconDistance = 335f;

    [Header("충전 / 도착")]
    [SerializeField] private float _chargeSeconds = 2.2f;
    [Tooltip("워프 게이트가 충전된 뒤 함선이 돌진하는 거리. 가속도가 점점 커지며 감속하지 않고 그대로 터널로 들어간다.")]
    [SerializeField, Min(10f)] private float _gateDistance = 260f;
    [Tooltip("돌진하는 시간(초). 짧을수록 세게 가속한다.")]
    [SerializeField, Min(0.2f)] private float _dashSeconds = 1.2f;
    [Tooltip("워프 터널 안에 최소한 머무는 시간(초). 대사를 빨리 넘겨도 이 시간은 터널을 보여준다.")]
    [SerializeField] private float _minTunnelSeconds = 5f;
    [Tooltip("소행성 지대 도착 위치 (XZ). 항성계(StarSystem)가 있으면 그 튜토리얼 비콘(레벨 1)에 도착하고, 없을 때만 이 값을 쓴다.")]
    [SerializeField] private Vector2 _arrivalXZ = new Vector2(2600f, 2400f);

    [Header("귀환 (장면 6)")]
    [Tooltip("귀환 터널 안에 머무는 시간(초)")]
    [SerializeField, Min(0f)] private float _returnTunnelSeconds = 3.5f;
    [Tooltip("정거장 구역(StationZone) 중심에서 도착 위치까지 (XZ). 프롤로그가 처음 함선을 둔 자리와 같다.")]
    [SerializeField] private Vector2 _returnDockOffset = new Vector2(0f, -55f);

    // ── 장면 참조 ──
    private SpaceshipController _ship;
    private Rigidbody _rb;
    private CameraController _cameraController;
    private InfiniteSpaceGrid _grid;
    private Camera _cam;

    // 제어권을 가져가기 전 상태 (프롤로그 연출이 이미 잠가 두었을 수도 있으니 "원래대로"가 아니라 "가져가기 전대로" 돌려준다)
    private bool _shipWasEnabled;
    private bool _rbWasKinematic;
    private bool _controlTaken;

    private readonly List<Renderer> _hiddenRenderers = new List<Renderer>();
    private readonly Queue<string> _queue = new Queue<string>();
    private Coroutine _pump;

    private float _baseY;
    private Vector3 _departStart;
    private Vector3 _departEnd;
    private bool _departed;
    private bool _arrived;
    private WarpBeacon _beacon;
    private GameObject _warpOut;
    private AsteroidField _field;
    private bool _returned;

    private void OnEnable()
    {
        StoryPlayer.OnCue += HandleCue;
        StoryPlayer.OnSequenceEnded += HandleEnded;
    }

    private void OnDisable()
    {
        StoryPlayer.OnCue -= HandleCue;
        StoryPlayer.OnSequenceEnded -= HandleEnded;
        GameAudio.StopLoop(this);
    }

    private void Awake()
    {
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            GameFonts.ApplyKorean(text);

        _beaconMarker.gameObject.SetActive(false);
        _flash.gameObject.SetActive(false);
        _chargeLight.enabled = false;
    }

    // ── 큐 ────────────────────────────────────────────

    private void HandleCue(string cue)
    {
        if (!cue.StartsWith(CuePrefix) && cue != ReturnCue)
            return;

        _queue.Enqueue(cue);
        if (_pump == null)
            _pump = StartCoroutine(Pump());
    }

    private void HandleEnded(string sequenceId, bool skipped)
    {
        if (sequenceId != SequenceId && sequenceId != ReturnSequenceId)
            return;

        bool isReturn = sequenceId == ReturnSequenceId;

        if (skipped)
        {
            // 건너뛰기: 진행 중인 연출을 멈추고 도착한 뒤의 최종 상태로 바로 만든다
            StopAllCoroutines();
            _pump = null;
            _queue.Clear();
            if (isReturn) FinalizeReturn();
            else FinalizeWorld();
        }
        else
        {
            StartCoroutine(FinishWhenIdle(isReturn));
        }
    }

    private IEnumerator FinishWhenIdle(bool isReturn)
    {
        while (_pump != null || _queue.Count > 0)
            yield return null;

        if (isReturn) FinalizeReturn();
        else FinalizeWorld();
    }

    private IEnumerator Pump()
    {
        while (_queue.Count > 0)
            yield return RunCue(_queue.Dequeue());

        _pump = null;
    }

    private IEnumerator RunCue(string cue)
    {
        switch (cue)
        {
            case "s3_depart":
                yield return StepDepart();
                break;
            case "s3_beacon_show":
                StepBeaconShow();
                break;
            case "s3_warp_charge":
                yield return StepCharge();
                break;
            case "s3_warp_go":
                yield return StepGo();
                break;
            case "s3_warp_arrive":
                yield return StepArrive();
                break;
            case ReturnCue:
                yield return StepReturn();
                break;
        }
    }

    // ── 연출 단계 ──────────────────────────────────────

    private IEnumerator StepDepart()
    {
        TakeControl();

        Vector3 dir = _departDirection.normalized;
        _departStart = _ship.transform.position;
        _departEnd = _departStart + dir * _departDistance;
        _departed = true;

        Quaternion fromRot = _ship.transform.rotation;
        Quaternion toRot = Quaternion.LookRotation(dir);

        // 출발 → 가속 → 감속 (부드럽게 멈춘다)
        for (float t = 0f; t < _departSeconds; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / _departSeconds);
            Quaternion rot = Quaternion.Slerp(fromRot, toRot, Mathf.SmoothStep(0f, 1f, t / 1.2f));
            MoveShip(_departStart + dir * (_departDistance * k), rot);
            yield return null;
        }

        MoveShip(_departEnd, toRot);
    }

    private void StepBeaconShow()
    {
        if (_beacon != null)
            return;

        EnsureRefs();

        Vector3 origin = _departed ? _departStart : _ship.transform.position;
        Vector3 pos = origin + _departDirection.normalized * _beaconDistance;
        pos.y = _baseY;

        var go = Instantiate(_beaconPrefab, pos, Quaternion.identity);
        _beacon = go.GetComponent<WarpBeacon>();
        _beacon.Init(_beaconMarker, _beaconHint, _canvasRect);
        _beacon.Show();
    }

    private IEnumerator StepCharge()
    {
        TakeControl();
        EnsureDeparted();

        _chargeLight.enabled = true;

        for (float t = 0f; t < _chargeSeconds; t += Time.deltaTime)
        {
            float k = t / _chargeSeconds;
            _chargeLight.transform.position = _ship.transform.position + new Vector3(0f, 30f, -20f);
            _chargeLight.intensity = Mathf.Lerp(0f, 6f, k);
            if (_beacon != null)
                _beacon.SetCharge(k);

            // 엔진이 떨리는 느낌으로 아주 조금 흔든다
            Vector3 jitter = Random.insideUnitSphere * (0.5f * k);
            MoveShip(_departEnd + jitter, _ship.transform.rotation);
            yield return null;
        }

        MoveShip(_departEnd, _ship.transform.rotation);
    }

    private IEnumerator StepGo()
    {
        TakeControl();
        EnsureDeparted();

        yield return DashIntoTunnel(_ship.transform.position, _departDirection.normalized);

        if (_beacon != null) Destroy(_beacon.gameObject);
        _beacon = null;
        _chargeLight.enabled = false;

        yield return FlashTo(0f, 0.7f);
    }

    /// <summary>
    /// 함선 자리에 워프 게이트가 열리고, 충전되는 동안 함선이 멈춰 선 뒤(기존 연출) 가속도가 점점 커지며 돌진한다.
    /// 감속하지 않고 같은 속도로 계속 나아가는 채로 하얗게 번쩍이고 곧바로 워프 터널로 들어간다.
    /// 섬광이 덮인 상태로 끝나니, 부르는 쪽이 섬광을 걷는다.
    /// </summary>
    private IEnumerator DashIntoTunnel(Vector3 start, Vector3 dir)
    {
        Quaternion rot = Quaternion.LookRotation(dir);
        System.Action<Vector3> move = p => MoveShip(p, rot);

        _warpOut = WarpDash.SpawnGate(_warpOutPrefab, start, dir);
        GameAudio.Play(WarpStartSound);
        yield return WarpDash.Launch(start, Vector3.zero, WarpDash.GateChargeSeconds, dir, _gateDistance, _dashSeconds, move);

        Coroutine coast = StartCoroutine(WarpDash.Coast(WarpDash.LaunchEnd(start, Vector3.zero, WarpDash.GateChargeSeconds, dir, _gateDistance),
            dir, WarpDash.FinalSpeed(_gateDistance, _dashSeconds), move));
        yield return FlashTo(1f, 0.25f);
        StopCoroutine(coast);

        if (_warpOut != null) Destroy(_warpOut);
        _cameraController.enabled = false;
        if (_grid != null) _grid.gameObject.SetActive(false);
        _stage.Enter(_ship.transform, _rb, _cam);
        GameAudio.StartLoop(InWarpSound, this);
        StoryPlayer.Signal(TunnelSignal);
    }

    private IEnumerator StepArrive()
    {
        TakeControl();

        // 대사를 빨리 넘겨도 터널을 달리는 장면이 너무 짧게 지나가지 않도록
        while (_stage.IsActive && _stage.ActiveSeconds < _minTunnelSeconds)
            yield return null;

        yield return FlashTo(1f, 0.4f);

        LeaveTunnel();
        GameAudio.Play(WarpEndSound);
        PlaceAtArrival();

        yield return FlashTo(0f, 1.0f);
        StoryPlayer.Signal(ArrivedSignal);
        StartCoroutine(DriftIn());
    }

    /// <summary>도착 직후, 워프 때의 속도가 남아 앞으로 조금 미끄러지다 멈춘다.</summary>
    private IEnumerator DriftIn()
    {
        Vector3 start = _ship.transform.position;
        Quaternion rot = _ship.transform.rotation;

        for (float t = 0f; t < 3f; t += Time.deltaTime)
        {
            MoveShip(start + Vector3.forward * (40f * (1f - Mathf.Exp(-t))), rot);
            yield return null;
        }
    }

    /// <summary>장면 6 귀환: 바라보는 방향으로 워프 게이트에 돌진 → 짧은 터널 → 정거장 격납고 자리에 도착.</summary>
    private IEnumerator StepReturn()
    {
        TakeControl();

        Vector3 dir = _ship.transform.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f)
            dir = Vector3.forward;

        yield return DashIntoTunnel(_ship.transform.position, dir.normalized);
        yield return FlashTo(0f, 0.6f);

        while (_stage.IsActive && _stage.ActiveSeconds < _returnTunnelSeconds)
            yield return null;

        yield return FlashTo(1f, 0.4f);
        LeaveTunnel();
        GameAudio.Play(WarpEndSound);
        PlaceAtStation();
        yield return FlashTo(0f, 1.0f);
        StoryPlayer.Signal(ReturnedSignal);
    }

    /// <summary>귀환을 끝까지 봤을 때와 같은 최종 상태: 정거장 격납고 자리에 있고, 소행성 지대와 연출 오브젝트는 없다.</summary>
    private void FinalizeReturn()
    {
        EnsureRefs();
        LeaveTunnel();

        if (_warpOut != null) Destroy(_warpOut);
        _flash.gameObject.SetActive(false);
        SetShipVisible(true);

        if (!_returned)
            PlaceAtStation();

        ReleaseControl();
    }

    private void PlaceAtStation()
    {
        EnsureRefs();

        StationZone zone = StationZone.Home;
        Vector3 center = zone != null ? zone.transform.position : Vector3.zero;
        Vector3 pos = new Vector3(center.x + _returnDockOffset.x, _baseY, center.z + _returnDockOffset.y);
        MoveShip(pos, Quaternion.identity);
        _cameraController.SnapToTarget();

        // 소행성 지대는 그 자리에 남겨 두지 않는다 (함선 주위에 따라붙는 장식이라 정거장까지 따라온다)
        AsteroidField field = _field != null ? _field : AsteroidField.Active;
        if (field != null)
            Destroy(field.gameObject);
        _field = null;

        _returned = true;
    }

    // ── 정리 ──────────────────────────────────────────

    /// <summary>끝까지 봤을 때와 같은 최종 상태: 소행성 지대에 도착해 있고, 연출 오브젝트와 UI는 없다.</summary>
    private void FinalizeWorld()
    {
        if (!_controlTaken && _ship == null)
            EnsureRefs();

        LeaveTunnel();

        if (_warpOut != null) Destroy(_warpOut);
        if (_beacon != null) Destroy(_beacon.gameObject);
        _beacon = null;
        _beaconMarker.gameObject.SetActive(false);
        _chargeLight.enabled = false;
        _flash.gameObject.SetActive(false);

        SetShipVisible(true);

        if (!_arrived)
            PlaceAtArrival();

        ReleaseControl();
    }

    private void LeaveTunnel()
    {
        GameAudio.StopLoop(this);

        if (_stage.IsActive)
            _stage.Exit();

        if (_cameraController != null)
            _cameraController.enabled = true;

        if (_grid != null)
            _grid.gameObject.SetActive(true);
    }

    private void PlaceAtArrival()
    {
        EnsureRefs();

        Vector3 pos = new Vector3(_arrivalXZ.x, _baseY, _arrivalXZ.y);
        if (StarSystem.Instance != null && StarSystem.Instance.TryGetTutorialArrival(out Vector3 beaconArrival))
            pos = new Vector3(beaconArrival.x, _baseY, beaconArrival.z);

        _ship.transform.SetPositionAndRotation(pos, Quaternion.identity);
        _rb.position = pos;
        _rb.rotation = Quaternion.identity;

        // 멀리 이동했으니 카메라도 미끄러져 오지 않고 바로 따라 붙게 한다
        _cameraController.SnapToTarget();

        if (_field == null)
            _field = Instantiate(_asteroidFieldPrefab, pos, Quaternion.identity);

        _arrived = true;
    }

    // ── 공용 ──────────────────────────────────────────

    private void EnsureRefs()
    {
        if (_ship != null)
            return;

        _ship = FindObjectOfType<SpaceshipController>();
        _baseY = _ship.transform.position.y; // 함선이 다니는 높이. 터널 안에서 흔들려도 도착은 이 높이로.
        _rb = _ship.GetComponent<Rigidbody>();
        _cameraController = FindObjectOfType<CameraController>();
        _grid = FindObjectOfType<InfiniteSpaceGrid>();
        _cam = Camera.main;
    }

    /// <summary>출발 연출을 건너뛰고(또는 단독 재생으로) 충전/워프부터 시작하는 경우를 위해, 출발 지점을 지금 위치로 잡는다.</summary>
    private void EnsureDeparted()
    {
        if (_departed)
            return;

        _departStart = _ship.transform.position;
        _departEnd = _departStart;
        _departed = true;
    }

    private void TakeControl()
    {
        EnsureRefs();

        if (_controlTaken)
            return;

        _controlTaken = true;
        _shipWasEnabled = _ship.enabled;
        _rbWasKinematic = _rb.isKinematic;

        _ship.enabled = false;

        // 이미 키네마틱이면(프롤로그 연출이 잠가 둔 경우) 속도를 건드릴 수 없다
        if (!_rb.isKinematic)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        _rb.isKinematic = true;
    }

    private void ReleaseControl()
    {
        if (!_controlTaken)
            return;

        _controlTaken = false;

        // 프롤로그 연출이 먼저 조작을 돌려줬다면(= 이미 켜져 있다면) 건드리지 않는다
        if (!_ship.enabled && _rb.isKinematic)
        {
            _ship.enabled = _shipWasEnabled;
            _rb.isKinematic = _rbWasKinematic;
        }
    }

    private void MoveShip(Vector3 position, Quaternion rotation)
    {
        _ship.transform.SetPositionAndRotation(position, rotation);
        _rb.position = position;
        _rb.rotation = rotation;
    }

    private void SetShipVisible(bool visible)
    {
        if (visible)
        {
            foreach (Renderer r in _hiddenRenderers)
            {
                if (r != null)
                    r.enabled = true;
            }

            _hiddenRenderers.Clear();
            return;
        }

        foreach (Renderer r in _ship.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled)
                continue;

            r.enabled = false;
            _hiddenRenderers.Add(r);
        }
    }

    private IEnumerator FlashTo(float alpha, float seconds)
    {
        _flash.gameObject.SetActive(true);

        float from = _flash.color.a;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            SetFlashAlpha(Mathf.Lerp(from, alpha, t / seconds));
            yield return null;
        }

        SetFlashAlpha(alpha);
        _flash.gameObject.SetActive(alpha > 0f);
    }

    private void SetFlashAlpha(float alpha)
    {
        Color c = Color.white;
        c.a = alpha;
        _flash.color = c;
    }
}
