using System;
using UnityEngine;

/// <summary>
/// 게임 월드에 항성계 맵(StarSystemData)을 펼친다.
///  - 씬에 놓인 정거장(StationZone.Home)이 데이터의 모항 정거장 자리에 오도록 항성(원점)의 월드 위치를 정한다.
///    그래서 씬은 그대로 두고, 나머지 정거장 9곳은 런타임에 만든다.
///  - 정거장 10곳 모두 Trading Outpost 3D 모델(프리팹에 정한 크기 × stationModelScale)로, 함선이 다니는 높이에 놓인다.
///    함선이 다가가 일정 거리 안에 들어오면 정거장 안으로 도킹한다(StationDocking).
///  - 플레이어가 어떤 장소(정거장/워프 비콘) 가까이에 있는지 판정해 "지역"으로 알린다(OnAreaChanged).
///    소행성 벨트의 비콘 지역에 들어오면 비콘(레벨 색)과 소행성 지대를 깔고, 벗어나면 치운다.
///  - 궤도 사이 이동은 워프로만 한다(StarSystemWarp). 지역 안에서만 직접 비행한다.
/// 게임 씬(플레이어 함선 + 모항 정거장)이 있으면 자동으로 생긴다. 프리팹: Assets/Resources/Directors/StarSystem.prefab
/// </summary>
public class StarSystem : MonoBehaviour
{
    /// <summary>
    /// 정거장 충돌체의 레이어 (Ignore Raycast). 함선 몸통만 막고, 무기 Raycast는 HitResolver가 이 레이어를 건너뛰어 탄은 지나간다.
    /// 키네마틱인 화물선은 막히지 않아 그대로 정거장 안으로 들어가 사라진다.
    /// </summary>
    public const int StationLayer = 2;

    private const string PrefabPath = "Directors/StarSystem";
    private const float AreaCheckInterval = 0.25f;

    public enum LocationKind
    {
        None,
        Station,
        Beacon
    }

    /// <summary>항성계 안의 장소 하나 (정거장 또는 워프 비콘). index는 데이터 배열의 번호.</summary>
    public readonly struct Location : IEquatable<Location>
    {
        public static readonly Location None = new Location(LocationKind.None, -1);

        public readonly LocationKind Kind;
        public readonly int Index;

        public Location(LocationKind kind, int index)
        {
            Kind = kind;
            Index = index;
        }

        public bool IsNone => Kind == LocationKind.None;
        public bool Equals(Location other) => Kind == other.Kind && Index == other.Index;
        public override bool Equals(object obj) => obj is Location other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ Index;
        public static bool operator ==(Location a, Location b) => a.Equals(b);
        public static bool operator !=(Location a, Location b) => !a.Equals(b);
    }

    public static StarSystem Instance { get; private set; }

    /// <summary>플레이어가 있는 지역이 바뀌었을 때 (지역 밖으로 나가면 Location.None)</summary>
    public static event Action<Location> OnAreaChanged;

    [SerializeField] private StarSystemData _data;

    [Header("지역")]
    [Tooltip("장소에서 이 거리(XZ) 안이면 그 장소의 지역에 있는 것으로 본다 (정거장은 선체 끝에서 잰다)")]
    [SerializeField, Min(100f)] private float _areaRadius = 1600f;
    [Tooltip("지역을 벗어났다고 보는 거리 (들어온 거리보다 조금 크게 두어 경계에서 깜빡이지 않게)")]
    [SerializeField, Min(100f)] private float _areaExitRadius = 1800f;

    [Header("도착 위치 (장소 기준 XZ)")]
    [Tooltip("정거장으로 워프하면 정거장의 이쪽 방향(XZ)에 도착한다")]
    [SerializeField] private Vector2 _stationArrivalDirection = new Vector2(0f, -1f);
    [Tooltip("정거장 실제 선체 끝(위에서 내려다본 모양)에서 이만큼 떨어진 곳에 도착한다. 기본 줌에서 화면 위아래로 약 65가 보인다")]
    [SerializeField, Min(0f)] private float _stationArrivalClearance = 900f;
    [Tooltip("비콘이 화면 오른쪽 위에 보이도록 비콘의 왼쪽 아래에 도착한다")]
    [SerializeField] private Vector2 _beaconArrivalOffset = new Vector2(-150f, -90f);

