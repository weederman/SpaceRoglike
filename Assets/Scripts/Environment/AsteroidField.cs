using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 소행성 지대: 플레이어(함선) 주위에 거대한 암석과 부서진 선박 잔해를 흩뿌린다.
/// 암석은 항상 "최대 maxRocks개"만 존재한다. 함선이 움직여 aroundRadius 밖으로 멀어진 조각은 지워지는 대신
/// 함선 앞쪽 가장자리로 옮겨져 서서히 나타난다(암석은 작게 시작해 커진다). 그래서 어디로 가도 주변 밀도가 같고, 화면이 암석으로 가득 차지 않는다.
/// 암석은 3D 소행성 모델(FreeSciFi 소행성 메시 + 회색 암석 재질)이며, 함선이 다니는 높이보다 조금 아래에 놓여 함선이 그 위를 지나간다.
/// 저마다 다른 축으로 천천히 구른다. 잔해 조각은 2D Space Kit 스프라이트를 카메라를 향해 세운다(빌보드). 장식용이라 충돌체는 없다.
/// 큰 암석끼리는 가장자리 사이가 최소 _minRockGap만큼 떨어지게 놓는다 (잔해 조각은 작아서 간격을 따지지 않는다).
/// 난수는 시드가 고정이라 같은 움직임이면 같은 배치가 나온다.
/// 해적선 등이 암석 사이에 숨을 자리를 찾을 수 있도록 지금 놓인 암석 목록을 GetRocks로 알려 준다.
/// </summary>
public class AsteroidField : MonoBehaviour
{
    /// <summary>지금 놓인 큰 암석 하나 (수평 위치와 대략의 반지름)</summary>
    public struct Rock
    {
        public Vector3 position;
        public float radius;
    }

    /// <summary>지금 활성화된 소행성 지대 (없으면 null)</summary>
    public static AsteroidField Active { get; private set; }

    [Header("암석 (3D 모델)")]
    [SerializeField] private Mesh[] _rockMeshes;
    [Tooltip("암석마다 이 중 하나를 무작위로 입힌다")]
    [SerializeField] private Material[] _rockMaterials;
    [Tooltip("함선이 다니는 높이 기준으로 암석 중심의 높이 범위 (아래쪽에 두어 함선이 위로 지나가게)")]
    [SerializeField] private Vector2 _rockHeight = new Vector2(-45f, -12f);
    [Tooltip("구르는 속도 (도/초)")]
    [SerializeField, Min(0f)] private float _rockTumble = 6f;

    [Header("잔해 (스프라이트)")]
    [SerializeField] private Sprite[] _wrecks;

    [Header("개수 (플레이어 주위에 동시에 존재하는 최대 개수)")]
    [SerializeField, Min(0)] private int _maxRocks = 10;
    [SerializeField, Min(0)] private int _maxWrecks = 5;

    [Header("범위 (함선 기준 반지름)")]
    [Tooltip("이 반지름 안에는 두지 않는다 (함선과 겹치지 않게)")]
    [SerializeField, Min(0f)] private float _clearRadius = 80f;
    [Tooltip("이 반지름 안이 '플레이어 주위'. 이보다 멀어진 조각은 앞쪽 가장자리로 옮겨진다.")]
    [SerializeField, Min(1f)] private float _aroundRadius = 280f;

    [Header("크기 (월드 단위, 긴 쪽 기준)")]
    [SerializeField] private Vector2 _rockSize = new Vector2(32f, 105f);
    [SerializeField] private Vector2 _wreckSize = new Vector2(18f, 42f);

    [Tooltip("큰 암석끼리 가장자리 사이의 최소 간격 (월드 단위)")]
    [SerializeField, Min(0f)] private float _minRockGap = 30f;
    [Tooltip("간격을 지키는 자리를 찾을 때 시도하는 횟수. 끝내 못 찾으면 크기를 줄이고, 그래도 안 되면 가장 넓은 자리에 둔다.")]
    [SerializeField, Min(1)] private int _placeAttempts = 24;

    [Tooltip("함선이 다니는 높이 기준으로 잔해 조각 중심의 높이 범위 (카메라를 향해 서 있으므로 위아래로 퍼진다)")]
    [SerializeField] private Vector2 _height = new Vector2(-8f, 6f);
    [SerializeField, Min(0.05f)] private float _fadeInSeconds = 1.2f;
    [SerializeField] private int _seed = 4172;

