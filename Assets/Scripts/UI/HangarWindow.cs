using System.Collections.Generic;
using System.Linq;
using FORGE3D;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 정거장의 격납고 창. 탭 두 개:
///  - 격납고: 지금 함선(수리), 장착 중인 무기(슬롯), 보관 중인 무기. 보관 무기를 골라 슬롯에 장착하면 원래 무기는 보관함으로 돌아온다.
///  - 상점: 파는 무기(WeaponCatalog의 forSale)와 함선(아직 준비 중). 산 무기는 격납고 보관함에 들어간다.
/// 왼쪽 목록(StationListView)에서 고르면 오른쪽 상세(ItemDetailView)에 설명·세부정보(데미지·발사속도·DPS·사정거리·탄속·가격)와 버튼이 나온다.
/// 계산과 지불은 StationShop, 보관함은 PlayerHangar, 슬롯은 PlayerWeaponSelector.
/// </summary>
public class HangarWindow : StationWindow
{
    private enum Tab { Hangar, Shop }

    [Header("탭")]
    [SerializeField] private Button _hangarTab;
    [SerializeField] private Button _shopTab;
    [SerializeField] private Sprite _tabOn;
    [SerializeField] private Sprite _tabOff;

    [Header("목록 / 상세")]
    [SerializeField] private StationListView _list;
    [SerializeField] private ItemDetailView _detail;
    [Tooltip("함선 줄과 '준비 중' 함선 상품에 쓰는 아이콘 (함선 설정에 아이콘이 없을 때)")]
    [SerializeField] private Sprite _shipIcon;

    private Tab _tab = Tab.Hangar;
    private string _selectedKey;
    private string _message;
    private WeaponCatalog _catalog;

    // 세부정보 게이지의 기준 (목록 전체에서 가장 큰 값)
    private float _maxDamage, _maxShots, _maxDps, _maxRange, _maxSpeed;

    protected override void Awake()
    {
        base.Awake();
        _hangarTab.onClick.AddListener(() => SwitchTab(Tab.Hangar));
        _shopTab.onClick.AddListener(() => SwitchTab(Tab.Shop));
    }

    protected override void OnOpened()
    {
        _catalog = WeaponCatalog.Load();
        ComputeMaxima();

        PlayerHangar.OnChanged += Rebuild;
        if (CreditWallet.Instance != null)
            CreditWallet.Instance.OnChanged += HandleCredits;

        SwitchTab(Tab.Hangar);
    }

    protected override void OnClosed()
    {
        PlayerHangar.OnChanged -= Rebuild;
        if (CreditWallet.Instance != null)
            CreditWallet.Instance.OnChanged -= HandleCredits;
    }

    private void OnDestroy()
    {
        OnClosed();
    }

    private void HandleCredits(int credits)
    {
        Rebuild();
    }

    private void SwitchTab(Tab tab)
    {
        _tab = tab;
        _selectedKey = null;
        _message = null;
        ((Image)_hangarTab.targetGraphic).sprite = tab == Tab.Hangar ? _tabOn : _tabOff;
        ((Image)_shopTab.targetGraphic).sprite = tab == Tab.Shop ? _tabOn : _tabOff;
        SetTitle(tab == Tab.Hangar ? "격납고" : "상점");
        Rebuild(false);
    }

    private void Rebuild()
    {
        Rebuild(true);
    }

    /// <summary>목록을 새로 그리고, 고른 항목이 아직 있으면 다시 고른다 (없으면 첫 항목)</summary>
    private void Rebuild(bool keepScroll)
    {
        if (!IsOpen)
            return;

        _list.Clear(keepScroll);
        var entries = new List<(string key, StationListRow row, System.Action show)>();

        if (_tab == Tab.Hangar)
            BuildHangar(entries);
        else
            BuildShop(entries);

        var chosen = entries.FirstOrDefault(e => e.key == _selectedKey);
        if (chosen.row == null && entries.Count > 0)
            chosen = entries[0];

        if (chosen.row == null)
        {
            _detail.ShowEmpty(_tab == Tab.Hangar ? "격납고가 비어 있습니다." : "판매 중인 상품이 없습니다.");
            return;
        }

        _selectedKey = chosen.key;
        _list.Select(chosen.row);
        chosen.show();
    }

