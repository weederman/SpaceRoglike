using UnityEngine;
using FORGE3D;

/// <summary>
/// 상점에서 파는 "터렛 아이템" 데이터. 구매할 수 있는 장비(무기)를 표현하는 단위이며,
/// 터렛 프리팹 참조와 가격·아이콘만 갖는다(공격력 등 전투 수치는 WeaponStatsTable).
/// Turret1 프리팹은 모든 무기 타입의 참조를 이미 갖고 있으므로, 무기 종류별로
/// 프리팹을 따로 두지 않고 장착 시점에 weaponType으로 F3DFXController를 설정한다.
/// </summary>
[CreateAssetMenu(fileName = "TurretItem", menuName = "SpaceRoglike/Turret Item")]
public class TurretItemData : ScriptableObject
{
    public string displayName;
    public Sprite icon;
    public GameObject turretPrefab;
    public F3DFXType weaponType;
    [Min(0)] public int price = 100;
}
