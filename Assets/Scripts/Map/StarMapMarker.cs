using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 항성계 지도 위의 장소 표시 하나(정거장/워프 비콘). 마우스를 올리면 강조되고, 클릭하면 선택을 알린다.
/// 모양은 StarMap 프리팹의 표시 견본이 정하고, StarMapUI가 복제해 Bind로 장소를 정한다.
/// </summary>
public class StarMapMarker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private Image _icon;
    [SerializeField] private Graphic _highlight;
    [Tooltip("비콘의 레벨 숫자 등 (없어도 된다)")]
    [SerializeField] private TMPro.TMP_Text _label;

    public StarSystem.Location Location { get; private set; }

    public event Action<StarMapMarker> Clicked;
    public event Action<StarMapMarker> Hovered;

    private bool _selected;
    private bool _hover;
    private Vector3 _baseScale = Vector3.one;

    public void Bind(StarSystem.Location location, Color color, string label)
    {
        Location = location;
        _baseScale = transform.localScale;

        if (_icon != null)
            _icon.color = color;

        if (_label != null)
        {
            GameFonts.ApplyKorean(_label);
            _label.text = label ?? "";
        }

        Refresh();
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        Refresh();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hover = true;
        Refresh();
        Hovered?.Invoke(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hover = false;
        Refresh();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            Clicked?.Invoke(this);
    }

    private void Refresh()
    {
        if (_highlight != null)
            _highlight.enabled = _selected || _hover;

        transform.localScale = _baseScale * (_selected ? 1.3f : _hover ? 1.15f : 1f);
    }
}
