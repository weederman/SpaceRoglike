using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 우주정거장 하나. 플레이어가 이 정거장에 도킹해 있는 동안 "안에 있음"(IsInside) 상태가 되고,
/// 그동안 정거장 화면(StationInteriorView)이 열리고 자동 저장(SaveSystem)이 된다. 도킹/출항은 StationDocking이 정한다.
/// 정거장은 여러 개일 수 있다(항성계의 행성 궤도마다 하나). 씬에 놓인 정거장이 모항(Home, 튜토리얼 정거장)이고,
/// 항성계(StarSystem)가 런타임에 만드는 나머지 정거장은 Init으로 이름을 정한다.
/// 위치는 이 오브젝트의 위치이고, 크기는 StarSystem이 붙이는 정거장 모델(Model)의 범위다.
/// </summary>
public class StationZone : MonoBehaviour
{
    /// <summary>지금 있는 정거장 전부</summary>
    public static readonly List<StationZone> All = new List<StationZone>();

    /// <summary>어느 정거장이든 도킹했을 때 / 출항했을 때</summary>
    public static event Action<StationZone> AnyEntered;
    public static event Action<StationZone> AnyExited;

    /// <summary>씬에 놓인 모항 정거장 (튜토리얼 정거장). 없으면 null.</summary>
    public static StationZone Home
    {
        get
        {
            foreach (StationZone zone in All)
            {
                if (zone.IsHome)
                    return zone;
            }

            return null;
        }
    }

    /// <summary>플레이어가 도킹해 있는 정거장 (없으면 null)</summary>
    public static StationZone Current
    {
        get
        {
            foreach (StationZone zone in All)
            {
                if (zone.IsInside)
                    return zone;
            }

            return null;
        }
    }

    public event Action Entered;
    public event Action Exited;

    /// <summary>플레이어가 이 정거장에 도킹해 있는지</summary>
    public bool IsInside { get; private set; }

    public bool IsHome { get; private set; } = true;

    /// <summary>정거장 이름 (항성계 데이터에서 받는다. 모항은 StarSystem이 채운다)</summary>
    public string DisplayName { get; set; }

    /// <summary>항성계 데이터의 정거장 번호 (StarSystem이 채운다, 없으면 -1). 저장에 쓴다.</summary>
    public int StationIndex { get; set; } = -1;

    /// <summary>정거장 모델 (도킹 거리를 재는 데 쓴다. StarSystem이 채운다)</summary>
    public Transform Model
    {
        get => _model;
        set
        {
            _model = value;
            _hasBounds = false;
            _hullDistance = null;
            _footprintFailed = false;
        }
    }

    /// <summary>정거장 입구 쪽 (모델 앞쪽, 수평). 출항한 함선이 이쪽 바깥에 나온다.</summary>
    public Vector3 EntranceDirection
    {
        get
        {
            Vector3 dir = _model != null ? _model.forward : Vector3.forward;
            dir.y = 0f;
            return dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
        }
    }

    private Transform _model;
    private Bounds _bounds;
    private bool _hasBounds;

    /// <summary>정거장 모델이 차지하는 범위 (월드). 모델이 없으면 false. 정거장은 움직이지 않으므로 한 번 재고 기억한다.</summary>
    public bool TryGetBounds(out Bounds bounds)
    {
        if (!_hasBounds && _model != null)
        {
            bool any = false;
            foreach (Renderer r in _model.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { _bounds = r.bounds; any = true; }
                else _bounds.Encapsulate(r.bounds);
            }

            _hasBounds = any;
        }

        bounds = _bounds;
        return _hasBounds;
    }

    /// <summary>
    /// 정거장 선체 바깥, direction(수평) 쪽으로 선체 끝에서 clearance만큼 더 떨어진 곳 (높이 y).
    /// 정거장 모델이 아주 커서 중심에서 일정 거리로 재면 선체 안에 들어가므로, 워프 도착·출항·귀환 자리는 이것으로 정한다.
    /// fromHull이면 위에서 내려다본 실제 선체 모양(Footprint)의 끝에서 잰다. 아니면 모델 범위 상자의 끝에서 잰다
    /// (정거장이 십자 모양이라 상자 끝은 중심선의 실제 선체보다 1,500~2,000쯤 바깥이다).
    /// </summary>
    public Vector3 PointOutside(Vector3 direction, float clearance, float y, bool fromHull = true)
    {
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.back;

        Vector3 center = transform.position;
        float extent = 0f;
        if (TryGetBounds(out Bounds b))
        {
            center = b.center;
            extent = Mathf.Abs(direction.x) * b.extents.x + Mathf.Abs(direction.z) * b.extents.z;
            if (fromHull && EnsureFootprint())
                extent = HullExtent(center, direction, extent);
        }

        Vector3 p = center + direction * (extent + clearance);
        p.y = y;
        return p;
    }

