using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 워프 터널 안에서 성운 덩어리(FORGE3D Warp Tunnel/Example의 Cluster 프리팹)가 앞에서 뒤로 휙휙 지나가게 해서
/// "빠르게 앞으로 나아가는 중"이라는 느낌을 만든다. FORGE3D 예제의 ClusterEmitter와 같은 방식이지만,
/// 생성한 덩어리를 이 오브젝트 밑에 두어 오브젝트를 지우면 같이 정리되고, 풀링으로 재사용하며, 도중에 색 계열(팔레트)을 바꿀 수 있다.
/// 이 오브젝트의 +Z 방향이 진행 방향이다(덩어리는 +Z 앞쪽에서 나타나 -Z로 흘러간다).
/// </summary>
public class WarpClusterFlow : MonoBehaviour
{
    [Header("개수 / 간격")]
    [SerializeField, Min(1)] private int _maxActive = 12;
    [SerializeField, Min(0.02f)] private float _spawnInterval = 0.2f;

    [Header("이동")]
    [Tooltip("덩어리가 나타나는 앞쪽 거리")]
    [SerializeField] private float _spawnDistance = 1000f;
    [Tooltip("이 거리보다 뒤로 지나가면 사라진다(음수 = 카메라 뒤)")]
    [SerializeField] private float _endDistance = -140f;
    [SerializeField] private float _speed = 600f;

    [Header("배치 (진행 방향에 수직인 원 안에서)")]
    [Tooltip("진행 축에서 떨어진 거리 범위. 터널 반지름(약 85)보다 작게.")]
    [SerializeField] private Vector2 _ringRadius = new Vector2(55f, 80f);
    [SerializeField] private Vector2 _scaleRange = new Vector2(10f, 28f);

    /// <summary>진행 속도 배율 (워프 가속/폭풍에서 키운다)</summary>
    public float SpeedMultiplier { get; set; } = 1f;

    private sealed class Item
    {
        public Transform transform;
        public GameObject prefab;
    }

    private readonly List<Item> _active = new List<Item>();
    private readonly Dictionary<GameObject, Stack<Transform>> _pool = new Dictionary<GameObject, Stack<Transform>>();
    private GameObject[] _palette = new GameObject[0];
    private float _timer;

    /// <summary>앞으로 새로 나오는 덩어리의 종류. 이미 날아가는 덩어리는 그대로 지나간다.</summary>
    public void SetPalette(GameObject[] prefabs)
    {
        _palette = prefabs ?? new GameObject[0];
    }

    /// <summary>시작할 때 터널이 텅 비어 보이지 않도록, 진행 경로 곳곳에 미리 덩어리를 흩뿌려 둔다.</summary>
    public void Prewarm()
    {
        for (int i = 0; i < _maxActive; i++)
            Spawn(Random.Range(0f, _spawnDistance));
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= _spawnInterval)
        {
            _timer = 0f;
            if (_active.Count < _maxActive)
                Spawn(_spawnDistance);
        }

        Vector3 forward = transform.forward;
        float step = _speed * SpeedMultiplier * Time.deltaTime;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            Transform t = _active[i].transform;
            t.position -= forward * step;

            if (Vector3.Dot(t.position - transform.position, forward) < _endDistance)
                Recycle(i);
        }
    }

    private void Spawn(float distanceAhead)
    {
        if (_palette.Length == 0)
            return;

        GameObject prefab = _palette[Random.Range(0, _palette.Length)];
        if (prefab == null)
            return;

        Transform instance = GetFromPool(prefab);

        // 진행 방향에 수직인 평면 위의 한 점 (터널 벽에 닿지 않는 링 모양 범위)
        Vector2 circle = Random.insideUnitCircle.normalized * Random.Range(_ringRadius.x, _ringRadius.y);
        Vector3 offset = transform.right * circle.x + transform.up * circle.y;

        instance.position = transform.position + transform.forward * distanceAhead + offset;
        instance.rotation = Random.rotation;
        instance.localScale = Vector3.one * Random.Range(_scaleRange.x, _scaleRange.y);
        instance.gameObject.SetActive(true);

        _active.Add(new Item { transform = instance, prefab = prefab });
    }

    private Transform GetFromPool(GameObject prefab)
    {
        if (_pool.TryGetValue(prefab, out Stack<Transform> stack) && stack.Count > 0)
            return stack.Pop();

        GameObject go = Instantiate(prefab, transform);
        return go.transform;
    }

    private void Recycle(int index)
    {
        Item item = _active[index];
        _active.RemoveAt(index);

        item.transform.gameObject.SetActive(false);

        if (!_pool.TryGetValue(item.prefab, out Stack<Transform> stack))
        {
            stack = new Stack<Transform>();
            _pool[item.prefab] = stack;
        }

        stack.Push(item.transform);
    }
}
