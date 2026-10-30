using UnityEngine;

/// <summary>
/// 항성계 배경: 지금 위치에서 가장 가까운 행성과 항성을 "아주 멀리 있는 커다란 천체"로 보여준다(2D Space Kit 행성/항성 그림).
/// 천체는 하늘(스카이박스)처럼 카메라에서 일정한 방향·거리에 놓는다. 방향은 기본 카메라 각도(돌리지 않은 자리)에서 내려다볼 때
/// 함선 평면보다 훨씬 아래 깊은 곳의 시선 중심에서 실제 방향 쪽으로 치우친 곳이고, 실제 방향이 남쪽(기본 카메라 쪽)이면 북쪽으로 올려 놓는다(FarSide).
/// 카메라를 돌려도 천체는 월드에서 제자리라 별 배경과 함께 화면에서 돈다(카메라를 따라오지 않는다). 보이는 크기는 어느 쪽에서 봐도 같다.
/// 함선이 움직여도 카메라를 따라 자리를 다시 잡으므로 하늘처럼 제자리에 있고,
/// 항성계 안에서 위치가 바뀌면 방향과 크기(가까울수록 크게)가 천천히 바뀐다. 날아가다 가장 가까운 행성이 바뀌면 서서히 바꿔 낀다.
/// 워프 터널 무대·정거장 내부처럼 항성계 밖에 있을 때는 숨기고, 그동안은 행성을 고르지 않는다(터널 위치에서 가까운 엉뚱한 행성이 끼지 않게).
/// 워프 도착처럼 순간이동했으면 바꿔 끼는 과정 없이 도착한 곳의 행성으로 바로 바꾼다(예전 행성이 겹쳐 보이다 사라지지 않게).
/// StarSystem 프리팹에 함께 붙는다.
/// </summary>
public class PlanetBackdrop : MonoBehaviour
{
    [SerializeField] private StarSystem _system;

    [Header("배치")]
    [Tooltip("함선 평면 아래로 이만큼 깊은 곳에 둔다")]
    [SerializeField, Min(100f)] private float _depth = 3000f;
    [Tooltip("시선 중심에서 천체의 실제 방향 쪽으로 치우치는 거리 (깊이 기준)")]
    [SerializeField, Min(0f)] private float _spread = 2300f;
    [Tooltip("천체를 북쪽(기본 카메라에서 화면 먼 쪽)으로만 치우치게 하는 정도. 실제 방향이 남쪽이어도 북쪽 성분을 이 값 이상으로 올린다 " +
             "(기본 카메라 쪽으로 오면 화면 아래 함선 곁에 보여 가까이 있는 것처럼 보이므로)")]
    [SerializeField, Range(0f, 1f)] private float _minForward = 0.35f;
    [Tooltip("천체를 카메라에서 이 거리에 놓는다 (하늘처럼, 보이는 크기는 거리와 상관없이 같게 맞춘다). 먼 시야 거리보다 작아야 한다")]
    [SerializeField, Min(1000f)] private float _skyDistance = 9000f;
    [Tooltip("이 깊이까지 보이도록 카메라의 먼 시야 거리를 늘린다 (실행 중에만)")]
    [SerializeField, Min(1000f)] private float _cameraFarClip = 12000f;

    [Header("행성")]
    [Tooltip("행성 그림의 기본 크기 (데이터의 행성 배율을 곱한다)")]
    [SerializeField, Min(10f)] private float _planetSize = 3000f;
    [Tooltip("가장 가까울 때 / 가장 멀 때의 크기 배율")]
    [SerializeField] private Vector2 _planetDistanceScale = new Vector2(1.2f, 0.55f);
    [Tooltip("위 배율을 적용하는 거리 범위 (항성계 단위)")]
    [SerializeField] private Vector2 _planetDistanceRange = new Vector2(2000f, 16000f);
    [SerializeField] private Color _planetTint = new Color(0.9f, 0.9f, 0.95f, 1f);

