using FORGE3D;
using UnityEngine;

/// <summary>
/// 게임에 있는 무기 아이템 전체 목록(단일 출처). 위치: Assets/Resources/Shop/WeaponCatalog.asset
/// 상점은 이 중 forSale인 것을 팔고, 격납고는 무기 종류로 아이템(이름·아이콘·설명)을 찾는다.
/// 플레이어가 보관 중인 무기는 상점에 되팔 수 있다 (가격 × sellRatio, 기본 장비인 발칸은 가격이 0이라 0 CR).
/// </summary>
[CreateAssetMenu(fileName = "WeaponCatalog", menuName = "SpaceRoglike/Weapon Catalog")]
public class WeaponCatalog : ScriptableObject
{
    public const string ResourcePath = "Shop/WeaponCatalog";

    public TurretItemData[] weapons = new TurretItemData[0];

    [Tooltip("보관 중인 무기를 상점에 되팔 때 받는 값 (가격 대비)")]
    [Range(0f, 1f)] public float sellRatio = 0.5f;

    private static WeaponCatalog _loaded;

    public static WeaponCatalog Load()
    {
        if (_loaded == null)
            _loaded = Resources.Load<WeaponCatalog>(ResourcePath);
        return _loaded;
    }

    /// <summary>상점에 되팔 때 받는 값</summary>
    public int SellPrice(TurretItemData item)
    {
        return item != null ? Mathf.FloorToInt(item.price * sellRatio) : 0;
    }

    /// <summary>무기 종류의 아이템 (없으면 null)</summary>
    public TurretItemData Find(F3DFXType type)
    {
        foreach (TurretItemData item in weapons)
        {
            if (item != null && item.weaponType == type)
                return item;
        }

        return null;
    }
}
