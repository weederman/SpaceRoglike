using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 항성계 안의 장소(정거장/워프 비콘)로 워프한다. 궤도 사이 이동은 이것으로만 한다.
/// 함선 자리에 워프 게이트가 열리고 충전되는 동안 함선이 감속해 멈췄다가, 가속도가 점점 커지며 돌진(WarpDash) →
/// 멈추지 않고 하얀 섬광 → 워프 터널(WarpTunnelStage, 거리에 따라 길게, 워프 엔진음 반복) → 섬광 → 도착 지점에 함선을 놓는다.
/// 워프하는 동안 조작은 잠기고(GameInputLock) 카메라·격자는 터널 무대가 대신 다룬다.
/// 스토리 재생 중이나 임무가 막는 동안(호송·소탕 교전)에는 워프할 수 없다. StarSystem 프리팹에 함께 붙는다. 출발은 항성계 지도(StarMapUI)에서 한다.
/// </summary>
public class StarSystemWarp : MonoBehaviour
{
    [SerializeField] private StarSystem _system;
    [SerializeField] private WarpTunnelStage _stage;
    [Tooltip("워프 게이트 이펙트 (첫 워프와 같은 워프 아웃)")]
    [SerializeField] private GameObject _gatePrefab;

    [Header("돌진")]
    [Tooltip("게이트가 충전된 뒤 돌진하는 거리")]
    [SerializeField, Min(10f)] private float _gateDistance = 260f;
    [Tooltip("돌진하는 시간(초). 짧을수록 세게 가속한다.")]
    [SerializeField, Min(0.2f)] private float _dashSeconds = 1.2f;

    [Header("시간 (초)")]
    [SerializeField, Min(0.05f)] private float _flashSeconds = 0.25f;
    [Tooltip("터널 안에 머무는 시간: 가장 가까운 곳 ~ 가장 먼 곳")]
    [SerializeField] private Vector2 _tunnelSeconds = new Vector2(2.5f, 5f);

    /// <summary>워프를 시작했을 때 / 도착했을 때 (도착한 장소)</summary>
    public event Action OnWarpStarted;
    public event Action<StarSystem.Location> OnWarpArrived;

    public static bool IsWarping { get; private set; }

    // SoundTable 이름
    private const string InWarpSound = "inwarp";
    private const string WarpStartSound = "warp_start";
    private const string WarpEndSound = "warp_end";

    /// <summary>섬광에 쓸 화면 전체 이미지 (항성계 지도 프리팹이 넘겨준다)</summary>
    public Image Flash { get; set; }

