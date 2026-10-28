using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 소행성 지대: 플레이어(함선) 주위에 거대한 암석과 부서진 선박 잔해를 흩뿌린다.
/// 암석은 항상 "최대 maxRocks개"만 존재한다. 함선이 움직여 aroundRadius 밖으로 멀어진 조각은 지워지는 대신
/// 함선 앞쪽 가장자리로 옮겨져 서서히 나타난다. 그래서 어디로 가도 주변 밀도가 같고, 화면이 암석으로 가득 차지 않는다.
/// 2D Space Kit의 소행성/잔해 스프라이트를 쓰되, 바닥에 눕히지 않고 항상 카메라를 바라보게 세워서(빌보드) 입체감을 살린다.
/// 암석은 카메라 방향 축을 중심으로 천천히 돈다. 장식용이라 충돌체는 없다.
/// 난수는 시드가 고정이라 같은 움직임이면 같은 배치가 나온다.
/// </summary>
public class AsteroidField : MonoBehaviour
{
    [SerializeField] private Sprite[] _rocks;
    [SerializeField] private Sprite[] _wrecks;

    [Header("개수 (플레이어 주위에 동시에 존재하는 최대 개수)")]
    [SerializeField, Min(0)] private int _maxRocks = 10;
    [SerializeField, Min(0)] private int _maxWrecks = 5;

    [Header("범위 (함선 기준 반지름)")]
    [Tooltip("이 반지름 안에는 두지 않는다 (함선과 겹치지 않게)")]
    [SerializeField, Min(0f)] private float _clearRadius = 80f;
    [Tooltip("이 반지름 안이 '플레이어 주위'. 이보다 멀어진 조각은 앞쪽 가장자리로 옮겨진다.")]
    [SerializeField, Min(1f)] private float _aroundRadius = 280f;

    [Header("크기 (월드 단위, 스프라이트 긴 쪽 기준)")]
    [SerializeField] private Vector2 _rockSize = new Vector2(32f, 105f);
    [SerializeField] private Vector2 _wreckSize = new Vector2(18f, 42f);

    [Tooltip("함선이 다니는 높이 기준으로 암석 중심의 높이 범위 (카메라를 향해 서 있으므로 위아래로 퍼진다)")]
    [SerializeField] private Vector2 _height = new Vector2(-8f, 6f);
    [SerializeField, Min(0.05f)] private float _fadeInSeconds = 1.2f;
    [SerializeField] private int _seed = 4172;

    private sealed class Piece
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public bool isWreck;
        public float roll;          // 카메라 방향 축 기준 시작 각도
        public float spinPerSecond; // 천천히 도는 속도(도/초)
        public float fade;          // 0 → 1: 새로 놓인 뒤 서서히 나타나는 정도
    }

    private readonly List<Piece> _pieces = new List<Piece>();
    private System.Random _rng;
    private Transform _target;
    private Camera _camera;

    private void Start()
    {
        _rng = new System.Random(_seed);
        _target = FindTarget();

        Create(_rocks, _maxRocks, false, -20);
        Create(_wrecks, _maxWrecks, true, -19);

        // 처음에는 카메라가 보는 앞쪽에 몰아서 놓아, 도착하자마자 암석들이 눈에 들어오게 한다
        foreach (Piece p in _pieces)
            Place(p, true);
    }

    private static Transform FindTarget()
    {
        var ship = FindObjectOfType<SpaceshipController>();
        return ship != null ? ship.transform : null;
    }

    private void Create(Sprite[] sprites, int count, bool wreck, int sortingOrder)
    {
        if (sprites == null || sprites.Length == 0)
            return;

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject($"{(wreck ? "Wreck" : "Rock")}_{i}");
            go.transform.SetParent(transform, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder; // 함선보다 항상 뒤

            _pieces.Add(new Piece { transform = go.transform, renderer = renderer, isWreck = wreck });
        }
    }

    /// <summary>조각 하나를 함선 주위의 새 자리에 놓고(크기/모양도 새로 뽑는다) 서서히 나타나게 한다.</summary>
    private void Place(Piece p, bool initial)
    {
        Vector3 center = _target != null ? _target.position : transform.position;

        // 반지름: 처음엔 주위 전체에, 옮길 때는 가장자리 근처에 (화면 밖에서 나타나도록)
        float minR = initial ? _clearRadius : _aroundRadius * 0.8f;
        float r = Mathf.Sqrt(Mathf.Lerp(minR * minR, _aroundRadius * _aroundRadius, (float)_rng.NextDouble()));

        // 각도: 카메라가 보는 앞쪽(+Z)에 치우쳐서. 처음엔 70%를 앞쪽 가까이에 몰아 놓는다.
        float spread = initial && _rng.NextDouble() < 0.7 ? 70f : 110f;
        float angle = Mathf.Lerp(-spread, spread, (float)_rng.NextDouble()) * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(Mathf.Sin(angle) * r, Mathf.Lerp(_height.x, _height.y, (float)_rng.NextDouble()), Mathf.Cos(angle) * r);
        p.transform.position = center + offset;

        Sprite[] sprites = p.isWreck ? _wrecks : _rocks;
        Sprite sprite = sprites[_rng.Next(sprites.Length)];
        p.renderer.sprite = sprite;

        // 스프라이트 크기가 제각각이라, 긴 변이 원하는 월드 크기가 되도록 배율을 맞춘다
        Vector2 sizeRange = p.isWreck ? _wreckSize : _rockSize;
        float longSide = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        float size = Mathf.Lerp(sizeRange.x, sizeRange.y, (float)_rng.NextDouble());
        p.transform.localScale = Vector3.one * (size / longSide);

        p.roll = (float)_rng.NextDouble() * 360f;
        p.spinPerSecond = Mathf.Lerp(-4f, 4f, (float)_rng.NextDouble());
        p.fade = 0f;
        SetAlpha(p, 0f);
    }

    private void LateUpdate()
    {
        if (_pieces.Count == 0)
            return;

        if (_target == null)
            _target = FindTarget();

        if (_camera == null)
            _camera = Camera.main;

        Vector3 center = _target != null ? _target.position : transform.position;
        Quaternion facing = _camera != null ? _camera.transform.rotation : Quaternion.identity;
        float time = Time.time;
        float aroundSqr = _aroundRadius * _aroundRadius;

        foreach (Piece p in _pieces)
        {
            // 주위(수평 거리)를 벗어났으면 다시 놓는다
            Vector3 d = p.transform.position - center;
            d.y = 0f;
            if (_target != null && d.sqrMagnitude > aroundSqr)
                Place(p, false);

            // 모든 조각이 카메라와 같은 방향으로 서서 화면을 정면으로 바라본다 (카메라가 움직여도 따라간다)
            p.transform.rotation = facing * Quaternion.Euler(0f, 0f, p.roll + p.spinPerSecond * time);

            if (p.fade < 1f)
            {
                p.fade = Mathf.Min(1f, p.fade + Time.deltaTime / _fadeInSeconds);
                SetAlpha(p, Mathf.SmoothStep(0f, 1f, p.fade));
            }
        }
    }

    private static void SetAlpha(Piece p, float alpha)
    {
        Color c = p.renderer.color;
        c.a = alpha;
        p.renderer.color = c;
    }
}
