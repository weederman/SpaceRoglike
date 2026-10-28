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
///  s3_warp_go       워프 아웃 이펙트와 함께 함선이 쏘아져 나가 푸른 워프 터널로 들어간다
///  s3_warp_arrive   하얗게 번쩍인 뒤 소행성 지대에 도착한다 (암석과 선박 잔해가 흩어진 곳)
///
/// cue는 큐에 쌓아 한 번에 하나씩 순서대로 실행한다. 레이더 연출(radar_show/hide)은 RadarPing이 따로 받는다.
/// Esc로 건너뛰면 남은 연출을 건너뛰고 곧바로 도착한 뒤의 상태가 된다. 프리팹: Assets/Resources/Directors/Cue/FirstWarpDirector.prefab
/// </summary>
public class FirstWarpDirector : MonoBehaviour
{
    private const string SequenceId = "tutorial_warp";
    private const string CuePrefix = "s3_";

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
    [Tooltip("워프 터널 안에 최소한 머무는 시간(초). 대사를 빨리 넘겨도 이 시간은 터널을 보여준다.")]
    [SerializeField] private float _minTunnelSeconds = 5f;
    [Tooltip("소행성 지대 도착 위치 (XZ). 정거장에서 충분히 먼 곳.")]
    [SerializeField] private Vector2 _arrivalXZ = new Vector2(2600f, 2400f);

    // ── 장면 참조 ──
    private SpaceshipController _ship;
    private Rigidbody _rb;
    private F3DPlayerTurretController _turret;
    private CameraController _cameraController;
    private InfiniteSpaceGrid _grid;
    private Camera _cam;

    // 제어권을 가져가기 전 상태 (프롤로그 연출이 이미 잠가 두었을 수도 있으니 "원래대로"가 아니라 "가져가기 전대로" 돌려준다)
    private bool _shipWasEnabled;
    private bool _turretWasEnabled;
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
    private F3DWarpJump _warpOutJump;
    private AsteroidField _field;

    private void OnEnable()
    {
        StoryPlayer.OnCue += HandleCue;
        StoryPlayer.OnSequenceEnded += HandleEnded;
    }

    private void OnDisable()
    {
        StoryPlayer.OnCue -= HandleCue;
        StoryPlayer.OnSequenceEnded -= HandleEnded;
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
        if (!cue.StartsWith(CuePrefix))
            return;

        _queue.Enqueue(cue);
        if (_pump == null)
            _pump = StartCoroutine(Pump());
    }

    private void HandleEnded(string sequenceId, bool skipped)
    {
        if (sequenceId != SequenceId)
            return;

        if (skipped)
        {
            // 건너뛰기: 진행 중인 연출을 멈추고 도착한 뒤의 최종 상태로 바로 만든다
            StopAllCoroutines();
            _pump = null;
            _queue.Clear();
            FinalizeWorld();
        }
        else
        {
            StartCoroutine(FinishWhenIdle());
        }
    }

    private IEnumerator FinishWhenIdle()
    {
        while (_pump != null || _queue.Count > 0)
            yield return null;

        FinalizeWorld();
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

        Vector3 dir = _departDirection.normalized;
        _warpOut = Instantiate(_warpOutPrefab, _ship.transform.position, Quaternion.LookRotation(dir));
        _warpOutJump = _warpOut.GetComponent<F3DWarpJump>();
        if (_warpOutJump != null)
            _warpOutJump.DebugLoop = false; // 프리팹은 4초마다 반복 재생하도록 되어 있다 (Start 전에 끈다)

        // 이펙트가 충전하다가 함선을 쏘아 보낸다. 충분히 멀어질 때까지 이펙트가 정한 위치를 따라간다.
        float timeout = Time.time + 7f;
        Quaternion rot = Quaternion.LookRotation(dir);
        while (Time.time < timeout && Vector3.Distance(_ship.transform.position, _departEnd) < 110f)
        {
            if (_warpOutJump != null && _warpOutJump.ShipPos != null)
                MoveShip(_warpOutJump.ShipPos.position, rot);

            yield return null;
        }

        // 쏘아져 나가며 사라지고 하얗게 번쩍인 뒤 터널 안으로
        SetShipVisible(false);
        yield return FlashTo(1f, 0.25f);

        if (_warpOut != null) Destroy(_warpOut);
        if (_beacon != null) Destroy(_beacon.gameObject);
        _beacon = null;
        _chargeLight.enabled = false;

        _cameraController.enabled = false;
        if (_grid != null) _grid.gameObject.SetActive(false);
        SetShipVisible(true);
        _stage.Enter(_ship.transform, _rb, _cam);

        yield return FlashTo(0f, 0.7f);
    }

    private IEnumerator StepArrive()
    {
        TakeControl();

        // 대사를 빨리 넘겨도 터널을 달리는 장면이 너무 짧게 지나가지 않도록
        while (_stage.IsActive && _stage.ActiveSeconds < _minTunnelSeconds)
            yield return null;

        yield return FlashTo(1f, 0.4f);

        LeaveTunnel();
        PlaceAtArrival();

        yield return FlashTo(0f, 1.0f);
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
        _turret = FindObjectOfType<F3DPlayerTurretController>();
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
        _turretWasEnabled = _turret != null && _turret.enabled;
        _rbWasKinematic = _rb.isKinematic;

        _ship.enabled = false;
        if (_turret != null) _turret.enabled = false;

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
            if (_turret != null) _turret.enabled = _turretWasEnabled;
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
