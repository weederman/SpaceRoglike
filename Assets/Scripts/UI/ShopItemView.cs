using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>정거장 상점의 무기 칸 하나 (SCI-FI UI Pack ItemButton 기반). 아이콘/이름/가격을 보여주고 클릭을 전달한다.</summary>
public class ShopItemView : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _priceText;
    [SerializeField] private Color _priceColor = new Color(1f, 0.85f, 0.35f, 1f);
    [SerializeField] private Color _unaffordableColor = new Color(0.55f, 0.6f, 0.65f, 1f);

    public TurretItemData Item { get; private set; }

    public void Bind(TurretItemData item, Action<TurretItemData> onClick)
    {
        Item = item;
        if (item.icon != null)
            _icon.sprite = item.icon;
        _nameText.text = item.displayName;
        _priceText.text = $"{item.price} 크레딧";
        _button.onClick.AddListener(() => onClick(item));
    }

    /// <summary>크레딧이 모자라면 칸을 어둡게 하고 클릭을 막는다.</summary>
    public void SetAffordable(bool affordable)
    {
        _button.interactable = affordable;
        _priceText.color = affordable ? _priceColor : _unaffordableColor;
    }
}
