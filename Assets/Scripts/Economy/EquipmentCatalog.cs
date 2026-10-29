using System;
using UnityEngine;

/// <summary>
/// 함선 장비 전체 목록(단일 출처). 위치: Assets/Resources/Shop/EquipmentCatalog.asset
/// 기획 가격·성능표(docs/game-design.md 2.3)의 5종 × 4단계. 효과는 모두 함선 기본값에 대한 % 증가다(bonusPercent).
///  - 강화 장갑: 선체 최대 체력 +%
///  - 보호막 증폭기: 보호막 용량·재충전 +%. 함선에 기본 보호막이 없으므로(장착해야 보호막이 생긴다) 함선의 보호막 기준값(ShipCatalog)에 %를 더한 보호막을 단다
///  - 추진기 개량: 가속 +%
///  - 화물창 확장: 화물칸 용량 +%
///  - 채굴 장비 개량: 채굴 속도 +%
/// 같은 종류는 하나만 단다(등급끼리 합산하지 않고 교체). 함선의 장비 칸 수(ShipCatalog.equipmentSlots)만큼 종류를 고를 수 있다.
/// 장착·보관 상태는 PlayerEquipment가 갖는다.
/// </summary>
[CreateAssetMenu(fileName = "EquipmentCatalog", menuName = "SpaceRoglike/Equipment Catalog")]
public class EquipmentCatalog : ScriptableObject
{
    public const string ResourcePath = "Shop/EquipmentCatalog";

    public enum Kind { Armor, Shield, Thruster, Cargo, Mining }

    public static readonly Kind[] AllKinds = { Kind.Armor, Kind.Shield, Kind.Thruster, Kind.Cargo, Kind.Mining };

    [Serializable]
    public class Item
    {
        [Tooltip("저장·장착에서 부르는 이름 (예: armor_1)")]
        public string id;
        public Kind kind;
        [Tooltip("등급 (1~4)")]
        [Range(1, 4)] public int tier = 1;
        public string displayName;
        public Sprite icon;
        [Min(0)] public int price = 1000;
        [Tooltip("상점에서 파는지")]
        public bool forSale = true;
        [TextArea(2, 6)] public string description;

        [Tooltip("함선 기본값에 더하는 비율 (%)")]
        [Min(0f)] public float bonusPercent = 10f;
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
        switch (kind)
        {
            case Kind.Armor: return "강화 장갑";
            case Kind.Shield: return "보호막 증폭기";
            case Kind.Thruster: return "추진기 개량";
            case Kind.Cargo: return "화물창 확장";
            default: return "채굴 장비 개량";
        }
    }

    /// <summary>효과가 무엇에 붙는지 (예: "선체 내구도")</summary>
    public static string EffectName(Kind kind)
    {
        switch (kind)
        {
            case Kind.Armor: return "선체 내구도";
            case Kind.Shield: return "보호막 용량";
            case Kind.Thruster: return "가속";
            case Kind.Cargo: return "화물 용량";
            default: return "채굴 속도";
        }
    }

    /// <summary>"선체 내구도 +10%"</summary>
    public static string EffectLine(Item item)
    {
        return item != null ? $"{EffectName(item.kind)} +{item.bonusPercent:0.#}%" : "";
    }
}