    [Header("항성")]
    [SerializeField, Min(10f)] private float _starSize = 1100f;
    [SerializeField] private Vector2 _starDistanceScale = new Vector2(1.4f, 0.45f);
    [SerializeField] private Color _starTint = Color.white;

    [SerializeField, Min(0.05f)] private float _fadeSeconds = 1.2f;
    [Tooltip("한 프레임에 이보다 멀리 움직였으면 순간이동(워프 도착 등)으로 보고 행성을 바로 바꾼다 (항성계 단위)")]
    [SerializeField, Min(10f)] private float _teleportDistance = 400f;

    private SpriteRenderer _starRenderer;
    private SpriteRenderer[] _planetRenderers;
    private int[] _planetOrbits = { -1, -1 };
    private float[] _planetAlpha = { 0f, 0f };
    private int _activeSlot;
    private float _visibility;
    private Vector2 _lastHere;
    private bool _wasInside;
    private Transform _player;
    private Camera _cam;

    private void Start()
    {
        if (_system == null)
            _system = GetComponent<StarSystem>();

        _starRenderer = CreateLayer("Star", -320);
        _planetRenderers = new[] { CreateLayer("PlanetA", -310), CreateLayer("PlanetB", -310) };

        var ship = FindObjectOfType<SpaceshipController>();
        _player = ship != null ? ship.transform : null;
    }