    private SpaceshipController _ship;
    private Rigidbody _rb;
    private CameraController _cameraController;
    private InfiniteSpaceGrid _grid;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsWarping = false;
    }

    /// <summary>지금 워프를 시작할 수 있는지 (스토리 재생 중, 이미 워프 중, 다른 창이 조작을 잠근 중이면 불가)</summary>
    public bool CanWarp(object requester)
    {
        // 호송 중이거나 소탕 교전 중에는 떠날 수 없다
        return !IsWarping && !StoryPlayer.IsPlaying && !StationDocking.IsDocked && !MissionDirector.BlocksTravel
               && !GameInputLock.IsLockedExcept(requester);
    }

    public bool WarpTo(StarSystem.Location destination)
    {
        if (IsWarping || destination.IsNone || _system == null)
            return false;

        StartCoroutine(WarpRoutine(destination));
        return true;
    }

    private IEnumerator WarpRoutine(StarSystem.Location destination)
    {
        IsWarping = true;
        GameInputLock.Lock(this);
        EnsureRefs();
        OnWarpStarted?.Invoke();

        float distance = Vector3.Distance(_ship.transform.position, _system.LocationWorld(destination));
        float tunnelSeconds = Mathf.Lerp(_tunnelSeconds.x, _tunnelSeconds.y,
            Mathf.InverseLerp(0f, _system.Data.OuterRadius * 2f, distance));
        float baseY = _system.Origin.y;

        // 조종을 가져간다. 날던 속도는 기억해 두었다가 게이트가 충전되는 동안 서서히 줄인다.
        bool wasEnabled = _ship.enabled;
        _ship.enabled = false;
        Vector3 velocity = Vector3.zero;
        if (!_rb.isKinematic)
        {
            velocity = _rb.velocity;
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        _rb.isKinematic = true;

        // 함선 자리에 게이트를 열고 감속해 멈춤 → 점점 세게 가속하며 돌진 → 멈추지 않고 섬광 → 터널
        Vector3 start = _ship.transform.position;
        Vector3 dir = _ship.transform.forward;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
        Quaternion rot = Quaternion.LookRotation(dir);
        Action<Vector3> move = p =>
        {
            _ship.transform.SetPositionAndRotation(p, rot);
            _rb.position = p;
            _rb.rotation = rot;
        };

        GameObject gate = WarpDash.SpawnGate(_gatePrefab, start, dir);
        GameAudio.Play(WarpStartSound);
        yield return WarpDash.Launch(start, velocity, WarpDash.GateChargeSeconds, dir, _gateDistance, _dashSeconds, move);

        Coroutine coast = StartCoroutine(WarpDash.Coast(WarpDash.LaunchEnd(start, velocity, WarpDash.GateChargeSeconds, dir, _gateDistance),
            dir, WarpDash.FinalSpeed(_gateDistance, _dashSeconds), move));
        yield return FadeFlash(0f, 1f);
        StopCoroutine(coast);

        if (gate != null) Destroy(gate);
        if (_cameraController != null) _cameraController.enabled = false;
        if (_grid != null) _grid.gameObject.SetActive(false);
        _stage.Enter(_ship.transform, _rb, Camera.main);
        GameAudio.StartLoop(InWarpSound, this);
        yield return FadeFlash(1f, 0f);

        yield return new WaitForSeconds(tunnelSeconds);

        // 섬광 → 도착
        yield return FadeFlash(0f, 1f);
        GameAudio.StopLoop(this);
        GameAudio.Play(WarpEndSound);
        _stage.Exit();

        Vector3 arrival = _system.ArrivalPoint(destination);
        arrival.y = baseY;
        _ship.transform.SetPositionAndRotation(arrival, Quaternion.identity);
        _rb.position = arrival;
        _rb.rotation = Quaternion.identity;

        if (_grid != null) _grid.gameObject.SetActive(true);
        if (_cameraController != null)
        {
            _cameraController.enabled = true;
            _cameraController.SnapToTarget();
        }

        _rb.isKinematic = false;
        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _ship.enabled = wasEnabled;

        yield return FadeFlash(1f, 0f);

        GameInputLock.Unlock(this);
        IsWarping = false;
        OnWarpArrived?.Invoke(destination);
    }

    private IEnumerator FadeFlash(float from, float to)
    {
        if (Flash == null)
            yield break;

        Flash.gameObject.SetActive(true);
        for (float t = 0f; t < _flashSeconds; t += Time.unscaledDeltaTime)
        {
            SetFlash(Mathf.Lerp(from, to, t / _flashSeconds));
            yield return null;
        }

        SetFlash(to);
        if (to <= 0f)
            Flash.gameObject.SetActive(false);
    }

    private void SetFlash(float alpha)
    {
        Color c = Flash.color;
        c.a = alpha;
        Flash.color = c;
    }

    private void EnsureRefs()
    {
        if (_system == null)
            _system = GetComponent<StarSystem>();

        if (_ship != null)
            return;

        _ship = FindObjectOfType<SpaceshipController>();
        _rb = _ship.GetComponent<Rigidbody>();
        _cameraController = FindObjectOfType<CameraController>();
        _grid = FindObjectOfType<InfiniteSpaceGrid>(true);
    }

    private void OnDestroy()
    {
        if (IsWarping)
        {
            GameInputLock.Unlock(this);
            IsWarping = false;
        }

        GameAudio.StopLoop(this);
    }
}