    private void Select(string key)
    {
        if (key != _selectedKey)
            _message = null;
        _selectedKey = key;
        Rebuild(true);
    }

    // ── 격납고 탭 ──────────────────────────────────────

    private void BuildHangar(List<(string, StationListRow, System.Action)> entries)
    {
        // 함선
        _list.AddHeader("함선");
        var ship = FindObjectOfType<SpaceshipController>();
        PlayerShipAppearance.LookSettings look = ship != null ? PlayerShipAppearance.For(ship).CurrentSettings : null;
        HullHealth hull = ship != null ? ship.GetComponentInChildren<HullHealth>() : null;
        ShieldHealth shield = ship != null ? ship.GetComponentInChildren<ShieldHealth>() : null;
        string shipName = look != null && !string.IsNullOrEmpty(look.displayName) ? look.displayName : "함선";
        Sprite shipIcon = look != null && look.icon != null ? look.icon : _shipIcon;

        StationListRow shipRow = _list.AddRow();
        int repair = StationShop.Instance != null ? StationShop.Instance.RepairCost() : 0;
        shipRow.Bind(shipIcon, shipName, hull != null ? $"선체 {Mathf.CeilToInt(hull.CurrentHp)} / {Mathf.CeilToInt(hull.MaxHp)}" : "",
            repair > 0 ? "수리 필요" : "정상", () => Select("ship"));
        entries.Add(("ship", shipRow, () => ShowShip(ship, look, shipName, shipIcon, hull, shield)));

        // 장착 중
        PlayerWeaponSelector selector = PlayerWeaponSelector.Instance;
        int slots = selector != null ? selector.SlotCount : 0;
        _list.AddHeader("장착 중");
        for (int i = 0; i < slots; i++)
        {
            int slot = i;
            TurretItemData item = Item(selector.GetWeapon(slot));
            StationListRow row = _list.AddRow();
            row.Bind(item != null ? item.icon : null, ItemName(item, selector.GetWeapon(slot)), WeaponLine(item), $"슬롯 {slot + 1}",
                () => Select("slot" + slot));
            entries.Add(("slot" + slot, row, () => ShowEquipped(item, slot)));
        }

        // 보관 중 (같은 무기는 한 줄로)
        var stored = PlayerHangar.Items.GroupBy(t => t).ToList();
        _list.AddHeader(stored.Count > 0 ? $"보관 중 ({PlayerHangar.Items.Count})" : "보관 중 · 비어 있음 (상점에서 구매)");
        foreach (var group in stored)
        {
            F3DFXType type = group.Key;
            int count = group.Count();
            TurretItemData item = Item(type);
            StationListRow row = _list.AddRow();
            row.Bind(item != null ? item.icon : null, ItemName(item, type), WeaponLine(item), count > 1 ? $"×{count}" : "",
                () => Select("stored" + type));
            entries.Add(("stored" + type, row, () => ShowStored(item, type, count)));
        }
    }

