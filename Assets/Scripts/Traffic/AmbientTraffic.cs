using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 주변 교통: 플레이어가 있는 지역(StarSystem.OnAreaChanged)에 맞춰 NPC 함선과 해적을 띄운다.
///  - 정거장 지역: 아군 경비선(AllyShip)이 정거장 입구 앞에 나타나 그 근처를 순찰하고, 민간 화물선(NpcFreighter)이 오가며(도킹하며 사라지거나 워프로 떠난다),
///    가끔 해적이 습격해 온다. 습격한 해적은 가장 가까운 플레이어 편(플레이어·경비선·화물선)을 노리고, 경비선은 해적과 싸운다.
///  - 소행성대(비콘) 지역: 비콘 레벨에 따라 해적이 나타나 플레이어를 노린다.
///  - 지역을 벗어나면(워프 등) 이 지역에 띄운 배를 모두 치운다. 도킹 중에는 함선이 먼 내부 공간에 있어 지역이 바뀐 것으로 보지 않는다.
///  - 해적은 임무 진행 중·튜토리얼 진행 중·스토리 재생 중·도킹 중에는 나오지 않는다. 임무가 시작되거나 도킹하면 띄워 둔 해적은 워프로 물러난다.
/// 수치는 모두 이 프리팹(Assets/Resources/Directors/AmbientTraffic.prefab)에서 정하고, 함종은 EnemyShipCatalog의 id로 고른다.
/// 게임 씬(플레이어 함선 + 모항 정거장)이 있으면 자동으로 생긴다.
/// </summary>
public class AmbientTraffic : MonoBehaviour
{
    private const string PrefabPath = "Directors/AmbientTraffic";
    private const float TickInterval = 0.5f;

    [Serializable]
    public class PirateGroup
    {
        [Tooltip("한 번에 나타나는 척수 (최소, 최대)")]
        public Vector2Int count = new Vector2Int(2, 3);
        [Tooltip("나올 수 있는 함종 (EnemyShipCatalog id). 한 척마다 무작위로 고른다")]
        public string[] classes = { "light_fighter" };
        [Tooltip("앞 무리가 모두 사라진 뒤 다음 무리가 나올 때까지 (초, 최소~최대)")]
        public Vector2 interval = new Vector2(60f, 120f);
    }

    [Header("공통")]
    [Tooltip("해적·화물선이 워프로 떠날 때의 게이트 이펙트")]
    [SerializeField] private GameObject _warpPrefab;
    [Tooltip("이 퀘스트가 진행 중이면 튜토리얼 중으로 보고 해적을 띄우지 않는다")]
    [SerializeField] private string _tutorialQuestId = "frontier_gold";
    [Tooltip("해적이 노릴 대상을 다시 고르는 간격 (초)")]
    [SerializeField, Min(0.2f)] private float _retargetInterval = 2f;

    [Header("정거장: 경비선")]
    [Tooltip("경비선 함종 (EnemyShipCatalog id)")]
    [SerializeField] private string _allyClass = "patrol_corvette";
    [SerializeField, Min(0)] private int _allyCount = 3;
    [Tooltip("정거장 입구 앞에 나타날 때 경비선 사이 간격")]
    [SerializeField, Min(0f)] private float _allySpawnSpacing = 120f;
    [Tooltip("순찰 구역: 입구 쪽 정거장 선체 끝에서 나간 거리 (최소~최대)")]
    [SerializeField] private Vector2 _patrolClearance = new Vector2(250f, 600f);
    [Tooltip("순찰 구역: 입구 중심선에서 옆으로 이 거리 안")]
    [SerializeField, Min(0f)] private float _patrolHalfWidth = 400f;
    [SerializeField, Min(1f)] private float _patrolSpeed = 30f;
    [Tooltip("이 거리 안에 해적이 들어오면 싸운다")]
    [SerializeField, Min(10f)] private float _allyEngageRange = 1200f;
    [Tooltip("정거장 선체에서 이보다 먼 해적은 쫓지 않는다")]
    [SerializeField, Min(10f)] private float _allyLeashDistance = 2000f;