    [Header("정거장")]
    [Tooltip("정거장 모델 (Trading Outpost). 프리팹에 정한 크기에 아래 배율을 곱해 쓴다.")]
    [SerializeField] private GameObject _stationModel;
    [Tooltip("정거장 모델 배율 (프리팹 크기 × 이 값)")]
    [SerializeField, Min(0.01f)] private float _stationModelScale = 20f;
    [Tooltip("정거장 선체 모양(위에서 본 모양)에 넣는 높이 범위 (함선 평면 기준 아래, 위). 이 범위의 선체만 함선을 막고 도킹 거리를 잰다. " +
             "카메라보다 높이 솟은 부분은 화면에 보이지 않으므로 빼야 보이지 않는 벽이 생기지 않는다")]
    [SerializeField] private Vector2 _stationHullBand = new Vector2(-250f, 150f);

    [Header("비콘 지역")]
    [SerializeField] private WarpBeacon _beaconPrefab;
    [SerializeField] private AsteroidField _asteroidFieldPrefab;

    [Header("워프 / 지도")]
    [SerializeField] private StarSystemWarp _warp;
    [Tooltip("항성계 지도 창 (Assets/Resources/UI/StarMap.prefab)")]
    [SerializeField] private StarMapUI _mapPrefab;

    public StarSystemData Data => _data;
    public StarSystemWarp Warp => _warp;
    public Transform Player => _player;

    /// <summary>항성(항성계 원점)의 월드 위치. Y는 함선이 다니는 높이.</summary>
    public Vector3 Origin { get; private set; }

    public Location CurrentArea { get; private set; } = Location.None;

    private Transform _player;
    private float _stationY;
    private float _nextAreaCheck;

    private WarpBeacon _areaBeacon;
    private AsteroidField _areaField; // 이 지역에 들어와서 우리가 깐 소행성 지대 (튜토리얼이 깐 것은 건드리지 않는다)

    // ── 시작 ──────────────────────────────────────────

