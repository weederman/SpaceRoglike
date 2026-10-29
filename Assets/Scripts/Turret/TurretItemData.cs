using UnityEngine;
using FORGE3D;

/// <summary>
/// 무기(터렛) 아이템 데이터. 상점에서 사고 격납고에 보관·장착하는 단위이며,
/// 터렛 프리팹 참조와 이름·아이콘·가격·설명만 갖는다(공격력 등 전투 수치는 WeaponStatsTable).
/// Turret1 프리팹은 모든 무기 타입의 참조를 이미 갖고 있으므로, 무기 종류별로
/// 프리팹을 따로 두지 않고 장착 시점에 weaponType으로 F3DFXController를 설정한다.
/// 전체 목록은 WeaponCatalog (Assets/Resources/Shop/WeaponCatalog.asset).
/// </summary>
[CreateAssetMenu(fileName = "TurretItem", menuName = "SpaceRoglike/Turret Item")]
public class TurretItemData : ScriptableObject
{
    public string displayName;
    public Sprite icon;
    public GameObject turretPrefab;
    public F3DFXType weaponType;
    [Min(0)] public int price = 100;
    [Tooltip("상점에서 파는지 (기본 장비처럼 팔지 않는 무기도 격납고에는 보인다)")]
    public bool forSale = true;
    [Tooltip("격납고·상점의 '설명' 탭에 보이는 글")]
    [TextArea(2, 6)] public string description;
}
