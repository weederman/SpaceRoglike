using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적을 스폰하고, 하나가 파괴되면(HullHealth.OnBroken) 정원(_maxAliveCount)을
/// 다시 채우도록 새로 스폰한다. 정원은 인스펙터에서 언제든 조절 가능하다.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject _enemyPrefab;
    [SerializeField] private Transform[] _spawnPoints;

    [Tooltip("동시에 존재할 수 있는 적의 최대 개체수. 나중에 자유롭게 조절 가능.")]
    [SerializeField, Min(1)] private int _maxAliveCount = 1;

    [Tooltip("적이 죽고 나서 다시 스폰되기까지의 대기 시간(초).")]
    [SerializeField, Min(0f)] private float _respawnDelay = 3f;

    private readonly List<GameObject> _alive = new List<GameObject>();

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

    private void Start()
    {
        // 개발자 모드에서 바꿔 둔 값이 있으면 씬을 다시 불러와도 유지한다
        if (DevTuning.EnemyCount.HasValue)
            _maxAliveCount = Mathf.Max(1, Mathf.RoundToInt(DevTuning.EnemyCount.Value));
        if (DevTuning.EnemyRespawnDelay.HasValue)
            _respawnDelay = Mathf.Max(0f, DevTuning.EnemyRespawnDelay.Value);

        FillToCapacity();
    }

    private void FillToCapacity()
    {
        for (int i = _alive.Count; i < _maxAliveCount; i++)
            SpawnOne();
    }

    private void SpawnOne()
    {
        if (_enemyPrefab == null || _spawnPoints == null || _spawnPoints.Length == 0)
            return;

        Transform point = _spawnPoints[Random.Range(0, _spawnPoints.Length)];
        GameObject enemy = Instantiate(_enemyPrefab, point.position, point.rotation);
        _alive.Add(enemy);
        DevTuning.ApplyToEnemy(enemy);

        HullHealth hull = enemy.GetComponentInChildren<HullHealth>();
        if (hull != null)
            hull.OnBroken += () => HandleEnemyDeath(enemy);
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        _alive.Remove(enemy);
        StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(_respawnDelay);

        if (_alive.Count < _maxAliveCount)
            SpawnOne();
    }
}
