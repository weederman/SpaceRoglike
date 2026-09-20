using UnityEngine;
using FORGE3D;

/// <summary>
/// 인벤토리에 들어가는 "터렛 아이템" 데이터. 보상으로 얻는 장비를 표현하는 단위이며,
/// 나중에 인벤토리 대신 보상 선택지 UI에서 써도 되도록 터렛 프리팹 참조만 갖는다.
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
}
