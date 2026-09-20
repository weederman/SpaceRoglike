using UnityEngine;

/// <summary>
/// 인벤토리에 들어가는 "터렛 아이템" 데이터. 보상으로 얻는 장비를 표현하는 단위이며,
/// 나중에 인벤토리 대신 보상 선택지 UI에서 써도 되도록 터렛 프리팹 참조만 갖는다.
/// </summary>
[CreateAssetMenu(fileName = "TurretItem", menuName = "SpaceRoglike/Turret Item")]
public class TurretItemData : ScriptableObject
{
    public string displayName;
    public Sprite icon;
    public GameObject turretPrefab;
}
