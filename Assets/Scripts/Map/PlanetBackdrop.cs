using UnityEngine;

/// <summary>
/// 항성계 배경: 지금 위치에서 가장 가까운 행성과 항성을 "아주 멀리 있는 커다란 천체"로 보여준다(2D Space Kit 행성/항성 그림).
/// 천체는 함선이 다니는 평면보다 훨씬 아래 깊은 곳에, 카메라가 내려다보는 방향을 기준으로 실제 방향 쪽으로 치우쳐 놓는다.
/// 매 프레임 카메라 시선을 따라 자리를 다시 잡으므로 함선이 움직여도 하늘처럼 거의 제자리에 있고,
/// 항성계 안에서 위치가 바뀌면 방향과 크기(가까울수록 크게)가 천천히 바뀐다. 가장 가까운 행성이 바뀌면 서서히 바꿔 낀다.
/// 워프 터널 무대처럼 항성계 밖에 있을 때는 숨긴다. StarSystem 프리팹에 함께 붙는다.
/// </summary>
public class PlanetBackdrop : MonoBehaviour
{
    [SerializeField] private StarSystem _system;

    [Header("배치")]
    [Tooltip("함선 평면 아래로 이만큼 깊은 곳에 둔다")]
    [SerializeField, Min(100f)] private float _depth = 3000f;
    [Tooltip("시선 중심에서 천체의 실제 방향 쪽으로 치우치는 거리 (깊이 기준)")]
    [SerializeField, Min(0f)] private float _spread = 2300f;
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

    private SpriteRenderer _starRenderer;
    private SpriteRenderer[] _planetRenderers;
    private int[] _planetOrbits = { -1, -1 };
    private float[] _planetAlpha = { 0f, 0f };
    private int _activeSlot;
    private float _visibility;
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
        _visibility = Mathf.MoveTowards(_visibility, inside ? 1f : 0f, Time.unscaledDeltaTime / _fadeSeconds);

        // 시선 중심이 깊은 평면에 닿는 점 (카메라는 앞쪽 아래를 내려다본다)
        float planeY = _player.position.y - _depth;
        Vector3 forward = _cam.transform.forward;
        Vector3 center;
        if (forward.y < -0.05f)
            center = _cam.transform.position + forward * ((_cam.transform.position.y - planeY) / -forward.y);
        else
            center = new Vector3(_player.position.x, planeY, _player.position.z);

        Vector2 here = _system.ToSystem(_player.position);
        Quaternion facing = _cam.transform.rotation;

        // 항성
        Vector2 toStar = -here;
        float starDistance = toStar.magnitude;
        float starScale = Mathf.Lerp(_starDistanceScale.x, _starDistanceScale.y,
            Mathf.InverseLerp(_planetDistanceRange.x, _system.Data.OuterRadius, starDistance));
        Place(_starRenderer, _system.Data.starSprite, center, toStar, _starSize * starScale, facing, _starTint, _visibility);

        // 가장 가까운 행성 (바뀌면 다른 칸에 띄우고 서서히 바꿔 낀다)
        int nearest = NearestPlanet(here);
        if (nearest != _planetOrbits[_activeSlot])
        {
            _activeSlot = 1 - _activeSlot;
            _planetOrbits[_activeSlot] = nearest;
        }

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
            Place(_planetRenderers[slot], o.planetSprite, center, toPlanet, _planetSize * o.planetScale * scale, facing,
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

    /// <summary>천체 그림을 시선 중심에서 실제 방향 쪽으로 치우친 깊은 곳에, 카메라를 향하게 놓는다.</summary>
    private void Place(SpriteRenderer renderer, Sprite sprite, Vector3 center, Vector2 direction, float size,
        Quaternion facing, Color tint, float alpha)
    {
        renderer.sprite = sprite;
        if (sprite == null || alpha <= 0f)
        {
            renderer.enabled = false;
            return;
        }

        renderer.enabled = true;

        Vector2 dir = direction.sqrMagnitude > 1f ? direction.normalized : Vector2.zero;
        Transform t = renderer.transform;
        t.position = center + new Vector3(dir.x, 0f, dir.y) * _spread;
        t.rotation = facing;

        float longSide = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        t.localScale = Vector3.one * (size / longSide);

        tint.a *= alpha;
        renderer.color = tint;
    }
}
