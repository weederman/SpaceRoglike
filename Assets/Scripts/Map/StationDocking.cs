using System.Collections;
using UnityEngine;

/// <summary>
/// 우주정거장 도킹/출항 (EVE Online처럼).
///  - 입항: 함선이 정거장 선체에서 dockRange 안으로 다가가면 화면 아래에 "F 입항" 안내가 뜨고, F를 누르면 화면이 어두워지며
///    정거장 안(내부 공간)으로 들어간다. 내부 공간은 바깥 우주와 떨어진 먼 곳이다. 도킹해 있는 동안 정거장 화면(사이드바:
///    격납고·스페이스 라운지·지도·미션·정보)이 보이고 자동 저장(SaveSystem)이 된다. 함선은 제자리에서 움직이거나 쏘지 않고,
///    창이 아닌 곳을 마우스로 끌면 카메라가 함선 둘레를 돌아 둘러볼 수 있다(CameraController.OrbitEnabled).
///  - 출항: 정거장 화면의 창이 모두 닫혀 있을 때(창은 Esc로 닫는다) F를 누르면, 화면이 어두워진 뒤 정거장 앞쪽 바깥에서 밀려 나가며 시작한다.
///  - 스토리 재생 중, 워프 중, 임무가 막는 동안(호송·소탕 교전)에는 입항할 수 없다.
/// F는 채굴 키와 같지만, 입항 안내는 정거장 바로 곁에서만 뜨므로 겹치지 않는다. Shift+F(개발자 창)는 무시한다.
/// StarSystem 프리팹에 함께 붙는다. 내부 화면 UI: Assets/Resources/UI/StationInterior.prefab
/// </summary>
public class StationDocking : MonoBehaviour
{
    public static StationDocking Instance { get; private set; }

    /// <summary>지금 정거장 안에 도킹해 있는지 (도킹/출항 연출 중 포함)</summary>
    public static bool IsDocked => Instance != null && (Instance._docked != null || Instance._busy);

    [Header("도킹")]
    [Tooltip("정거장 선체(모델 범위)에서 이 거리(XZ) 안으로 들어오면 입항할 수 있다")]
    [SerializeField, Min(0f)] private float _dockRange = 1f;
    [Tooltip("입항·출항 키")]
    [SerializeField] private KeyCode _dockKey = KeyCode.F;
    [SerializeField, Min(0.05f)] private float _fadeSeconds = 0.4f;

    [Header("정거장 내부")]
    [Tooltip("내부 공간 위치 (XZ). 바깥 우주·워프 무대와 겹치지 않는 먼 곳")]
    [SerializeField] private Vector2 _interiorXZ = new Vector2(0f, -100000f);
    [SerializeField] private StationInteriorView _viewPrefab;

    [Header("출항")]
    [Tooltip("정거장 앞쪽 끝에서 이만큼 더 나간 곳에 나타난다")]
    [SerializeField, Min(0f)] private float _undockClearance = 120f;
    [Tooltip("출항할 때 밀려 나가는 속도")]
    [SerializeField, Min(0f)] private float _undockSpeed = 40f;

    private StationInteriorView _view;
    private SpaceshipController _ship;
    private Rigidbody _rb;
    private CameraController _cameraController;
    private InfiniteSpaceGrid _grid;

    private StationZone _docked;
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

        if (_viewPrefab != null)
        {
            _view = Instantiate(_viewPrefab, transform);
            _view.SetVisible(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        GameInputLock.Unlock(this);
    }

    // ── 입항 / 출항 키 ────────────────────────────────

    private void Update()
    {
        if (_ship == null || _view == null)
            return;

        if (_busy)
        {
            _view.ShowPrompt(null);
            return;
        }

        if (_docked != null)
        {
            // 정거장 화면의 창이 모두 닫혀 있을 때만 출항
            bool canUndock = !_view.AnyWindowOpen && !StoryPlayer.IsPlaying;
            _view.ShowPrompt(canUndock ? $"{_dockKey}   출항" : null);
            if (canUndock && DockKeyPressed())
                Undock();
            return;
        }

        StationZone near = NearestStation(out float distance);

        // 호송 중이나 소탕 교전 중에는 정거장 곁에서 싸워도 입항하지 않는다
        bool canDock = near != null && distance <= _dockRange
                       && !StoryPlayer.IsPlaying && !StarSystemWarp.IsWarping && !MissionDirector.BlocksTravel
                       && !GameInputLock.IsLockedExcept(this);
        _view.ShowPrompt(canDock ? $"{_dockKey}   입항 · {near.DisplayName}" : null);

        if (canDock && DockKeyPressed())
            StartCoroutine(DockRoutine(near));
    }

    private bool DockKeyPressed()
    {
        // Shift+F는 개발자 창
        return Input.GetKeyDown(_dockKey) && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift);
    }

    /// <summary>함선에서 가장 가까운 정거장과, 그 정거장 선체(모델 범위)까지의 수평 거리</summary>
    private StationZone NearestStation(out float distance)
    {
        distance = float.PositiveInfinity;
        StationZone best = null;
        Vector3 p = _ship.transform.position;

        foreach (StationZone zone in StationZone.All)
        {
            if (!zone.TryGetBounds(out Bounds b))
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
        {
            // 함선은 제자리에 두고, 마우스로 끌어 카메라를 돌려 둘러본다
            _cameraController.OrbitEnabled = true;
            _cameraController.SnapToTarget();
        }

        if (_grid != null) _grid.gameObject.SetActive(false);

        if (_view != null)
        {
            _view.SetStation(zone);
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
        if (_grid != null) _grid.gameObject.SetActive(true);

        // 정거장 앞쪽 끝 바깥에 놓고 앞으로 밀어낸다
        Vector3 dir = zone.Model != null ? zone.Model.forward : Vector3.forward;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;

        float y = StarSystem.Instance != null ? StarSystem.Instance.Origin.y : _ship.transform.position.y;
        Vector3 pos = zone.PointOutside(dir, _dockRange + _undockClearance, y);
        Quaternion rot = Quaternion.LookRotation(dir);
        _ship.transform.SetPositionAndRotation(pos, rot);
        _rb.position = pos;
        _rb.rotation = rot;
        _rb.isKinematic = false;
        _rb.velocity = dir * _undockSpeed;
        _rb.angularVelocity = Vector3.zero;
        _ship.enabled = true;
        if (_cameraController != null)
        {
            _cameraController.OrbitEnabled = false;
            _cameraController.SnapToTarget();
        }

        GameInputLock.Unlock(this);
        yield return Fade(1f, 0f);

        _busy = false;
    }

    // ── 연출 ──────────────────────────────────────────

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