    /// <summary>
    /// world에서 정거장 선체까지의 수평 거리. 위에서 내려다본 실제 선체 모양(Footprint)으로 잰다(선체 위면 0).
    /// 모델을 읽을 수 없으면 모델 범위 상자까지의 거리.
    /// </summary>
    public float DistanceToHull(Vector3 world)
    {
        if (!TryGetBounds(out Bounds b))
            return float.PositiveInfinity;

        if (!EnsureFootprint())
        {
            float dx = Mathf.Max(b.min.x - world.x, 0f, world.x - b.max.x);
            float dz = Mathf.Max(b.min.z - world.z, 0f, world.z - b.max.z);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // 격자 밖이면 격자 가장자리까지 + 그 칸에서 선체까지
        float gx = (world.x - _footprintOrigin.x) / FootprintCell;
        float gz = (world.z - _footprintOrigin.y) / FootprintCell;
        int cx = Mathf.Clamp(Mathf.FloorToInt(gx), 0, _footprintW - 1);
        int cz = Mathf.Clamp(Mathf.FloorToInt(gz), 0, _footprintH - 1);
        float outside = new Vector2(gx - Mathf.Clamp(gx, 0f, _footprintW), gz - Mathf.Clamp(gz, 0f, _footprintH)).magnitude;

        float cells = _hullDistance[cz * _footprintW + cx];
        if (cells <= 0f && outside <= 0f)
            return 0f;
        // 칸 중심끼리의 거리이므로 선체 칸의 가장자리까지는 반 칸 가깝다
        return Mathf.Max(0f, (cells + outside - 0.5f) * FootprintCell);
    }

    // ── 위에서 내려다본 선체 모양 ─────────────────────────
    // 정거장 모델의 삼각형을 수평면(XZ)에 눕혀 격자 칸에 칠하고, 칸마다 가장 가까운 선체 칸까지의 거리를 미리 재 둔다.
    // 정거장은 움직이지 않으므로 처음 쓸 때 한 번만 만든다.

    private const float FootprintCell = 20f;

    private float[] _hullDistance; // 칸 단위 거리 (선체 칸은 0)
    private int _footprintW, _footprintH;
    private Vector2 _footprintOrigin;
    private bool _footprintFailed;

    private bool EnsureFootprint()
    {
        if (_hullDistance != null)
            return true;
        if (_footprintFailed || _model == null || !TryGetBounds(out Bounds b))
            return false;

        _footprintOrigin = new Vector2(b.min.x, b.min.z) - Vector2.one * FootprintCell;
        _footprintW = Mathf.CeilToInt(b.size.x / FootprintCell) + 3;
        _footprintH = Mathf.CeilToInt(b.size.z / FootprintCell) + 3;
        var solid = new bool[_footprintW * _footprintH];

        bool any = false;
        foreach (MeshFilter filter in _model.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null || !mesh.isReadable)
                continue;

            Matrix4x4 toWorld = filter.transform.localToWorldMatrix;
            Vector3[] vertices = mesh.vertices;
            var flat = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 w = toWorld.MultiplyPoint3x4(vertices[i]);
                flat[i] = new Vector2((w.x - _footprintOrigin.x) / FootprintCell, (w.z - _footprintOrigin.y) / FootprintCell);
            }

            int[] triangles = mesh.triangles;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
                FillTriangle(solid, flat[triangles[i]], flat[triangles[i + 1]], flat[triangles[i + 2]]);
            any = true;
        }

        if (!any)
        {
            _footprintFailed = true;
            return false;
        }

