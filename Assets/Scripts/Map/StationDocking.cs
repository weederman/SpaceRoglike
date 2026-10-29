using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 우주정거장 도킹/출항 (EVE Online처럼).
///  - 도킹: 함선이 정거장 선체에서 dockRange(기본 10) 안으로 다가가면, 화면이 어두워지며 정거장 안(내부 공간)으로 들어간다.
///    내부 공간은 바깥 우주와 떨어진 먼 곳이며, 격납고 그림을 배경으로 함선이 서 있다. 도킹해 있는 동안 정비·상점(StationShop)이 열리고
///    자동 저장(SaveSystem)이 된다. 함선은 움직이거나 쏘지 않는다.
///  - 출항: 내부 화면의 "출항" 버튼 → 화면이 어두워진 뒤 정거장 바깥, 정거장 앞쪽으로 밀려 나가며 시작한다.
///    출항한 정거장은 한 번 멀어진 뒤에야 다시 도킹된다 (나오자마자 다시 들어가지 않게).
///  - 스토리 재생 중이나 워프 중에는 도킹하지 않는다 (튜토리얼은 대사로 정거장을 다룬다. 튜토리얼이 끝나 정거장 곁에 있으면 그때 도킹).
/// StarSystem 프리팹에 함께 붙는다. 내부 화면 UI: Assets/Resources/UI/StationInterior.prefab
/// </summary>
public class StationDocking : MonoBehaviour
{
    public static StationDocking Instance { get; private set; }

    /// <summary>지금 정거장 안에 도킹해 있는지 (도킹/출항 연출 중 포함)</summary>
    public static bool IsDocked => Instance != null && (Instance._docked != null || Instance._busy);

    [Header("도킹")]
    [Tooltip("정거장 선체(모델 범위)에서 이 거리(XZ) 안으로 들어오면 도킹한다")]
    [SerializeField, Min(0f)] private float _dockRange = 10f;
    [Tooltip("출항한 정거장은 이만큼(도킹 거리 + 이 값) 멀어진 뒤에야 다시 도킹된다")]
    [SerializeField, Min(0f)] private float _redockDistance = 80f;
    [SerializeField, Min(0.05f)] private float _fadeSeconds = 0.4f;

    [Header("정거장 내부")]
    [Tooltip("내부 공간 위치 (XZ). 바깥 우주·워프 무대와 겹치지 않는 먼 곳")]
    [SerializeField] private Vector2 _interiorXZ = new Vector2(0f, -100000f);
    [Tooltip("내부 배경 (격납고)")]
    [SerializeField] private Sprite _interiorBackdrop;
    [Tooltip("배경을 함선 아래로 얼마나 깊이 둘지와 크기")]
    [SerializeField, Min(10f)] private float _backdropDepth = 600f;
    [SerializeField, Min(10f)] private float _backdropSize = 2200f;
    [SerializeField] private StationInteriorView _viewPrefab;

    [Header("출항")]
    [Tooltip("정거장 앞쪽 끝에서 이만큼 더 나간 곳에 나타난다")]
    [SerializeField, Min(0f)] private float _undockClearance = 120f;
    [Tooltip("출항할 때 밀려 나가는 속도")]
    [SerializeField, Min(0f)] private float _undockSpeed = 40f;

    private readonly Dictionary<StationZone, Bounds> _stationBounds = new Dictionary<StationZone, Bounds>();
    private StationInteriorView _view;
    private SpriteRenderer _backdrop;
    private SpaceshipController _ship;
    private Rigidbody _rb;
    private CameraController _cameraController;
    private InfiniteSpaceGrid _grid;
    private Camera _cam;

    private StationZone _docked;
    private StationZone _justUndocked;
    private bool _busy;

    // ── 시작 ──────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        _ship = FindObjectOfType<SpaceshipController>();
        _rb = _ship != null ? _ship.GetComponent<Rigidbody>() : null;
        _cameraController = FindObjectOfType<CameraController>();
        _grid = FindObjectOfType<InfiniteSpaceGrid>(true);
        _cam = Camera.main;

        if (_viewPrefab != null)
        {
            _view = Instantiate(_viewPrefab, transform);
            _view.UndockButton.onClick.AddListener(Undock);
            _view.SetVisible(false);
        }

