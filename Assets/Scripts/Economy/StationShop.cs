using UnityEngine;

/// <summary>
/// 정거장 서비스: 함선 수리와 무기·장비(장갑·쉴드) 구매·판매. 화면은 정거장 내부의 격납고 창(HangarWindow)이 그리고, 여기서는 계산과 지불만 한다.
///  - 수리: 크레딧을 내고 선체를 최대로 회복한다 (깎인 체력 1당 repairCostPerHp 크레딧).
///  - 구매: 크레딧을 내고 무기를 격납고 보관함(PlayerHangar)에 넣는다. 장착은 격납고 탭에서 한다.
///  - 장비: 장갑·쉴드(EquipmentCatalog)도 같은 방식으로 사서 보관함(PlayerEquipment)에 넣고 되판다.
///  - 판매: 보관함의 무기를 되판다 (WeaponCatalog.SellPrice. 장착 중인 무기는 먼저 보관함으로 옮겨야 한다).
/// 파는 무기 목록은 WeaponCatalog(Assets/Resources/Shop/WeaponCatalog.asset)의 forSale 항목이다.
/// </summary>
public class StationShop : MonoBehaviour
{
    public static StationShop Instance { get; private set; }

    [Tooltip("선체 체력 1을 고치는 데 드는 크레딧")]
    [SerializeField, Min(0f)] private float _repairCostPerHp = 0.2f;

    private HullHealth _hull;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ── 수리 ──────────────────────────────────────────

    public HullHealth PlayerHull
    {
        get
        {
            if (_hull == null)
            {
                var ship = FindObjectOfType<SpaceshipController>();
                _hull = ship != null ? ship.GetComponentInChildren<HullHealth>() : null;
            }

            return _hull;
        }
    }

    /// <summary>선체를 최대로 고치는 데 드는 크레딧 (고칠 게 없으면 0)</summary>
    public int RepairCost()
    {
        HullHealth hull = PlayerHull;
        if (hull == null)
            return 0;

        float missing = Mathf.Max(0f, hull.MaxHp - hull.CurrentHp);
        return missing < 0.5f ? 0 : Mathf.Max(1, Mathf.CeilToInt(missing * _repairCostPerHp));
    }

    public bool Repair()
    {
        HullHealth hull = PlayerHull;
        var wallet = CreditWallet.Instance;
        int cost = RepairCost();
        if (hull == null || wallet == null || cost == 0 || !wallet.TrySpend(cost))
            return false;

        hull.Restore(1f);
        return true;
    }

    // ── 구매 ──────────────────────────────────────────

    public static bool CanAfford(int price)
    {
        return CreditWallet.Instance != null && CreditWallet.Instance.Amount >= price;
    }

    /// <summary>무기를 사서 격납고 보관함에 넣는다.</summary>
    public bool Buy(TurretItemData item)
    {
        if (item == null || !item.forSale || CreditWallet.Instance == null || !CreditWallet.Instance.TrySpend(item.price))
            return false;

        PlayerHangar.Add(item.weaponType);
        return true;
    }

    /// <summary>장비(장갑·쉴드)를 사서 격납고 보관함(PlayerEquipment)에 넣는다.</summary>
    public bool Buy(EquipmentCatalog.Item item)
    {
        if (item == null || !item.forSale || CreditWallet.Instance == null || !CreditWallet.Instance.TrySpend(item.price))
            return false;

        PlayerEquipment.Add(item.id);
        return true;
    }

    /// <summary>보관함의 장비를 판다. 받은 크레딧 (팔지 못하면 -1).</summary>
    public int Sell(EquipmentCatalog.Item item)
    {
        EquipmentCatalog catalog = EquipmentCatalog.Load();
        if (item == null || catalog == null || CreditWallet.Instance == null || !PlayerEquipment.Remove(item.id))
            return -1;

        int credits = catalog.SellPrice(item);
        if (credits > 0)
            CreditWallet.Instance.Add(credits);
        return credits;
    }

    /// <summary>보관함의 무기를 판다. 받은 크레딧 (팔지 못하면 -1).</summary>
    public int Sell(TurretItemData item)
    {
        WeaponCatalog catalog = WeaponCatalog.Load();
        if (item == null || catalog == null || CreditWallet.Instance == null || !PlayerHangar.Remove(item.weaponType))
            return -1;

        int credits = catalog.SellPrice(item);
        if (credits > 0)
            CreditWallet.Instance.Add(credits);
        return credits;
    }
}