    private SpriteRenderer CreateLayer(string layerName, int sortingOrder)
    {
        var go = new GameObject(layerName);
        go.transform.SetParent(transform, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = sortingOrder; // 다른 무엇보다 뒤 (스카이박스 바로 앞)
        renderer.color = new Color(1f, 1f, 1f, 0f);
        return renderer;
    }

    private void LateUpdate()
    {
        if (_system == null || _system.Data == null || _player == null)
            return;

        if (_cam == null)
        {
            _cam = Camera.main;
            if (_cam == null)
                return;
        }

        if (_cam.farClipPlane < _cameraFarClip)
            _cam.farClipPlane = _cameraFarClip;

        bool inside = _system.IsInsideSystem(_player.position);
        Vector2 here = _system.ToSystem(_player.position);

        // 순간이동(정거장 도킹·출항, 워프 도착 등)으로 안팎이 바뀌었으면 서서히 사라지지 않고 바로 숨기거나 보인다
        // (정거장 안으로 들어갈 때 예전 행성이 잠깐 보였다 사라지지 않게)
        bool jumped = (here - _lastHere).magnitude > _teleportDistance;
        if (jumped && inside != _wasInside)
            _visibility = inside ? 1f : 0f;
        else
            _visibility = Mathf.MoveTowards(_visibility, inside ? 1f : 0f, Time.unscaledDeltaTime / _fadeSeconds);

        // 기본 카메라 각도(돌리지 않은 자리, 북쪽을 내려다봄)의 시선 중심이 깊은 평면에 닿는 점 (카메라 기준).
        // 카메라를 돌린 각도는 빼고 내려다보는 각도만 쓰므로, 카메라를 돌려도 천체 방향은 월드에서 그대로다.
        Vector3 forward = _cam.transform.forward;
        float downward = Mathf.Max(0.05f, -forward.y);
        Vector3 view = new Vector3(0f, -downward, Mathf.Sqrt(Mathf.Max(0f, 1f - downward * downward)));
        Vector3 center = view * ((_cam.transform.position.y - (_player.position.y - _depth)) / downward);

        // 항성
        Vector2 toStar = -here;
        float starDistance = toStar.magnitude;
        float starScale = Mathf.Lerp(_starDistanceScale.x, _starDistanceScale.y,
            Mathf.InverseLerp(_planetDistanceRange.x, _system.Data.OuterRadius, starDistance));
        Place(_starRenderer, _system.Data.starSprite, center, toStar, _starSize * starScale, _starTint, _visibility);

        // 가장 가까운 행성. 항성계 밖(터널·정거장 내부)에서는 고르지 않고, 순간이동해 들어왔으면 바로 바꾼다.
        // 날아가다 바뀌면 다른 칸에 띄우고 서서히 바꿔 낀다.
        if (inside)
        {
            bool teleported = !_wasInside || (here - _lastHere).magnitude > _teleportDistance;
            int nearest = NearestPlanet(here);
            if (teleported)
            {
                _planetOrbits[_activeSlot] = nearest;
                _planetOrbits[1 - _activeSlot] = -1;
                _planetAlpha[_activeSlot] = 1f;
                _planetAlpha[1 - _activeSlot] = 0f;
            }
            else if (nearest != _planetOrbits[_activeSlot])
            {
                _activeSlot = 1 - _activeSlot;
                _planetOrbits[_activeSlot] = nearest;
            }
        }

        _lastHere = here;
        _wasInside = inside;

        for (int slot = 0; slot < 2; slot++)
        {
            float target = slot == _activeSlot ? 1f : 0f;
            _planetAlpha[slot] = Mathf.MoveTowards(_planetAlpha[slot], target, Time.unscaledDeltaTime / _fadeSeconds);

            int orbit = _planetOrbits[slot];
            if (orbit < 0 || _planetAlpha[slot] <= 0f)
            {
                _planetRenderers[slot].enabled = false;
                continue;
            }

            StarSystemData.Orbit o = _system.Data.orbits[orbit];
            Vector2 toPlanet = _system.Data.PlanetPosition(orbit) - here;
            float scale = Mathf.Lerp(_planetDistanceScale.x, _planetDistanceScale.y,
                Mathf.InverseLerp(_planetDistanceRange.x, _planetDistanceRange.y, toPlanet.magnitude));

            _planetRenderers[slot].enabled = true;
            Place(_planetRenderers[slot], o.planetSprite, center, toPlanet, _planetSize * o.planetScale * scale,
                _planetTint, _planetAlpha[slot] * _visibility);
        }
    }

    private int NearestPlanet(Vector2 here)
    {
        int best = -1;
        float bestDistance = float.PositiveInfinity;
        StarSystemData data = _system.Data;

        for (int i = 0; i < data.orbits.Length; i++)
        {
            if (data.orbits[i].kind != StarSystemData.OrbitKind.Planet)
                continue;

            float d = (data.PlanetPosition(i) - here).sqrMagnitude;
            if (d < bestDistance)
            {
                bestDistance = d;
                best = i;
            }
        }

        return best;
    }

    /// <summary>
    /// 천체 그림을 시선 중심(center, 카메라 기준)에서 실제 방향 쪽으로 치우친 방향에, 카메라에서 _skyDistance 떨어뜨려 카메라를 향하게 놓는다.
    /// 크기는 기본 카메라 각도에서 그 깊은 곳에 size로 있을 때와 같게 보이도록 맞춘다.
    /// </summary>
    private void Place(SpriteRenderer renderer, Sprite sprite, Vector3 center, Vector2 direction, float size,
        Color tint, float alpha)
    {
        renderer.sprite = sprite;
        if (sprite == null || alpha <= 0f)
        {
            renderer.enabled = false;
            return;
        }

        renderer.enabled = true;

        Vector3 dir = direction.sqrMagnitude > 1f ? FarSide(new Vector3(direction.x, 0f, direction.y).normalized) : Vector3.zero;
        Vector3 offset = center + dir * _spread;
        Vector3 sky = offset.normalized;
        Transform t = renderer.transform;
        t.position = _cam.transform.position + sky * _skyDistance;
        t.rotation = Quaternion.LookRotation(sky, Vector3.up);

        float longSide = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        t.localScale = Vector3.one * (size * _skyDistance / offset.magnitude / longSide);

        tint.a *= alpha;
        renderer.color = tint;
    }

    /// <summary>수평 방향 dir의 북쪽(+Z) 성분이 _minForward보다 작으면(남쪽) 올려서 북쪽으로 돌린다.</summary>
    private Vector3 FarSide(Vector3 dir)
    {
        if (dir.z >= _minForward)
            return dir;

        return new Vector3(dir.x, 0f, _minForward).normalized;
    }
}
