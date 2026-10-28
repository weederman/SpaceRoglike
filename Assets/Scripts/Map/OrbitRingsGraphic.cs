using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 항성계 지도의 궤도 원들을 그리는 UI 그래픽. 고리(안쪽 반지름 ~ 바깥 반지름) 여러 개를 하나의 메시로 그린다.
/// 행성 궤도는 가는 선, 소행성 벨트는 폭이 있는 반투명 띠로 쓴다. 클릭은 받지 않는다.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class OrbitRingsGraphic : MaskableGraphic
{
    private const int Segments = 160;

    private struct Ring
    {
        public float inner;
        public float outer;
        public Color color;
    }

    private readonly List<Ring> _rings = new List<Ring>();
    private Vector2 _offset;

    /// <summary>고리들의 중심을 사각형 가운데에서 이만큼 옮긴다 (지도 이동)</summary>
    public Vector2 Offset
    {
        get => _offset;
        set
        {
            _offset = value;
            SetVerticesDirty();
        }
    }

    public void Clear()
    {
        _rings.Clear();
        SetVerticesDirty();
    }

    /// <summary>중심에서 radius(픽셀) 거리에 width(픽셀) 굵기의 고리를 더한다</summary>
    public void AddRing(float radius, float width, Color ringColor)
    {
        float half = Mathf.Max(0.5f, width * 0.5f);
        _rings.Add(new Ring { inner = Mathf.Max(0f, radius - half), outer = radius + half, color = ringColor });
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Vector2 center = rectTransform.rect.center + _offset;

        foreach (Ring ring in _rings)
        {
            int start = vh.currentVertCount;
            Color32 c = ring.color * color;

            for (int i = 0; i <= Segments; i++)
            {
                float a = i / (float)Segments * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                vh.AddVert(center + dir * ring.inner, c, Vector2.zero);
                vh.AddVert(center + dir * ring.outer, c, Vector2.zero);
            }

            for (int i = 0; i < Segments; i++)
            {
                int v = start + i * 2;
                vh.AddTriangle(v, v + 1, v + 3);
                vh.AddTriangle(v, v + 3, v + 2);
            }
        }
    }
}