    [Header("정거장: 화물선")]
    [Tooltip("화물선 프리팹 (호송 임무의 EscortFreighter를 그대로 쓴다)")]
    [SerializeField] private GameObject _freighterPrefab;
    [SerializeField] private NpcFreighter.Settings _freighter = new NpcFreighter.Settings();
    [SerializeField, Min(0)] private int _maxFreighters = 2;
    [Tooltip("화물선이 새로 나타나는 간격 (초, 최소~최대)")]
    [SerializeField] private Vector2 _freighterInterval = new Vector2(20f, 45f);
    [Tooltip("화물선이 나타나는 거리 (정거장 선체 끝에서)")]
    [SerializeField, Min(0f)] private float _freighterSpawnClearance = 1300f;
    [Tooltip("화물선이 들르는 지점의 거리 (정거장 선체 끝에서, 최소~최대)")]
    [SerializeField] private Vector2 _freighterWaypointClearance = new Vector2(300f, 700f);

    [Header("정거장: 해적 습격")]
    [SerializeField] private PirateGroup _stationRaid = new PirateGroup();
    [Tooltip("정거장 지역에 들어온 뒤 첫 습격까지 (초, 최소~최대)")]
    [SerializeField] private Vector2 _stationRaidFirstDelay = new Vector2(45f, 90f);
    [Tooltip("습격해 오는 해적이 나타나는 거리 (정거장 선체 끝에서)")]
    [SerializeField, Min(0f)] private float _stationRaidClearance = 1400f;

    [Header("소행성대 해적 (비콘 레벨 1~5 순서)")]
    [SerializeField] private PirateGroup[] _beltLevels = new PirateGroup[0];
    [Tooltip("소행성대에 들어온 뒤 첫 해적이 나올 때까지 (초, 최소~최대)")]
    [SerializeField] private Vector2 _beltFirstDelay = new Vector2(8f, 15f);
    [Tooltip("해적이 나타나는 거리 (플레이어에게서, 최소~최대)")]
    [SerializeField] private Vector2 _beltSpawnDistance = new Vector2(550f, 750f);

    [Header("나타나는 자리")]
    [Tooltip("새로 나타나는 배는 플레이어와 이보다 가깝지 않은 곳에 놓는다 (화면 밖에서 나타나게)")]
    [SerializeField, Min(0f)] private float _minSpawnDistanceFromPlayer = 500f;
    [Tooltip("한 무리의 해적이 나란히 나타날 때의 간격")]
    [SerializeField, Min(0f)] private float _pirateSpacing = 60f;

    public static AmbientTraffic Instance { get; private set; }

    private readonly List<AllyShip> _allies = new List<AllyShip>();
    private readonly List<NpcFreighter> _freighters = new List<NpcFreighter>();
    private readonly List<PirateShip> _pirates = new List<PirateShip>();

    private EnemyShipCatalog _catalog;
    private Transform _player;
    private QuestTracker _quest;

    private StarSystem.Location _area = StarSystem.Location.None;
    private StationZone _station;
    private bool _alliesSpawned;
    private float _nextTick;
    private float _nextFreighter;
    private float _nextPirates;
    private float _nextRetarget;
    private bool _wasBlocked;

    // ── 시작 ──────────────────────────────────────────

    // 게임을 켤 때뿐 아니라 씬을 다시 불러올 때(패배 후 재시작, 타이틀에서 시작)도 다시 만든다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // 플레이어 함선과 모항 정거장이 있는 게임 씬에서만
        if (Instance != null || FindObjectOfType<SpaceshipController>() == null || StationZone.Home == null)
            return;