    private sealed class Piece
    {
        public Transform transform;
        public SpriteRenderer sprite;     // 잔해 조각
        public MeshFilter meshFilter;     // 암석
        public MeshRenderer meshRenderer; // 암석
        public bool isWreck;
        public float roll;          // 잔해: 카메라 방향 축 기준 시작 각도
        public float spinPerSecond; // 천천히 도는 속도(도/초)
        public Quaternion baseRotation; // 암석: 놓일 때의 무작위 자세
        public Vector3 spinAxis;        // 암석: 구르는 축
        public float baseScale;     // 다 나타났을 때의 배율
        public float fade;          // 0 → 1: 새로 놓인 뒤 서서히 나타나는 정도
        public float radius;        // 긴 변 기준 대략의 반지름 (간격 계산용)
        public bool placed;
    }

    private readonly List<Piece> _pieces = new List<Piece>();
    private System.Random _rng;
    private Transform _target;
    private Camera _camera;

    private void OnEnable()
    {
        Active = this;
    }

    private void OnDisable()
    {
        if (Active == this)
            Active = null;
    }

    /// <summary>지금 놓인 큰 암석들을 result에 채운다 (잔해 조각은 빼고).</summary>
    public void GetRocks(List<Rock> result)
    {
        result.Clear();
        foreach (Piece p in _pieces)
        {
            if (!p.isWreck && p.placed)
                result.Add(new Rock { position = p.transform.position, radius = p.radius });
        }
    }

    private void Start()
    {
        _rng = new System.Random(_seed);
        _target = FindTarget();

        CreateRocks(_maxRocks);
        CreateWrecks(_maxWrecks);

        // 처음에는 카메라가 보는 앞쪽에 몰아서 놓아, 도착하자마자 암석들이 눈에 들어오게 한다
        foreach (Piece p in _pieces)
            Place(p, true);
    }

    private static Transform FindTarget()
    {
        var ship = FindObjectOfType<SpaceshipController>();
        return ship != null ? ship.transform : null;
    }

