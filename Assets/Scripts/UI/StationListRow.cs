using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 정거장 창의 목록 한 줄 (아이콘 + 이름 + 보조 설명 + 오른쪽 글). 누르면 선택을 알리고, 선택된 줄은 배경이 바뀐다.
/// StationListView가 견본을 복제해 쓴다.
/// </summary>
public class StationListRow : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _background;
    [SerializeField] private Sprite _normalSprite;
    [SerializeField] private Sprite _selectedSprite;
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _subtitle;
    [SerializeField] private TMP_Text _right;

    private Action _onClick;

    private void Awake()
    {
        _button.onClick.AddListener(() => _onClick?.Invoke());
    }

    public void Bind(Sprite icon, string title, string subtitle, string right, Action onClick)
    {
        _icon.sprite = icon;
        _icon.enabled = icon != null;
        _title.text = title;
        _subtitle.text = subtitle ?? "";
        _right.text = right ?? "";
        _onClick = onClick;
        _button.interactable = onClick != null;
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        _background.sprite = selected && _selectedSprite != null ? _selectedSprite : _normalSprite;
    }
}