        var prefab = Resources.Load<AmbientTraffic>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[Traffic] Assets/Resources/{PrefabPath}.prefab을 찾지 못해 주변 NPC·해적이 나오지 않습니다.");
            return;
        }

        Instantiate(prefab).name = "AmbientTraffic";
    }

    private void Awake()
    {
        Instance = this;
        _catalog = EnemyShipCatalog.Load();
        StarSystem.OnAreaChanged += HandleAreaChanged;
    }

    private void Start()
    {
        var ship = FindObjectOfType<SpaceshipController>();
        _player = ship != null ? ship.transform : null;

        if (StarSystem.Instance != null)
            HandleAreaChanged(StarSystem.Instance.CurrentArea);
    }

    private void OnDestroy()
    {
        StarSystem.OnAreaChanged -= HandleAreaChanged;
        if (Instance == this)
            Instance = null;
    }

    // ── 지역 ──────────────────────────────────────────

    private void HandleAreaChanged(StarSystem.Location area)
    {
        // 도킹하면 함선이 먼 내부 공간으로 옮겨져 지역이 없어진 것으로 나온다 → 지역을 바꾸지 않는다.
        // 출항 중(아직 도킹 상태)에 바뀐 지역은 도킹이 끝난 뒤 Update에서 다시 맞춘다(SyncArea).
        if (StationDocking.IsDocked || area == _area)
            return;

        ClearAll();
        _area = area;
        _station = area.Kind == StarSystem.LocationKind.Station ? StarSystem.FindStation(area.Index) : null;
        _alliesSpawned = false;

        if (area.Kind == StarSystem.LocationKind.Station)
        {
            _nextFreighter = Time.time + Random.Range(0f, _freighterInterval.x);
            _nextPirates = Time.time + RandomIn(_stationRaidFirstDelay);
        }
        else if (area.Kind == StarSystem.LocationKind.Beacon)
        {
            _nextPirates = Time.time + RandomIn(_beltFirstDelay);
        }
    }

    /// <summary>도킹 중에 놓친 지역 변화를 맞춘다 (다른 곳에서 도킹해 다른 정거장으로 출항한 경우 등)</summary>
    private void SyncArea()
    {
        if (!StationDocking.IsDocked && StarSystem.Instance != null && StarSystem.Instance.CurrentArea != _area)
            HandleAreaChanged(StarSystem.Instance.CurrentArea);
    }

    /// <summary>이 지역에 띄운 배를 모두 바로 없앤다 (플레이어가 떠났다)</summary>
    private void ClearAll()
    {
        foreach (AllyShip ally in _allies)
            if (ally != null) Destroy(ally.gameObject);
        foreach (NpcFreighter freighter in _freighters)
            if (freighter != null) Destroy(freighter.gameObject);
        foreach (PirateShip pirate in _pirates)
            if (pirate != null) Destroy(pirate.gameObject);

        _allies.Clear();
        _freighters.Clear();
        _pirates.Clear();
    }

    // ── 진행 ──────────────────────────────────────────

    private void Update()
    {
        if (_player == null || _catalog == null || Time.time < _nextTick)
            return;

        _nextTick = Time.time + TickInterval;
        SyncArea();
        _allies.RemoveAll(a => a == null || a.Hull == null || !a.Hull.IsActive);
        _freighters.RemoveAll(f => f == null || f.Hull == null || !f.Hull.IsActive);
        _pirates.RemoveAll(p => p == null || p.Hull == null || !p.Hull.IsActive);

        bool blocked = PiratesBlocked();
        if (blocked && !_wasBlocked)
            RetreatPirates();
        _wasBlocked = blocked;

        bool quiet = StoryPlayer.IsPlaying || StationDocking.IsDocked || StarSystemWarp.IsWarping;

        if (_station != null)
        {
            if (!_alliesSpawned && !quiet)
                SpawnAllies();

            if (!quiet && Time.time >= _nextFreighter)
            {
                _nextFreighter = Time.time + RandomIn(_freighterInterval);
                if (_freighters.Count < _maxFreighters)
                    SpawnFreighter();
            }

            if (!blocked && AlivePirates() == 0 && Time.time >= _nextPirates)
            {
                _nextPirates = Time.time + RandomIn(_stationRaid.interval);
                SpawnStationRaid();
            }

            if (Time.time >= _nextRetarget)
            {
                _nextRetarget = Time.time + _retargetInterval;
                RetargetRaiders();
            }
        }
        else if (_area.Kind == StarSystem.LocationKind.Beacon)
        {
            PirateGroup group = BeltGroup();
            if (group != null && !blocked && AlivePirates() == 0 && Time.time >= _nextPirates)
            {
                _nextPirates = Time.time + RandomIn(group.interval);
                SpawnBeltPirates(group);
            }
        }
    }

    /// <summary>해적을 띄우면 안 되는 때 (임무·튜토리얼·스토리·도킹·워프 중)</summary>
    private bool PiratesBlocked()
    {
        if (MissionDirector.IsRunning || StoryPlayer.IsPlaying || StationDocking.IsDocked || StarSystemWarp.IsWarping)
            return true;

        if (_quest == null)
            _quest = FindObjectOfType<QuestTracker>(true);
        return _quest != null && _quest.CurrentQuestId == _tutorialQuestId;
    }

    /// <summary>띄워 둔 해적을 워프로 물러나게 한다 (임무 시작, 도킹 등)</summary>
    private void RetreatPirates()
    {
        foreach (PirateShip pirate in _pirates)
        {
            if (pirate != null && pirate.IsHostileTarget)
                pirate.SetMode(PirateShip.Mode.Warp);
        }
    }

    private int AlivePirates()
    {
        int alive = 0;
        foreach (PirateShip pirate in _pirates)
        {
            if (pirate.IsHostileTarget)
                alive++;
        }

        return alive;
    }

    // ── 정거장 ────────────────────────────────────────

    private void SpawnAllies()
    {
        _alliesSpawned = true;

        // 정거장 입구(출항하는 곳) 앞에 옆으로 나란히 띄우고, 거기서부터 순찰을 시작한다
        Vector3 dir = _station.EntranceDirection;
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        Vector3 front = _station.PointOutside(dir, _patrolClearance.x, _player.position.y);

        for (int i = 0; i < _allyCount; i++)
        {
            float offset = (i - (_allyCount - 1) * 0.5f) * _allySpawnSpacing;
            Vector3 position = front + side * offset + dir * Random.Range(0f, _allySpawnSpacing * 0.5f);

            AllyShip ally = _catalog.SpawnAlly(_allyClass, position, Quaternion.LookRotation(dir));
            if (ally == null)
                return;

            ally.Patrol(_station, _patrolClearance, _patrolHalfWidth, _patrolSpeed, _allyEngageRange, _allyLeashDistance);
            _allies.Add(ally);
        }
    }

    private void SpawnFreighter()
    {
        if (_freighterPrefab == null)
            return;

        float angle = Random.Range(0f, 360f);
        if (!TryFarPoint(_freighterSpawnClearance, ref angle, out Vector3 spawn))
            return;

        // 나타난 쪽에서 크게 벗어나지 않는 정거장 앞 지점에 들른다
        Vector3 waypoint = _station.PointOutside(Direction(angle + Random.Range(-60f, 60f)),
            RandomIn(_freighterWaypointClearance), _player.position.y);

        GameObject go = Instantiate(_freighterPrefab, spawn, Quaternion.identity);
        go.name = "NpcFreighter";

        // 호송 임무용 이동은 떼고 주변 교통용으로 움직인다
        var escort = go.GetComponent<EscortFreighter>();
        if (escort != null)
        {
            escort.enabled = false;
            Destroy(escort);
        }

        var freighter = go.AddComponent<NpcFreighter>();
        freighter.Init(_freighter, _station, waypoint, _warpPrefab);
        _freighters.Add(freighter);
    }

    private void SpawnStationRaid()
    {
        float angle = Random.Range(0f, 360f);
        if (!TryFarPoint(_stationRaidClearance, ref angle, out Vector3 center))
            return;

        Vector3 face = _station.transform.position - center;
        SpawnPirates(_stationRaid, center, face, NearestFriendly(center));
    }

    /// <summary>습격한 해적마다 가장 가까운 플레이어 편(플레이어·경비선·화물선)을 노리게 한다</summary>
    private void RetargetRaiders()
    {
        foreach (PirateShip pirate in _pirates)
        {
            if (!pirate.IsHostileTarget || pirate.Current != PirateShip.Mode.Raid)
                continue;

            Transform target = NearestFriendly(pirate.transform.position);
            if (target != null && target != pirate.RaidTarget)
                pirate.Raid(target);
        }
    }

    private Transform NearestFriendly(Vector3 from)
    {
        Transform best = StationDocking.IsDocked ? null : _player;
        float bestDistance = best != null ? HorizontalDistance(best.position, from) : float.PositiveInfinity;

        foreach (AllyShip ally in _allies)
            Consider(ally.transform);
        foreach (NpcFreighter freighter in _freighters)
            Consider(freighter.transform);

        return best;

        void Consider(Transform t)
        {
            float d = HorizontalDistance(t.position, from);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = t;
            }
        }
    }

    /// <summary>
    /// 정거장 선체에서 clearance만큼 떨어진 점 중 플레이어에게서 충분히 먼 곳을 고른다 (angle부터 둘레를 돌며 찾는다).
    /// 찾은 각도를 angle에 돌려준다.
    /// </summary>
    private bool TryFarPoint(float clearance, ref float angle, out Vector3 point)
    {
        for (int i = 0; i < 8; i++)
        {
            float a = angle + i * 45f;
            point = _station.PointOutside(Direction(a), clearance, _player.position.y);
            if (HorizontalDistance(point, _player.position) >= _minSpawnDistanceFromPlayer)
            {
                angle = a;
                return true;
            }
        }

        point = Vector3.zero;
        return false;
    }

    // ── 소행성대 ──────────────────────────────────────

    private PirateGroup BeltGroup()
    {
        if (StarSystem.Instance == null || _beltLevels.Length == 0)
            return null;

        int level = StarSystem.Instance.Data.beacons[_area.Index].level;
        return _beltLevels[Mathf.Clamp(level - 1, 0, _beltLevels.Length - 1)];
    }

    private void SpawnBeltPirates(PirateGroup group)
    {
        float angle = Random.Range(0f, 360f);
        Vector3 center = _player.position + Direction(angle) * RandomIn(_beltSpawnDistance);
        SpawnPirates(group, center, _player.position - center, _player);
    }

    // ── 해적 ──────────────────────────────────────────

    private void SpawnPirates(PirateGroup group, Vector3 center, Vector3 face, Transform target)
    {
        if (group.classes == null || group.classes.Length == 0)
            return;

        face.y = 0f;
        Quaternion rotation = face.sqrMagnitude > 0.01f ? Quaternion.LookRotation(face) : Quaternion.identity;
        Vector3 side = rotation * Vector3.right;

        int count = Random.Range(group.count.x, group.count.y + 1);
        for (int i = 0; i < count; i++)
        {
            // 옆으로 나란히 (서로 겹치지 않게)
            Vector3 position = center + side * ((i - (count - 1) * 0.5f) * _pirateSpacing);
            string shipClass = group.classes[Random.Range(0, group.classes.Length)];

            PirateShip pirate = _catalog.Spawn(shipClass, position, rotation, _player, _warpPrefab);
            if (pirate == null)
                continue;

            pirate.Raid(target != null ? target : _player);
            _pirates.Add(pirate);
        }
    }

    // ── 계산 ──────────────────────────────────────────

    /// <summary>위쪽(+Z) = 0도, 시계 방향</summary>
    private static Vector3 Direction(float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
    }

    private static float RandomIn(Vector2 range)
    {
        return Random.Range(range.x, range.y);
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
