using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 항성계 안의 장소(정거장/워프 비콘)로 워프한다. 궤도 사이 이동은 이것으로만 한다.
/// 충전(함선 정지) → 하얀 섬광 → 워프 터널(WarpTunnelStage, 거리에 따라 길게) → 섬광 → 도착 지점에 함선을 놓는다.
/// 워프하는 동안 조작은 잠기고(GameInputLock) 카메라·격자는 터널 무대가 대신 다룬다.
/// 스토리 재생 중에는 워프할 수 없다. StarSystem 프리팹에 함께 붙는다. 출발은 항성계 지도(StarMapUI)에서 한다.
/// </summary>
public class StarSystemWarp : MonoBehaviour
{
    [SerializeField] private StarSystem _system;
    [SerializeField] private WarpTunnelStage _stage;

    [Header("시간 (초)")]
    [SerializeField, Min(0f)] private float _chargeSeconds = 1.2f;
    [SerializeField, Min(0.05f)] private float _flashSeconds = 0.25f;
    [Tooltip("터널 안에 머무는 시간: 가장 가까운 곳 ~ 가장 먼 곳")]
    [SerializeField] private Vector2 _tunnelSeconds = new Vector2(2.5f, 5f);

    /// <summary>워프를 시작했을 때 / 도착했을 때 (도착한 장소)</summary>
    public event Action OnWarpStarted;
    public event Action<StarSystem.Location> OnWarpArrived;

    public static bool IsWarping { get; private set; }

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
        return !IsWarping && !StoryPlayer.IsPlaying && !GameInputLock.IsLockedExcept(requester);
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

        // 충전: 함선을 세운다
        bool wasEnabled = _ship.enabled;
        _ship.enabled = false;
        if (!_rb.isKinematic)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        _rb.isKinematic = true;
        yield return new WaitForSeconds(_chargeSeconds);

        // 섬광 → 터널
        yield return FadeFlash(0f, 1f);
        if (_cameraController != null) _cameraController.enabled = false;
        if (_grid != null) _grid.gameObject.SetActive(false);
        _stage.Enter(_ship.transform, _rb, Camera.main);
        yield return FadeFlash(1f, 0f);

        yield return new WaitForSeconds(tunnelSeconds);

        // 섬광 → 도착
        yield return FadeFlash(0f, 1f);
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
    }
}