    private void ShowShip(SpaceshipController ship, PlayerShipAppearance.LookSettings look, string shipName, Sprite icon,
        HullHealth hull, ShieldHealth shield)
    {
        _detail.Show(icon, shipName, "현재 함선", hull != null ? $"선체 {Mathf.CeilToInt(hull.CurrentHp)} / {Mathf.CeilToInt(hull.MaxHp)}" : "");
        _detail.SetDescription(look != null ? look.description : "");

        if (hull != null)
            _detail.AddStat("선체", $"{Mathf.CeilToInt(hull.CurrentHp)} / {Mathf.CeilToInt(hull.MaxHp)}", hull.CurrentHp / hull.MaxHp);
        if (shield != null)
            _detail.AddStat("쉴드", $"{Mathf.CeilToInt(shield.CurrentHp)} / {Mathf.CeilToInt(shield.MaxHp)}", shield.CurrentHp / Mathf.Max(1f, shield.MaxHp));

        var move = ship != null ? ship.GetComponent<ShipMovementBase>() : null;
        if (move != null)
        {
            _detail.AddStat("최고 속도", move.MaxSpeed.ToString("0"));
            _detail.AddStat("가속", move.Acceleration.ToString("0"));
        }

        if (PlayerWeaponSelector.Instance != null)
            _detail.AddStat("무기 슬롯", PlayerWeaponSelector.Instance.SlotCount.ToString());

        StationShop shop = StationShop.Instance;
        int cost = shop != null ? shop.RepairCost() : 0;
        if (cost == 0)
            _detail.SetPrimary("수리 완료", false, null);
        else
            _detail.SetPrimary($"수리 · {cost:N0} CR", StationShop.CanAfford(cost), () =>
            {
                _message = shop.Repair() ? "선체를 수리했습니다." : "크레딧이 부족합니다.";
                Rebuild();
            });

        _detail.SetMessage(_message);
    }

    private void ShowEquipped(TurretItemData item, int slot)
    {
        ShowWeapon(item, $"장착 중 · 슬롯 {slot + 1}");
        _detail.SetMessage(_message ?? "보관 중인 무기를 골라 이 슬롯에 장착할 수 있습니다.");
    }

    private void ShowStored(TurretItemData item, F3DFXType type, int count)
    {
        ShowWeapon(item, count > 1 ? $"보관 중 ×{count}" : "보관 중");

        // 슬롯 버튼 (지금 함선은 슬롯 두 개)
        PlayerWeaponSelector selector = PlayerWeaponSelector.Instance;
        int slots = selector != null ? selector.SlotCount : 0;
        _detail.SetPrimary(slots > 0 ? $"슬롯 1에 장착 ({ItemName(Item(selector.GetWeapon(0)), selector.GetWeapon(0))})" : null,
            true, () => Equip(type, 0));
        _detail.SetSecondary(slots > 1 ? $"슬롯 2에 장착 ({ItemName(Item(selector.GetWeapon(1)), selector.GetWeapon(1))})" : null,
            true, () => Equip(type, 1));
        _detail.SetMessage(_message);
    }

    private void Equip(F3DFXType type, int slot)
    {
        _message = PlayerHangar.Equip(type, slot) ? $"슬롯 {slot + 1}에 장착했습니다. 원래 무기는 보관함으로 옮겼습니다." : "장착하지 못했습니다.";
        _selectedKey = "slot" + slot;
        Rebuild(); // PlayerHangar.OnChanged로도 다시 그려진다
    }

    // ── 상점 탭 ────────────────────────────────────────

    private void BuildShop(List<(string, StationListRow, System.Action)> entries)
    {
        _list.AddHeader("무기");
        if (_catalog != null)
        {
            foreach (TurretItemData item in _catalog.weapons.Where(w => w != null && w.forSale))
            {
                TurretItemData it = item;
                StationListRow row = _list.AddRow();
                string price = StationShop.CanAfford(it.price) ? $"{it.price:N0} CR" : $"<color=#FF7B7B>{it.price:N0} CR</color>";
                row.Bind(it.icon, ItemName(it, it.weaponType), WeaponLine(it), price, () => Select("buy" + it.weaponType));
                entries.Add(("buy" + it.weaponType, row, () => ShowForSale(it)));
            }
        }

        _list.AddHeader("함선");
        StationListRow shipRow = _list.AddRow();
        shipRow.Bind(_shipIcon, "함선 업그레이드 · 교체", "준비 중", "", () => Select("ships"));
        entries.Add(("ships", shipRow, () =>
        {
            _detail.Show(_shipIcon, "함선 업그레이드 · 교체", "준비 중", "");
            _detail.SetDescription("함선 업그레이드와 교체는 준비 중입니다.");
        }));
    }