    private void CreateRocks(int count)
    {
        if (_rockMeshes == null || _rockMeshes.Length == 0 || _rockMaterials == null || _rockMaterials.Length == 0)
            return;

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject($"Rock_{i}");
            go.transform.SetParent(transform, false);

            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _pieces.Add(new Piece { transform = go.transform, meshFilter = filter, meshRenderer = renderer });
        }
    }

    private void CreateWrecks(int count)
    {
        if (_wrecks == null || _wrecks.Length == 0)
            return;

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject($"Wreck_{i}");
            go.transform.SetParent(transform, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = -19; // 함선보다 항상 뒤

            _pieces.Add(new Piece { transform = go.transform, sprite = renderer, isWreck = true });
        }
    }

    /// <summary>조각 하나를 함선 주위의 새 자리에 놓고(크기/모양도 새로 뽑는다) 서서히 나타나게 한다.</summary>
    private void Place(Piece p, bool initial)
    {
        Vector3 center = _target != null ? _target.position : transform.position;

        // 모양: 잔해는 스프라이트, 암석은 메시와 재질을 새로 뽑는다. 긴 쪽 길이로 크기를 맞춘다.
        float longSide;
        if (p.isWreck)
        {
            Sprite sprite = _wrecks[_rng.Next(_wrecks.Length)];
            p.sprite.sprite = sprite;
            longSide = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        }
        else
        {
            Mesh mesh = _rockMeshes[_rng.Next(_rockMeshes.Length)];
            p.meshFilter.sharedMesh = mesh;
            p.meshRenderer.sharedMaterial = _rockMaterials[_rng.Next(_rockMaterials.Length)];
            Vector3 b = mesh.bounds.size;
            longSide = Mathf.Max(b.x, Mathf.Max(b.y, b.z));
        }

        Vector2 sizeRange = p.isWreck ? _wreckSize : _rockSize;
        float size = Mathf.Lerp(sizeRange.x, sizeRange.y, (float)_rng.NextDouble());

        // 큰 암석은 다른 암석과 간격을 지키는 자리를 찾는다. 못 찾으면 크기를 줄여 가며 다시 찾고,
        // 끝내 없으면 이웃과 가장 덜 겹치는 자리에 둔다.
        Vector3 position = RandomOffset(center, initial, p.isWreck);
        if (!p.isWreck)
        {
            float bestGap = float.NegativeInfinity;
            Vector3 best = position;

            for (int attempt = 0; attempt < _placeAttempts; attempt++)
            {
                Vector3 candidate = attempt == 0 ? position : RandomOffset(center, initial, false);
                float gap = SmallestGap(p, candidate, size * 0.5f);

                if (gap >= _minRockGap)
                {
                    bestGap = gap;
                    best = candidate;
                    break;
                }

                if (gap > bestGap)
                {
                    bestGap = gap;
                    best = candidate;
                }

                // 시도의 절반이 지나도 자리가 없으면 조금씩 작게
                if (attempt >= _placeAttempts / 2)
                    size = Mathf.Max(sizeRange.x, size * 0.92f);
            }

            position = best;
        }

        p.transform.position = position;

        // 모양마다 크기가 제각각이라, 긴 쪽이 원하는 월드 크기가 되도록 배율을 맞춘다
        p.baseScale = size / Mathf.Max(0.001f, longSide);
        p.radius = size * 0.5f;
        p.placed = true;

        p.roll = (float)_rng.NextDouble() * 360f;
        if (p.isWreck)
        {
            p.spinPerSecond = Mathf.Lerp(-4f, 4f, (float)_rng.NextDouble());
        }
        else
        {
            p.spinPerSecond = Mathf.Lerp(0.4f, 1f, (float)_rng.NextDouble()) * _rockTumble * (_rng.NextDouble() < 0.5 ? -1f : 1f);
            p.baseRotation = Quaternion.Euler((float)_rng.NextDouble() * 360f, (float)_rng.NextDouble() * 360f, (float)_rng.NextDouble() * 360f);
            p.spinAxis = new Vector3((float)_rng.NextDouble() - 0.5f, (float)_rng.NextDouble() - 0.5f, (float)_rng.NextDouble() - 0.5f);
            p.spinAxis = p.spinAxis.sqrMagnitude > 0.01f ? p.spinAxis.normalized : Vector3.up;
        }

        p.fade = 0f;
        ApplyFade(p);
    }

    /// <summary>함선 주위의 무작위 자리. 처음엔 주위 전체에, 옮길 때는 가장자리 근처에(화면 밖에서 나타나도록).</summary>
    private Vector3 RandomOffset(Vector3 center, bool initial, bool wreck)
    {
        float minR = initial ? _clearRadius : _aroundRadius * 0.8f;
        float r = Mathf.Sqrt(Mathf.Lerp(minR * minR, _aroundRadius * _aroundRadius, (float)_rng.NextDouble()));

        // 각도: 카메라가 보는 앞쪽(+Z)에 치우쳐서. 처음엔 70%를 앞쪽 가까이에 몰아 놓는다.
        float spread = initial && _rng.NextDouble() < 0.7 ? 70f : 110f;
        float angle = Mathf.Lerp(-spread, spread, (float)_rng.NextDouble()) * Mathf.Deg2Rad;

        Vector2 height = wreck ? _height : _rockHeight;
        return center + new Vector3(Mathf.Sin(angle) * r, Mathf.Lerp(height.x, height.y, (float)_rng.NextDouble()), Mathf.Cos(angle) * r);
    }

    /// <summary>이 자리에 반지름 radius인 암석을 두면, 이미 놓인 다른 큰 암석(과 채굴용 광맥 소행성)과
    /// 가장자리 사이가 가장 가까운 거리 (수평 기준).</summary>
    private float SmallestGap(Piece self, Vector3 position, float radius)
    {
        float smallest = float.PositiveInfinity;
        foreach (Piece other in _pieces)
        {
            if (other == self || other.isWreck || !other.placed)
                continue;

            Vector3 d = other.transform.position - position;
            d.y = 0f;
            smallest = Mathf.Min(smallest, d.magnitude - radius - other.radius);
        }

        foreach (MineableAsteroid ore in MineableAsteroid.All)
        {
            Vector3 d = ore.transform.position - position;
            d.y = 0f;
            smallest = Mathf.Min(smallest, d.magnitude - radius - ore.Radius);
        }

        return smallest;
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

            // 잔해 조각은 카메라와 같은 방향으로 서서 화면을 정면으로 바라보고(카메라가 움직여도 따라간다),
            // 암석은 저마다의 축으로 천천히 구른다
            if (p.isWreck)
                p.transform.rotation = facing * Quaternion.Euler(0f, 0f, p.roll + p.spinPerSecond * time);
            else
                p.transform.rotation = Quaternion.AngleAxis(p.spinPerSecond * time, p.spinAxis) * p.baseRotation;

            if (p.fade < 1f)
            {
                p.fade = Mathf.Min(1f, p.fade + Time.deltaTime / _fadeInSeconds);
                ApplyFade(p);
            }
        }
    }

    /// <summary>새로 놓인 조각이 서서히 나타나게 한다: 잔해는 투명도, 암석(불투명 3D)은 작게 시작해 커진다.</summary>
    private static void ApplyFade(Piece p)
    {
        float k = Mathf.SmoothStep(0f, 1f, p.fade);
        if (p.isWreck)
        {
            p.transform.localScale = Vector3.one * p.baseScale;
            Color c = p.sprite.color;
            c.a = k;
            p.sprite.color = c;
        }
        else
        {
            p.transform.localScale = Vector3.one * (p.baseScale * Mathf.Lerp(0.05f, 1f, k));
        }
    }
}
