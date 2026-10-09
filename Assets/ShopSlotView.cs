using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>상점 그리드의 칸 하나. 무기 아이콘/이름/가격을 보여주고 클릭을 전달한다.</summary>
public class ShopSlotView : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private Text _nameText;
    [SerializeField] private Text _priceText;
    [SerializeField] private Button _button;

    public TurretItemData Item { get; private set; }

    public void Bind(TurretItemData item, Action<TurretItemData> onClick)
    {
        Item = item;
        _icon.sprite = item.icon;
        _icon.enabled = item.icon != null;
        _nameText.text = item.displayName;
        _priceText.text = item.price + " 크레딧";
        _button.onClick.AddListener(() => onClick(item));
    }

    /// <summary>크레딧이 모자라면 칸을 어둡게 하고 클릭을 막는다.</summary>
    public void SetAffordable(bool affordable)
    {
        _button.interactable = affordable;
    }
}
