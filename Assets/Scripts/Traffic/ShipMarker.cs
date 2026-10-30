using System;
using UnityEngine;

/// <summary>
/// 함선 아래 수평면(XZ)에 작은 원을 그려 편을 알린다. 아군 NPC는 초록, 해적은 빨강.
/// 원은 함선의 자식이 아니라 따로 두고 위치만 따라간다(함선의 배율·기울기와 상관없이 같은 굵기로 보이게).
/// 색·굵기·크기는 함종 목록(EnemyShipCatalog)의 Markers 값을 쓴다.
/// </summary>
public class ShipMarker : MonoBehaviour
{
    public enum Side
    {
        Friendly,
        Hostile
    }

    [Serializable]
    public class Settings
    {
        public Color friendly = new Color(0.25f, 1f, 0.35f, 0.85f);
        public Color hostile = new Color(1f, 0.25f, 0.2f, 0.85f);
        [Tooltip("원의 반지름 = 선체 판정의 가로 반지름 × 이 값")]
        [Min(0.1f)] public float radiusRatio = 1.1f;
        [Tooltip("반지름의 최소값 (작은 함선도 보이게)")]
        [Min(0f)] public float minRadius = 8f;
        [Tooltip("선 굵기 (월드)")]
        [Min(0.01f)] public float width = 1.2f;
        [Tooltip("원을 함선 높이보다 이만큼 아래에 그린다")]
        public float heightOffset = -2f;
        [Range(8, 128)] public int segments = 48;
    }

    private static Material _material;

    private Transform _ring;
    private float _heightOffset;

    /// <summary>함선 루트에 원을 붙인다 (이미 있으면 색만 바꾼다). 설정을 읽을 수 없으면 붙이지 않는다.</summary>
    public static ShipMarker Attach(GameObject ship, Side side)
    {
        EnemyShipCatalog catalog = EnemyShipCatalog.Load();
        Settings settings = catalog != null ? catalog.Markers : null;
        if (ship == null || settings == null)
            return null;

        var marker = ship.GetComponent<ShipMarker>();
        if (marker == null)
            marker = ship.AddComponent<ShipMarker>();

        marker.Build(settings, side);
        return marker;
    }

    private void Build(Settings settings, Side side)
    {
        if (_material == null)
            _material = new Material(Shader.Find("Sprites/Default"));

        LineRenderer line;
        if (_ring == null)
        {
            _ring = new GameObject($"{name}_Marker").transform;
            line = _ring.gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.sharedMaterial = _material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.TransformZ; // 원이 놓인 수평면에 납작하게 (위에서 내려다보는 카메라)
            _ring.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
        else
        {
            line = _ring.GetComponent<LineRenderer>();
        }

        Color color = side == Side.Friendly ? settings.friendly : settings.hostile;
        line.startColor = color;
        line.endColor = color;
        line.widthMultiplier = settings.width;

        float radius = Mathf.Max(settings.minRadius, HullRadius() * settings.radiusRatio);
        line.positionCount = settings.segments;
        for (int i = 0; i < settings.segments; i++)
        {
            float a = i * Mathf.PI * 2f / settings.segments;
            // 링 오브젝트가 X축으로 90도 누워 있으므로 로컬 XY가 월드 XZ가 된다
            line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }

        _heightOffset = settings.heightOffset;
        LateUpdate();
    }

    /// <summary>선체 판정(HullHealth의 콜라이더)의 가로 반지름. 없으면 렌더러 범위.</summary>
    private float HullRadius()
    {
        Physics.SyncTransforms();

        HullHealth hull = GetComponentInChildren<HullHealth>();
        Collider hitbox = hull != null ? hull.GetComponent<Collider>() : null;
        if (hitbox != null)
            return Mathf.Max(hitbox.bounds.extents.x, hitbox.bounds.extents.z);

        Renderer r = GetComponentInChildren<Renderer>();
        return r != null ? Mathf.Max(r.bounds.extents.x, r.bounds.extents.z) : 0f;
    }

    private void LateUpdate()
    {
        if (_ring == null)
            return;

        Vector3 p = transform.position;
        p.y += _heightOffset;
        _ring.position = p;
    }

    private void OnEnable()
    {
        if (_ring != null)
            _ring.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (_ring != null)
            _ring.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_ring != null)
            Destroy(_ring.gameObject);
    }
}
