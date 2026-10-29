using System;
using UnityEngine;

/// <summary>
/// 플레이어가 가질 수 있는 함선 전체 목록(단일 출처). 위치: Assets/Resources/Ships/ShipCatalog.asset
/// 함선마다 겉모습(모델·이름·아이콘·설명), 무기 슬롯 수(= 터렛 수), 장비 칸 수, 기본 성능(선체·속도·가속·보호막 기준값)을 둔다.
///  - 함선 프리팹의 터렛은 2개다. 무기 슬롯이 더 많은 함선은 extraTurretMounts 자리에 터렛을 더 단다.
///  - 장비 효과(%)는 이 기본값에 더한다 (PlayerEquipment).
/// 보유·탑승 상태는 PlayerShips가 갖는다.
/// </summary>
[CreateAssetMenu(fileName = "ShipCatalog", menuName = "SpaceRoglike/Ship Catalog")]
public class ShipCatalog : ScriptableObject
{
    public const string ResourcePath = "Ships/ShipCatalog";

    [Serializable]
    public class Ship
    {
        [Tooltip("저장·지급에서 부르는 이름 (예: specter)")]
        public string id;
        [Tooltip("함급 (코르벳 / 프리깃 / 디스트로이어)")]
        public string shipClass;
        [Tooltip("겉모습과 격납고에 보이는 이름·아이콘·설명")]
        public PlayerShipAppearance.LookSettings look = new PlayerShipAppearance.LookSettings();

        [Header("슬롯")]
        [Tooltip("무기 슬롯 수 (= 터렛 수)")]
        [Min(1)] public int weaponSlots = 2;
        [Tooltip("장비 칸 수 (같은 종류는 하나)")]
        [Min(1)] public int equipmentSlots = 4;
        [Tooltip("기본 터렛 2개 말고 더 다는 터렛 자리 (함선 기준 로컬 위치). 무기 슬롯 - 2 개")]
        public Vector3[] extraTurretMounts = new Vector3[0];

        [Header("기본 성능 (장비 %는 여기에 더한다)")]
        [Min(1f)] public float hullHp = 1000f;
        [Min(1f)] public float maxSpeed = 60f;
        [Min(0.1f)] public float acceleration = 30f;
        [Tooltip("보호막 증폭기를 달았을 때 보호막 용량의 기준")]
        [Min(1f)] public float shieldBaseHp = 600f;
        [Tooltip("마지막으로 맞은 뒤 보호막 재충전이 시작되기까지 (초)")]
        [Min(0f)] public float shieldRechargeDelay = 4f;
        [Tooltip("보호막 재충전 속도의 기준 (초당)")]
        [Min(0f)] public float shieldRechargePerSecond = 50f;

        public string DisplayName => look != null && !string.IsNullOrEmpty(look.displayName) ? look.displayName : id;
    }

    [Tooltip("처음 가진 함선 (프롤로그에서 받는 코르벳)")]
    public string defaultShip = "specter";
    public Ship[] ships = new Ship[0];

    private static ShipCatalog _loaded;

    public static ShipCatalog Load()
    {
        if (_loaded == null)
            _loaded = Resources.Load<ShipCatalog>(ResourcePath);
        return _loaded;
    }

    /// <summary>id의 함선 (없으면 null)</summary>
    public Ship Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        foreach (Ship ship in ships)
        {
            if (ship != null && ship.id == id)
                return ship;
        }

        return null;
    }

    public Ship Default => Find(defaultShip) ?? (ships.Length > 0 ? ships[0] : null);
}