    // 게임을 켤 때뿐 아니라 씬을 다시 불러올 때(패배 후 재시작 등)도 다시 만든다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        Bootstrap();
    }

    private static void Bootstrap()
    {
        // 플레이어 함선과 모항 정거장이 있는 게임 씬에서만
        if (Instance != null || FindObjectOfType<SpaceshipController>() == null || StationZone.Home == null)
            return;

        var prefab = Resources.Load<StarSystem>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[StarSystem] Assets/Resources/{PrefabPath}.prefab을 찾지 못해 항성계 맵 없이 시작합니다.");
            return;
        }

        Instantiate(prefab);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        OnAreaChanged = null;
    }

    private void Awake()
    {
        if (_data == null)
            _data = Resources.Load<StarSystemData>(StarSystemData.ResourcePath);

        StationZone home = StationZone.Home;
        if (_data == null || _data.stations.Length == 0 || home == null)
        {
            Debug.LogWarning("[StarSystem] 항성계 데이터(Resources/StarSystem.asset) 또는 모항 정거장이 없어 맵을 펼치지 않습니다.");
            enabled = false;
            return;
        }

        Instance = this;

        var ship = FindObjectOfType<SpaceshipController>();
        _player = ship.transform;

        // 모항 정거장이 데이터의 모항 자리에 오도록 항성의 월드 위치를 정한다
        Vector2 homeLocal = _data.StationPosition(_data.homeStation);
        Vector3 homeWorld = home.transform.position;
        _stationY = homeWorld.y;
        Origin = new Vector3(homeWorld.x - homeLocal.x, _player.position.y, homeWorld.z - homeLocal.y);

        home.DisplayName = _data.stations[_data.homeStation].name;
        home.StationIndex = _data.homeStation;
        CreateStations();

        if (_mapPrefab != null && _warp != null)
            Instantiate(_mapPrefab, transform).Init(this, _warp);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void CreateStations()
    {
        for (int i = 0; i < _data.stations.Length; i++)
        {
            StarSystemData.Station s = _data.stations[i];
            Vector3 pos = StationWorld(i);

            var go = new GameObject($"Station_{s.name}");
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            Transform model = CreateStationModel(go.transform, s.angle);

            StationZone zone;
            if (i == _data.homeStation)
            {
                zone = StationZone.Home; // 모항은 씬의 정거장을 쓴다
            }
            else
            {
                zone = go.AddComponent<StationZone>();
                zone.Init(s.name);
                zone.StationIndex = i;
            }

            if (zone != null)
            {
                zone.HullBand = _stationHullBand;
                zone.Model = model;
                zone.BuildHullColliders(StationLayer);
            }
        }
    }

    /// <summary>정거장 모델을 함선이 다니는 높이에 놓는다 (프리팹 크기 × 배율, 모델 자체의 판정은 없앤다). 모델이 없으면 null.</summary>
    private Transform CreateStationModel(Transform station, float yaw)
    {
        if (_stationModel == null)
            return null;

        GameObject model = Instantiate(_stationModel, station);
        model.name = "Model";
        model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        model.transform.localScale = _stationModel.transform.localScale * _stationModelScale;
        model.transform.position = new Vector3(station.position.x, Origin.y, station.position.z);

        // 모델의 판정은 지운다. 함선을 막는 판정은 StationZone.BuildHullColliders가 위에서 본 선체 모양으로 따로 만든다.
        foreach (Collider c in model.GetComponentsInChildren<Collider>(true))
            Destroy(c);

        return model.transform;
    }

    // ── 좌표 ──────────────────────────────────────────

    /// <summary>항성계 좌표(XZ) → 월드 좌표 (함선 높이)</summary>
    public Vector3 ToWorld(Vector2 local)
    {
        return new Vector3(Origin.x + local.x, Origin.y, Origin.z + local.y);
    }

    /// <summary>월드 좌표 → 항성계 좌표(XZ)</summary>
    public Vector2 ToSystem(Vector3 world)
    {
        return new Vector2(world.x - Origin.x, world.z - Origin.z);
    }

    /// <summary>정거장 구역의 월드 위치 (모항은 씬의 정거장 위치)</summary>
    public Vector3 StationWorld(int index)
    {
        Vector3 p = ToWorld(_data.StationPosition(index));
        p.y = _stationY;
        return p;
    }

    public Vector3 BeaconWorld(int index)
    {
        return ToWorld(_data.BeaconPosition(index));
    }

    public Vector3 LocationWorld(Location location)
    {
        switch (location.Kind)
        {
            case LocationKind.Station: return StationWorld(location.Index);
            case LocationKind.Beacon: return BeaconWorld(location.Index);
            default: return Origin;
        }
    }

    /// <summary>그 장소로 워프했을 때 함선이 나타나는 자리 (함선 높이). 정거장이면 선체 바깥으로 _stationArrivalClearance만큼 떨어진 곳.</summary>
    public Vector3 ArrivalPoint(Location location)
    {
        if (location.Kind == LocationKind.Station)
            return StationArrival(FindStation(location.Index));

        Vector3 p = LocationWorld(location);
        return new Vector3(p.x + _beaconArrivalOffset.x, Origin.y, p.z + _beaconArrivalOffset.y);
    }

    /// <summary>정거장 앞(선체 바깥, 워프 도착과 같은 쪽)에 나타나는 자리 (함선 높이). 튜토리얼 귀환도 이 자리를 쓴다.</summary>
    public Vector3 StationArrival(StationZone zone)
    {
        return StationApproach(zone, _stationArrivalClearance);
    }

    /// <summary>
    /// 워프 도착과 같은 쪽으로 정거장 선체 끝에서 clearance만큼 떨어진 자리 (함선 높이).
    /// fromHull이면 실제 선체 모양에서, 아니면 모델 범위 상자 끝에서 잰다(StationZone.PointOutside).
    /// </summary>
    public Vector3 StationApproach(StationZone zone, float clearance, bool fromHull = true)
    {
        Vector3 dir = new Vector3(_stationArrivalDirection.x, 0f, _stationArrivalDirection.y);
        if (zone == null)
            return Origin;
        return zone.PointOutside(dir, clearance, Origin.y, fromHull);
    }

    /// <summary>항성계 데이터의 정거장 번호로 정거장을 찾는다 (없으면 null)</summary>
    public static StationZone FindStation(int index)
    {
        foreach (StationZone zone in StationZone.All)
        {
            if (zone.StationIndex == index)
                return zone;
        }

        return null;
    }

    /// <summary>튜토리얼 첫 워프(장면 3)가 도착하는 자리: 모항 옆 벨트의 레벨 1 비콘</summary>
    public bool TryGetTutorialArrival(out Vector3 position)
    {
        position = Vector3.zero;
        if (_data == null || _data.tutorialBeacon < 0 || _data.tutorialBeacon >= _data.beacons.Length)
            return false;

        position = ArrivalPoint(new Location(LocationKind.Beacon, _data.tutorialBeacon));
        return true;
    }

    public string LocationName(Location location)
    {
        switch (location.Kind)
        {
            case LocationKind.Station: return _data.stations[location.Index].name;
            case LocationKind.Beacon: return _data.beacons[location.Index].name;
            default: return "";
        }
    }

    /// <summary>플레이어가 항성계 안(바깥 궤도 조금 밖까지)에 있는지. 워프 터널 무대는 항성계 밖 먼 곳에 있다.</summary>
    public bool IsInsideSystem(Vector3 world)
    {
        return ToSystem(world).magnitude <= _data.OuterRadius + _areaExitRadius * 2f;
    }

    // ── 지역 ──────────────────────────────────────────

    private void Update()
    {
        OreDeposits.Tick();

        if (_player == null || Time.unscaledTime < _nextAreaCheck)
            return;

        _nextAreaCheck = Time.unscaledTime + AreaCheckInterval;

        Location area = FindArea(_player.position);
        if (area != CurrentArea)
            ChangeArea(area);
    }

    private Location FindArea(Vector3 world)
    {
        // 지금 지역은 벗어나는 거리를 조금 더 넉넉히 본다
        if (!CurrentArea.IsNone && DistanceTo(CurrentArea, world) <= _areaExitRadius)
            return CurrentArea;

        Location best = Location.None;
        float bestDistance = _areaRadius;

        for (int i = 0; i < _data.stations.Length; i++)
            Consider(new Location(LocationKind.Station, i));
        for (int i = 0; i < _data.beacons.Length; i++)
            Consider(new Location(LocationKind.Beacon, i));

        return best;

        void Consider(Location candidate)
        {
            float d = DistanceTo(candidate, world);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = candidate;
            }
        }
    }

    private void ChangeArea(Location area)
    {
        ClearAreaDressing();
        CurrentArea = area;

        if (area.Kind == LocationKind.Beacon)
            DressBeaconArea(area.Index);

        OnAreaChanged?.Invoke(area);
    }

    /// <summary>비콘 지역: 레벨 색의 워프 비콘을 띄우고, 소행성 지대가 없으면 깔고, 광맥(OreDeposits)을 깐다</summary>
    private void DressBeaconArea(int index)
    {
        if (_beaconPrefab != null)
        {
            _areaBeacon = Instantiate(_beaconPrefab, BeaconWorld(index), Quaternion.identity, transform);
            _areaBeacon.name = $"Beacon_{_data.beacons[index].name}";
            _areaBeacon.SetColor(StarSystemData.LevelColor(_data.beacons[index].level));
            _areaBeacon.Show();
        }

        // 튜토리얼(장면 3)이 이미 소행성 지대를 깔았으면 그대로 쓴다
        if (_asteroidFieldPrefab != null && AsteroidField.Active == null)
            _areaField = Instantiate(_asteroidFieldPrefab, _player.position, Quaternion.identity);

        // 광맥: 튜토리얼 진행 중에는 튜토리얼 비콘에 깔지 않는다 (튜토리얼은 자기 광맥을 쓴다)
        if (!(index == _data.tutorialBeacon && TutorialInProgress()))
        {
            Vector3 center = BeaconWorld(index);
            center.y = _player.position.y;
            OreDeposits.Spawn(index, _data.beacons[index].level, center, transform);
        }
    }

    private static bool TutorialInProgress()
    {
        QuestTracker quest = FindObjectOfType<QuestTracker>(true);
        return quest != null && !string.IsNullOrEmpty(quest.CurrentQuestId);
    }

    private void ClearAreaDressing()
    {
        OreDeposits.Clear();

        if (_areaBeacon != null)
            Destroy(_areaBeacon.gameObject);
        _areaBeacon = null;

        if (_areaField != null)
            Destroy(_areaField.gameObject);
        _areaField = null;
    }

    /// <summary>장소까지의 수평 거리. 정거장은 아주 커서 중심이 아니라 선체(모델 범위) 끝에서 잰다.</summary>
    /// <summary>
    /// world가 그 장소의 지역 범위 안인지. 붙어 있는 두 장소(가까운 정거장 둘 등)는 지역 판정이 먼저 들어간 쪽에 머무르므로,
    /// "이 장소에 왔는가"를 볼 때는 CurrentArea와 함께 이것도 본다.
    /// </summary>
    public bool IsNear(Location location, Vector3 world)
    {
        return !location.IsNone && DistanceTo(location, world) <= _areaRadius;
    }

    private float DistanceTo(Location location, Vector3 world)
    {
        if (location.Kind == LocationKind.Station)
        {
            StationZone zone = FindStation(location.Index);
            if (zone != null && zone.TryGetBounds(out Bounds b))
            {
                float dx = Mathf.Max(b.min.x - world.x, 0f, world.x - b.max.x);
                float dz = Mathf.Max(b.min.z - world.z, 0f, world.z - b.max.z);
                return Mathf.Sqrt(dx * dx + dz * dz);
            }
        }

        return HorizontalDistance(LocationWorld(location), world);
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