        _hullDistance = DistanceField(solid);
        return true;
    }

    /// <summary>삼각형(칸 단위 좌표)이 덮는 칸을 칠한다. 아주 가는 삼각형도 빠지지 않게 변을 따라서도 칠한다.</summary>
    private void FillTriangle(bool[] solid, Vector2 a, Vector2 b, Vector2 c)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
        int maxX = Mathf.Min(_footprintW - 1, Mathf.FloorToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
        int minZ = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
        int maxZ = Mathf.Min(_footprintH - 1, Mathf.FloorToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));

        float area = Cross(b - a, c - a);
        if (Mathf.Abs(area) > 0.0001f)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var p = new Vector2(x + 0.5f, z + 0.5f);
                    float w0 = Cross(b - a, p - a) / area;
                    float w1 = Cross(c - b, p - b) / area;
                    float w2 = Cross(a - c, p - c) / area;
                    if (w0 >= 0f && w1 >= 0f && w2 >= 0f)
                        solid[z * _footprintW + x] = true;
                }
            }
        }

        FillEdge(solid, a, b);
        FillEdge(solid, b, c);
        FillEdge(solid, c, a);
    }

    private void FillEdge(bool[] solid, Vector2 from, Vector2 to)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) * 2f));
        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(from, to, (float)i / steps);
            int x = Mathf.FloorToInt(p.x);
            int z = Mathf.FloorToInt(p.y);
            if (x >= 0 && x < _footprintW && z >= 0 && z < _footprintH)
                solid[z * _footprintW + x] = true;
        }
    }

    private static float Cross(Vector2 u, Vector2 v)
    {
        return u.x * v.y - u.y * v.x;
    }

    /// <summary>칸마다 가장 가까운 선체 칸까지의 거리 (칸 단위, 대각선 √2로 두 번 훑는 근사)</summary>
    private float[] DistanceField(bool[] solid)
    {
        const float diagonal = 1.41421356f;
        int w = _footprintW, h = _footprintH;
        var d = new float[w * h];
        for (int i = 0; i < d.Length; i++)
            d[i] = solid[i] ? 0f : float.PositiveInfinity;

        for (int z = 0; z < h; z++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = z * w + x;
                if (x > 0) d[i] = Mathf.Min(d[i], d[i - 1] + 1f);
                if (z > 0)
                {
                    d[i] = Mathf.Min(d[i], d[i - w] + 1f);
                    if (x > 0) d[i] = Mathf.Min(d[i], d[i - w - 1] + diagonal);
                    if (x < w - 1) d[i] = Mathf.Min(d[i], d[i - w + 1] + diagonal);
                }
            }
        }

        for (int z = h - 1; z >= 0; z--)
        {
            for (int x = w - 1; x >= 0; x--)
            {
                int i = z * w + x;
                if (x < w - 1) d[i] = Mathf.Min(d[i], d[i + 1] + 1f);
                if (z < h - 1)
                {
                    d[i] = Mathf.Min(d[i], d[i + w] + 1f);
                    if (x < w - 1) d[i] = Mathf.Min(d[i], d[i + w + 1] + diagonal);
                    if (x > 0) d[i] = Mathf.Min(d[i], d[i + w - 1] + diagonal);
                }
            }
        }

        return d;
    }

    /// <summary>center에서 direction 쪽으로 나가며 마지막으로 선체가 있는 곳까지의 거리 (없으면 boxExtent)</summary>
    private float HullExtent(Vector3 center, Vector3 direction, float boxExtent)
    {
        float step = FootprintCell * 0.5f;
        for (float t = boxExtent + FootprintCell; t >= 0f; t -= step)
        {
            Vector3 p = center + direction * t;
            int x = Mathf.FloorToInt((p.x - _footprintOrigin.x) / FootprintCell);
            int z = Mathf.FloorToInt((p.z - _footprintOrigin.y) / FootprintCell);
            if (x >= 0 && x < _footprintW && z >= 0 && z < _footprintH && _hullDistance[z * _footprintW + x] <= 0f)
                return t + step;
        }

        return boxExtent;
    }

    // 에디터에서 도메인 리로드를 꺼 둬도 플레이마다 깨끗이 시작하도록
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        All.Clear();
        AnyEntered = null;
        AnyExited = null;
    }

    /// <summary>런타임에 만든 정거장의 이름을 정한다(모항이 아님).</summary>
    public void Init(string displayName)
    {
        IsHome = false;
        DisplayName = displayName;
    }

    /// <summary>도킹(true) / 출항(false). StationDocking이 부른다.</summary>
    public void SetDocked(bool docked)
    {
        if (docked == IsInside)
            return;

        IsInside = docked;
        if (docked)
        {
            Entered?.Invoke();
            AnyEntered?.Invoke(this);
        }
        else
        {
            Exited?.Invoke();
            AnyExited?.Invoke(this);
        }
    }

    private void OnEnable()
    {
        All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
        SetDocked(false);
    }
}
