using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 항성계 지도 영역의 마우스 입력: 휠 = 확대/축소(커서 위치 기준), 드래그 = 이동.
/// 장소 표시(StarMapMarker) 위에서 굴리거나 끌어도 이 영역으로 전달된다. 실제 확대/이동은 StarMapUI가 한다.
/// </summary>
public class StarMapViewport : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler
{
    /// <summary>휠을 굴렸을 때 (화면 좌표, 굴린 양: 위로 굴리면 +)</summary>
    public event Action<Vector2, float> Scrolled;

    /// <summary>끌었을 때 (화면 좌표 이동량)</summary>
    public event Action<Vector2> Dragged;

    public void OnScroll(PointerEventData eventData)
    {
        Scrolled?.Invoke(eventData.position, eventData.scrollDelta.y);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
    }

    public void OnDrag(PointerEventData eventData)
    {
        Dragged?.Invoke(eventData.delta);
    }
}