        var backdropGo = new GameObject("InteriorBackdrop");
        backdropGo.transform.SetParent(transform, false);
        _backdrop = backdropGo.AddComponent<SpriteRenderer>();
        _backdrop.sprite = _interiorBackdrop;
        _backdrop.sortingOrder = -400; // 함선보다 뒤
        _backdrop.enabled = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        GameInputLock.Unlock(this);
    }

    // ── 도킹 판정 ─────────────────────────────────────

    private void Update()
    {
        if (_ship == null || _busy || _docked != null)
            return;

        StationZone near = NearestStation(out float distance);

        // 출항한 정거장에서 충분히 멀어지면 다시 도킹할 수 있다
        if (_justUndocked != null && (near != _justUndocked || distance > _dockRange + _redockDistance))
            _justUndocked = null;

        if (near == null || near == _justUndocked || distance > _dockRange)
            return;

        if (StoryPlayer.IsPlaying || StarSystemWarp.IsWarping || GameInputLock.IsLockedExcept(this))
            return;

        StartCoroutine(DockRoutine(near));
    }

    /// <summary>함선에서 가장 가까운 정거장과, 그 정거장 선체(모델 범위)까지의 수평 거리</summary>
    private StationZone NearestStation(out float distance)
    {
        distance = float.PositiveInfinity;
        StationZone best = null;
        Vector3 p = _ship.transform.position;

        foreach (StationZone zone in StationZone.All)
        {
            if (!TryGetBounds(zone, out Bounds b))
                continue;

            float dx = Mathf.Max(b.min.x - p.x, 0f, p.x - b.max.x);
            float dz = Mathf.Max(b.min.z - p.z, 0f, p.z - b.max.z);
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d < distance)
            {
                distance = d;
                best = zone;
            }
        }

        return best;
    }

    private bool TryGetBounds(StationZone zone, out Bounds bounds)
    {
        if (_stationBounds.TryGetValue(zone, out bounds))
            return true;

        if (zone == null || zone.Model == null)
            return false;

        bool any = false;
        foreach (Renderer r in zone.Model.GetComponentsInChildren<Renderer>(true))
        {
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }

        if (any)
            _stationBounds[zone] = bounds;
        return any;
    }

    // ── 도킹 / 출항 ───────────────────────────────────

    /// <summary>연출 없이 바로 그 정거장 안에 도킹한다 (저장을 불러와 이어 할 때)</summary>
    public void DockImmediately(StationZone zone)
    {
        if (zone == null || _ship == null)
            return;

        StopAllCoroutines();
        _busy = false;
        EnterInterior(zone);
    }

    private IEnumerator DockRoutine(StationZone zone)
    {
        _busy = true;
        GameInputLock.Lock(this);

        yield return Fade(0f, 1f);
        EnterInterior(zone);
        GameInputLock.Unlock(this);
        yield return Fade(1f, 0f);

        _busy = false;
    }

    /// <summary>함선을 내부 공간에 세우고 도킹 상태로 만든다</summary>
    private void EnterInterior(StationZone zone)
    {
        _docked = zone;

        // 함선은 움직이지 않는다
        _ship.enabled = false;
        if (!_rb.isKinematic)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        _rb.isKinematic = true;

        var pos = new Vector3(_interiorXZ.x, _ship.transform.position.y, _interiorXZ.y);
        _ship.transform.SetPositionAndRotation(pos, Quaternion.identity);
        _rb.position = pos;
        _rb.rotation = Quaternion.identity;
        if (_cameraController != null)
            _cameraController.SnapToTarget();

        if (_grid != null) _grid.gameObject.SetActive(false);
        _backdrop.enabled = _interiorBackdrop != null;

        if (_view != null)
        {
            _view.Title.text = string.IsNullOrEmpty(zone.DisplayName) ? "정거장" : zone.DisplayName;
            _view.SetVisible(true);
        }

        zone.SetDocked(true);
    }

    public void Undock()
    {
        if (_docked == null || _busy)
            return;

        StartCoroutine(UndockRoutine());
    }

    private IEnumerator UndockRoutine()
    {
        _busy = true;
        GameInputLock.Lock(this);
        StationZone zone = _docked;

        yield return Fade(0f, 1f);

        zone.SetDocked(false);
        _docked = null;
        if (_view != null) _view.SetVisible(false);
        _backdrop.enabled = false;
        if (_grid != null) _grid.gameObject.SetActive(true);

        // 정거장 앞쪽 끝 바깥에 놓고 앞으로 밀어낸다
        Vector3 center = zone.transform.position;
        Vector3 dir = zone.Model != null ? zone.Model.forward : Vector3.forward;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;

        float extent = 0f;
        if (TryGetBounds(zone, out Bounds b))
        {
            center = b.center;
            extent = Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.z) * b.extents.z;
        }

        Vector3 pos = center + dir * (extent + _dockRange + _undockClearance);
        pos.y = StarSystem.Instance != null ? StarSystem.Instance.Origin.y : _ship.transform.position.y;
        Quaternion rot = Quaternion.LookRotation(dir);
        _ship.transform.SetPositionAndRotation(pos, rot);
        _rb.position = pos;
        _rb.rotation = rot;
        _rb.isKinematic = false;
        _rb.velocity = dir * _undockSpeed;
        _rb.angularVelocity = Vector3.zero;
        _ship.enabled = true;
        if (_cameraController != null)
            _cameraController.SnapToTarget();

        _justUndocked = zone;
        GameInputLock.Unlock(this);
        yield return Fade(1f, 0f);

        _busy = false;
    }

    // ── 연출 ──────────────────────────────────────────

    private void LateUpdate()
    {
        // 내부 배경: 카메라 시선 중심의 깊은 곳에, 카메라를 향해 화면을 채우도록 놓는다
        if (!_backdrop.enabled || _cam == null || _ship == null)
            return;

        Vector3 forward = _cam.transform.forward;
        float planeY = _ship.transform.position.y - _backdropDepth;
        Vector3 center = forward.y < -0.05f
            ? _cam.transform.position + forward * ((_cam.transform.position.y - planeY) / -forward.y)
            : _ship.transform.position + Vector3.down * _backdropDepth;

        _backdrop.transform.SetPositionAndRotation(center, _cam.transform.rotation);
        float longSide = Mathf.Max(_interiorBackdrop.bounds.size.x, _interiorBackdrop.bounds.size.y);
        _backdrop.transform.localScale = Vector3.one * (_backdropSize / longSide);
    }

    private IEnumerator Fade(float from, float to)
    {
        if (_view == null)
            yield break;

        for (float t = 0f; t < _fadeSeconds; t += Time.unscaledDeltaTime)
        {
            _view.SetFade(Mathf.Lerp(from, to, t / _fadeSeconds));
            yield return null;
        }

        _view.SetFade(to);
    }
}
