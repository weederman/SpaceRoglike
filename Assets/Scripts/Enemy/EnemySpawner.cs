using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적을 스폰하고, 하나가 파괴되면(HullHealth.OnBroken) 정원(_maxAliveCount)을
/// 다시 채우도록 새로 스폰한다. 정원은 인스펙터에서 언제든 조절 가능하다.
/// 플레이어와 _despawnDistance보다 멀어진 적(워프·도킹으로 떠나 남겨진 적)은 없애고, 정원은 플레이어 곁(화면 밖)에서 다시 채운다.
/// 정거장 안(도킹 중), 워프 중, 항성계 밖(워프 터널 무대)에서는 스폰하지 않는다.
/// 꺼져 있는 동안(enabled = false)은 스스로 스폰하지 않는다(멀어진 적을 없애는 것은 계속한다). 프롤로그가 끄고, 튜토리얼에서는
/// 장면 Director가 적을 직접 만들므로 스토리 cue로 다시 켠다.
///   enemies_on    스포너를 켜고 정원을 채운다
///   enemies_off   스포너를 끈다(살아있는 적은 그대로)
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    private const string OnCue = "enemies_on";
    private const string OffCue = "enemies_off";
    private const float CheckInterval = 0.5f;

    [SerializeField] private GameObject _enemyPrefab;
    [SerializeField] private Transform[] _spawnPoints;

    [Tooltip("동시에 존재할 수 있는 적의 최대 개체수. 나중에 자유롭게 조절 가능.")]
    [SerializeField, Min(1)] private int _maxAliveCount = 1;

    [Tooltip("적이 죽고 나서 다시 스폰되기까지의 대기 시간(초).")]
    [SerializeField, Min(0f)] private float _respawnDelay = 3f;

    [Tooltip("플레이어와 이보다 멀어진 적은 없앤다 (수평 거리)")]
    [SerializeField, Min(100f)] private float _despawnDistance = 2500f;
    [Tooltip("스폰 지점이 플레이어에게서 멀면, 플레이어에게서 이만큼 떨어진 곳(화면 밖)에 스폰한다")]
    [SerializeField, Min(50f)] private float _spawnDistance = 900f;

    private readonly List<GameObject> _alive = new List<GameObject>();
    private bool _started;
    private float _nextSpawnTime;
    private Transform _player;

    /// <summary>동시에 존재할 수 있는 적의 최대 개체수. 늘리면 즉시 부족한 만큼 스폰한다(줄여도 살아있는 적은 그대로).</summary>
    public int MaxAliveCount
    {
        get => _maxAliveCount;
        set
        {
            _maxAliveCount = Mathf.Max(1, value);
            FillToCapacity();
        }
    }

    public float RespawnDelay
    {
        get => _respawnDelay;
        set => _respawnDelay = Mathf.Max(0f, value);
    }

    public IReadOnlyList<GameObject> AliveEnemies => _alive;

    // 꺼져 있어도 cue를 받아야 하므로 OnEnable이 아니라 Awake/OnDestroy에서 구독한다
    private void Awake()
    {
        StoryPlayer.OnCue += HandleCue;

        // 코루틴은 컴포넌트가 꺼져 있어도 돈다: 멀어진 적은 꺼져 있을 때도 없앤다
        StartCoroutine(WatchRoutine());
    }

    private void OnDestroy()
    {
        StoryPlayer.OnCue -= HandleCue;
    }

    private void HandleCue(string cue)
    {
        if (cue == OnCue)
        {
            enabled = true;

            // 아직 Start 전이면 Start에서 채운다
            if (_started)
                FillToCapacity();
        }
        else if (cue == OffCue)
        {
            enabled = false;
        }
    }

    private void Start()
    {
        _started = true;

        // 개발자 모드에서 바꿔 둔 값이 있으면 씬을 다시 불러와도 유지한다
        if (DevTuning.EnemyCount.HasValue)
            _maxAliveCount = Mathf.Max(1, Mathf.RoundToInt(DevTuning.EnemyCount.Value));
        if (DevTuning.EnemyRespawnDelay.HasValue)
            _respawnDelay = Mathf.Max(0f, DevTuning.EnemyRespawnDelay.Value);

        FillToCapacity();
    }

    private IEnumerator WatchRoutine()
    {
        var wait = new WaitForSeconds(CheckInterval);
        while (true)
        {
            yield return wait;
            DespawnFarEnemies();

            if (enabled && _started && _alive.Count < _maxAliveCount && Time.time >= _nextSpawnTime)
                FillToCapacity();
        }
    }

    private void DespawnFarEnemies()
    {
        Transform player = Player;
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            GameObject enemy = _alive[i];
            if (enemy == null)
            {
                _alive.RemoveAt(i);
                continue;
            }

            if (player != null && HorizontalDistance(enemy.transform.position, player.position) > _despawnDistance)
            {
                _alive.RemoveAt(i);
                Destroy(enemy);
            }
        }
    }

    private void FillToCapacity()
    {
        for (int i = _alive.Count; i < _maxAliveCount; i++)
        {
            if (!SpawnOne())
                return;
        }
    }

    /// <summary>적 하나를 스폰한다. 지금 스폰할 수 없는 곳이면(정거장 안, 워프 중 등) false.</summary>
    private bool SpawnOne()
    {
        if (_enemyPrefab == null || _spawnPoints == null || _spawnPoints.Length == 0)
            return false;

        if (!TryPickSpawn(out Vector3 position, out Quaternion rotation))
            return false;

        GameObject enemy = Instantiate(_enemyPrefab, position, rotation);
        _alive.Add(enemy);
        DevTuning.ApplyToEnemy(enemy);

        HullHealth hull = enemy.GetComponentInChildren<HullHealth>();
        if (hull != null)
            hull.OnBroken += () => HandleEnemyDeath(enemy);
        return true;
    }

    /// <summary>스폰 지점이 플레이어 곁이면 그곳, 아니면 플레이어 주위 화면 밖</summary>
    private bool TryPickSpawn(out Vector3 position, out Quaternion rotation)
    {
        Transform point = _spawnPoints[Random.Range(0, _spawnPoints.Length)];
        position = point.position;
        rotation = point.rotation;

        Transform player = Player;
        if (player == null)
            return true;

        if (StationDocking.IsDocked || StarSystemWarp.IsWarping
            || (StarSystem.Instance != null && !StarSystem.Instance.IsInsideSystem(player.position)))
            return false;

        if (HorizontalDistance(position, player.position) <= _spawnDistance * 1.5f)
            return true;

        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        position = player.position + dir * _spawnDistance;
        position.y = point.position.y;
        rotation = Quaternion.LookRotation(-dir);
        return true;
    }

    private Transform Player
    {
        get
        {
            if (_player == null)
            {
                var ship = FindObjectOfType<SpaceshipController>();
                _player = ship != null ? ship.transform : null;
            }

            return _player;
        }
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        _alive.Remove(enemy);
        _nextSpawnTime = Time.time + _respawnDelay;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