    private void ShowForSale(TurretItemData item)
    {
        int owned = PlayerHangar.Items.Count(t => t == item.weaponType);
        int equipped = 0;
        PlayerWeaponSelector selector = PlayerWeaponSelector.Instance;
        for (int i = 0; selector != null && i < selector.SlotCount; i++)
            if (selector.GetWeapon(i) == item.weaponType) equipped++;

        ShowWeapon(item, owned + equipped > 0 ? $"보유: 장착 {equipped} · 보관 {owned}" : "보유하지 않음");

        bool afford = StationShop.CanAfford(item.price);
        _detail.SetPrimary(afford ? $"구매 · {item.price:N0} CR" : "크레딧 부족", afford, () =>
        {
            _message = StationShop.Instance != null && StationShop.Instance.Buy(item)
                ? "구매했습니다. 격납고 탭에서 장착할 수 있습니다."
                : "구매하지 못했습니다.";
            Rebuild();
        });
        _detail.SetMessage(_message);
    }

    // ── 무기 상세 ──────────────────────────────────────

    private void ShowWeapon(TurretItemData item, string info)
    {
        if (item == null)
        {
            _detail.Show(null, "알 수 없는 무기", "", info);
            return;
        }

        _detail.Show(item.icon, ItemName(item, item.weaponType), item.forSale ? $"{item.price:N0} CR" : "기본 장비", info);

        F3DFXType type = item.weaponType;
        string bonus = $"쉴드 피해 ×{WeaponStatsTable.GetShieldMultiplier(type):0.##} · 선체 피해 ×{WeaponStatsTable.GetArmorMultiplier(type):0.##}";
        _detail.SetDescription(string.IsNullOrEmpty(item.description) ? bonus : $"{item.description}\n\n{bonus}");

        WeaponSpecs.Specs s = WeaponSpecs.Get(item);
        bool continuous = s.shotsPerSecond <= 0f;
        _detail.AddStat("데미지", continuous ? $"{s.damage:0.#} /초" : s.damage.ToString("0.#"), Ratio(s.damage, _maxDamage));
        _detail.AddStat("발사속도", continuous ? "지속" : $"{s.shotsPerSecond:0.#} 발/초", continuous ? 1f : Ratio(s.shotsPerSecond, _maxShots));
        _detail.AddStat("DPS", s.dps.ToString("0.#"), Ratio(s.dps, _maxDps));
        _detail.AddStat("사정거리", s.range > 0f ? s.range.ToString("0") : "-", Ratio(s.range, _maxRange));
        _detail.AddStat("탄속", s.isBeam ? "즉시" : s.projectileSpeed > 0f ? s.projectileSpeed.ToString("0") : "-",
            s.isBeam ? 1f : Ratio(s.projectileSpeed, _maxSpeed));
        _detail.AddStat("가격", item.forSale ? $"{item.price:N0} CR" : "판매하지 않음");
    }

    private void ComputeMaxima()
    {
        _maxDamage = _maxShots = _maxDps = _maxRange = _maxSpeed = 0f;
        if (_catalog == null)
            return;

        foreach (TurretItemData item in _catalog.weapons.Where(w => w != null))
        {
            WeaponSpecs.Specs s = WeaponSpecs.Get(item);
            _maxDamage = Mathf.Max(_maxDamage, s.damage);
            _maxShots = Mathf.Max(_maxShots, s.shotsPerSecond);
            _maxDps = Mathf.Max(_maxDps, s.dps);
            _maxRange = Mathf.Max(_maxRange, s.range);
            _maxSpeed = Mathf.Max(_maxSpeed, s.projectileSpeed);
        }
    }

    private static float Ratio(float value, float max)
    {
        return max > 0f ? value / max : 0f;
    }

    private TurretItemData Item(F3DFXType type)
    {
        return _catalog != null ? _catalog.Find(type) : null;
    }

    private static string ItemName(TurretItemData item, F3DFXType type)
    {
        return item != null && !string.IsNullOrEmpty(item.displayName) ? item.displayName : WeaponStatsTable.GetDisplayName(type);
    }

    private static string WeaponLine(TurretItemData item)
    {
        return item != null ? $"DPS {WeaponStatsTable.GetDps(item.weaponType):0.#}" : "";
    }
}
