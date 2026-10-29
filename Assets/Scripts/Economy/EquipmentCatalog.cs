using System;
using UnityEngine;

/// <summary>
/// 함선 장비(장갑·쉴드) 전체 목록(단일 출처). 위치: Assets/Resources/Shop/EquipmentCatalog.asset
///  - 장갑: 선체 최대 체력을 더한다 (함선 기본 선체 + hullBonus)
///  - 쉴드: 쉴드를 단다 (최대 체력, 피격이 없으면 rechargeDelay초 뒤부터 초당 rechargePerSecond씩 재충전)
/// 함선은 장갑 칸 하나·쉴드 칸 하나를 갖고, 처음에는 둘 다 비어 있다(쉴드 없음). 상점에서 사서 격납고에서 장착한다.
/// 장착·보관 상태는 PlayerEquipment가 갖는다.
/// </summary>
[CreateAssetMenu(fileName = "EquipmentCatalog", menuName = "SpaceRoglike/Equipment Catalog")]
public class EquipmentCatalog : ScriptableObject
{
    public const string ResourcePath = "Shop/EquipmentCatalog";

    public enum Kind { Armor, Shield }

    [Serializable]
    public class Item
    {
        [Tooltip("저장·장착에서 부르는 이름 (예: armor_1)")]
        public string id;
        public Kind kind;
        public string displayName;
        public Sprite icon;
        [Min(0)] public int price = 1000;
        [Tooltip("상점에서 파는지")]
        public bool forSale = true;
        [TextArea(2, 6)] public string description;

        [Header("장갑")]
        [Tooltip("선체 최대 체력에 더하는 값")]
        [Min(0f)] public float hullBonus;

        [Header("쉴드")]
        [Min(0f)] public float shieldHp;
        [Tooltip("마지막으로 맞은 뒤 재충전이 시작되기까지 (초)")]
        [Min(0f)] public float rechargeDelay = 4f;
        [Tooltip("재충전 속도 (초당 쉴드 체력)")]
        [Min(0f)] public float rechargePerSecond;
    }

    public Item[] items = new Item[0];

    [Tooltip("보관 중인 장비를 상점에 되팔 때 받는 값 (가격 대비)")]
    [Range(0f, 1f)] public float sellRatio = 0.5f;

    private static EquipmentCatalog _loaded;

    public static EquipmentCatalog Load()
    {
        if (_loaded == null)
            _loaded = Resources.Load<EquipmentCatalog>(ResourcePath);
        return _loaded;
    }

    /// <summary>id의 장비 (없으면 null)</summary>
    public Item Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        foreach (Item item in items)
        {
            if (item != null && item.id == id)
                return item;
        }

        return null;
    }

    public int SellPrice(Item item)
    {
        return item != null ? Mathf.FloorToInt(item.price * sellRatio) : 0;
    }

    public static string KindName(Kind kind)
    {
        return kind == Kind.Armor ? "장갑" : "쉴드";
    }
}
