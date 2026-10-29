using System.Collections.Generic;
using System.Linq;
using FORGE3D;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 정거장의 격납고 창. 탭 두 개:
///  - 격납고: 지금 함선(수리), 장착 중인 무기(슬롯)·장갑·쉴드, 보관 중인 무기·장비. 보관품을 골라 장착하면 원래 것은 보관함으로 돌아온다.
///    장갑·쉴드는 떼어 보관함에 넣을 수도 있다(쉴드 칸이 비면 쉴드 없음).
///  - 상점: 파는 무기(WeaponCatalog의 forSale)·장갑·쉴드(EquipmentCatalog), 보관품 판매(무기·장비를 되팔기, 발칸은 0 CR),
///    광물 시장(화물칸의 광석을 시장가로 판매, OreCatalog), 함선(아직 준비 중). 산 것은 격납고 보관함에 들어간다.
/// 왼쪽 목록(StationListView)에서 고르면 오른쪽 상세(ItemDetailView)에 설명·세부정보(데미지·발사속도·DPS·사정거리·탄속·가격)와 버튼이 나온다.
/// 계산과 지불은 StationShop, 무기 보관함은 PlayerHangar, 무기 슬롯은 PlayerWeaponSelector, 장갑·쉴드는 PlayerEquipment.
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
    [Tooltip("장갑 칸 아이콘 (장비에 아이콘이 없거나 칸이 비었을 때)")]
    [SerializeField] private Sprite _armorIcon;
    [Tooltip("쉴드 칸 아이콘 (장비에 아이콘이 없거나 칸이 비었을 때)")]
    [SerializeField] private Sprite _shieldIcon;

    private Tab _tab = Tab.Hangar;
    private string _selectedKey;
    private string _message;
    private WeaponCatalog _catalog;
    private EquipmentCatalog _equipment;

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
        _equipment = EquipmentCatalog.Load();
        ComputeMaxima();

        PlayerHangar.OnChanged += Rebuild;
        PlayerEquipment.OnChanged += Rebuild;
        if (CreditWallet.Instance != null)
            CreditWallet.Instance.OnChanged += HandleCredits;
        if (CargoHold.Instance != null)
            CargoHold.Instance.OnChanged += Rebuild;

        SwitchTab(Tab.Hangar);
    }

    protected override void OnClosed()
    {
        PlayerHangar.OnChanged -= Rebuild;
        PlayerEquipment.OnChanged -= Rebuild;
        if (CreditWallet.Instance != null)
            CreditWallet.Instance.OnChanged -= HandleCredits;
        if (CargoHold.Instance != null)
            CargoHold.Instance.OnChanged -= Rebuild;
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

        AddEquipmentSlot(entries, EquipmentCatalog.Kind.Armor);
        AddEquipmentSlot(entries, EquipmentCatalog.Kind.Shield);

        // 보관 중 (같은 무기는 한 줄로)
        var stored = PlayerHangar.Items.GroupBy(t => t).ToList();
        int storedCount = PlayerHangar.Items.Count + PlayerEquipment.Items.Count;
        _list.AddHeader(storedCount > 0 ? $"보관 중 ({storedCount})" : "보관 중 · 비어 있음 (상점에서 구매)");
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

        foreach (var group in PlayerEquipment.Items.GroupBy(id => id))
        {
            EquipmentCatalog.Item item = _equipment != null ? _equipment.Find(group.Key) : null;
            if (item == null)
                continue;

            int count = group.Count();
            StationListRow row = _list.AddRow();
            row.Bind(IconOf(item), item.displayName, EquipmentLine(item), count > 1 ? $"×{count}" : "",
                () => Select("storedEq" + item.id));
            entries.Add(("storedEq" + item.id, row, () => ShowStoredEquipment(item, count)));
        }
    }

    private void AddEquipmentSlot(List<(string, StationListRow, System.Action)> entries, EquipmentCatalog.Kind kind)
    {
        EquipmentCatalog.Item item = PlayerEquipment.Get(kind);
        string kindName = EquipmentCatalog.KindName(kind);
        string key = "eq" + kind;
        StationListRow row = _list.AddRow();
        row.Bind(item != null ? IconOf(item) : KindIcon(kind), item != null ? item.displayName : $"{kindName} 없음",
            item != null ? EquipmentLine(item) : "상점에서 구매", kindName, () => Select(key));
        entries.Add((key, row, () => ShowEquipmentSlot(kind, item)));
    }

    private void ShowShip(SpaceshipController ship, PlayerShipAppearance.LookSettings look, string shipName, Sprite icon,
        HullHealth hull, ShieldHealth shield)
    {
        _detail.Show(icon, shipName, "현재 함선", hull != null ? $"선체 {Mathf.CeilToInt(hull.CurrentHp)} / {Mathf.CeilToInt(hull.MaxHp)}" : "");
        _detail.SetDescription(look != null ? look.description : "");

        if (hull != null)
            _detail.AddStat("선체", $"{Mathf.CeilToInt(hull.CurrentHp)} / {Mathf.CeilToInt(hull.MaxHp)}", hull.CurrentHp / hull.MaxHp);
        if (shield != null && shield.Installed)
            _detail.AddStat("쉴드", $"{Mathf.CeilToInt(shield.CurrentHp)} / {Mathf.CeilToInt(shield.MaxHp)}", shield.CurrentHp / Mathf.Max(1f, shield.MaxHp));
        else
            _detail.AddStat("쉴드", "없음 (쉴드 장비 필요)");

        EquipmentCatalog.Item armor = PlayerEquipment.Get(EquipmentCatalog.Kind.Armor);
        EquipmentCatalog.Item shieldItem = PlayerEquipment.Get(EquipmentCatalog.Kind.Shield);
        _detail.AddStat("장갑", armor != null ? armor.displayName : "없음");
        _detail.AddStat("쉴드 장비", shieldItem != null ? shieldItem.displayName : "없음");

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

    private void ShowEquipmentSlot(EquipmentCatalog.Kind kind, EquipmentCatalog.Item item)
    {
        string kindName = EquipmentCatalog.KindName(kind);
        if (item == null)
        {
            _detail.Show(KindIcon(kind), $"{kindName} 칸 · 비어 있음", "", "장착한 장비 없음");
            _detail.SetDescription(kind == EquipmentCatalog.Kind.Shield
                ? "쉴드가 없으면 모든 공격을 선체로 받습니다. 상점에서 쉴드를 사서 장착하세요."
                : "장갑을 달면 선체 최대 체력이 늘어납니다. 상점에서 장갑을 사서 장착하세요.");
            _detail.SetMessage(_message);
            return;
        }

        ShowEquipment(item, $"장착 중 · {kindName}");
        _detail.SetPrimary("해제 · 보관함으로", true, () =>
        {
            _message = PlayerEquipment.Unequip(kind) ? $"{item.displayName}을 떼어 보관함으로 옮겼습니다." : "해제하지 못했습니다.";
            Rebuild();
        });
        _detail.SetMessage(_message);
    }

    private void ShowStoredEquipment(EquipmentCatalog.Item item, int count)
    {
        ShowEquipment(item, count > 1 ? $"보관 중 ×{count}" : "보관 중");

        EquipmentCatalog.Item current = PlayerEquipment.Get(item.kind);
        _detail.SetPrimary(current != null ? $"장착 (지금: {current.displayName})" : "장착", true, () =>
        {
            bool ok = PlayerEquipment.Equip(item.id);
            _message = !ok ? "장착하지 못했습니다."
                : current != null ? $"{item.displayName}을 장착했습니다. 원래 장비는 보관함으로 옮겼습니다."
                : $"{item.displayName}을 장착했습니다.";
            _selectedKey = "eq" + item.kind;
            Rebuild();
        });
        _detail.SetMessage(_message);
    }

    private void ShowEquipment(EquipmentCatalog.Item item, string info)
    {
        _detail.Show(IconOf(item), item.displayName, item.forSale ? $"{item.price:N0} CR" : "", info);
        _detail.SetDescription(item.description);

        _detail.AddStat("종류", EquipmentCatalog.KindName(item.kind));
        if (item.kind == EquipmentCatalog.Kind.Armor)
        {
            _detail.AddStat("선체 체력", $"+{item.hullBonus:N0}", Ratio(item.hullBonus, MaxOf(EquipmentCatalog.Kind.Armor, i => i.hullBonus)));
        }
        else
        {
            _detail.AddStat("쉴드 체력", item.shieldHp.ToString("N0"), Ratio(item.shieldHp, MaxOf(EquipmentCatalog.Kind.Shield, i => i.shieldHp)));
            _detail.AddStat("재충전 속도", item.rechargePerSecond > 0f ? $"{item.rechargePerSecond:0.#} /초" : "없음",
                Ratio(item.rechargePerSecond, MaxOf(EquipmentCatalog.Kind.Shield, i => i.rechargePerSecond)));
            _detail.AddStat("재충전 대기", $"피격 후 {item.rechargeDelay:0.#}초");
        }

        _detail.AddStat("가격", item.forSale ? $"{item.price:N0} CR" : "판매하지 않음");
    }

    private float MaxOf(EquipmentCatalog.Kind kind, System.Func<EquipmentCatalog.Item, float> value)
    {
        return _equipment != null ? _equipment.items.Where(i => i != null && i.kind == kind).Select(value).DefaultIfEmpty(0f).Max() : 0f;
    }

    private Sprite IconOf(EquipmentCatalog.Item item)
    {
        return item.icon != null ? item.icon : KindIcon(item.kind);
    }

    private Sprite KindIcon(EquipmentCatalog.Kind kind)
    {
        return kind == EquipmentCatalog.Kind.Armor ? _armorIcon : _shieldIcon;
    }

    private static string EquipmentLine(EquipmentCatalog.Item item)
    {
        return item.kind == EquipmentCatalog.Kind.Armor ? $"선체 +{item.hullBonus:N0}" : $"쉴드 {item.shieldHp:N0}";
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

        // 장갑 · 쉴드
        if (_equipment != null)
        {
            foreach (EquipmentCatalog.Kind kind in new[] { EquipmentCatalog.Kind.Armor, EquipmentCatalog.Kind.Shield })
            {
                _list.AddHeader(EquipmentCatalog.KindName(kind));
                foreach (EquipmentCatalog.Item item in _equipment.items.Where(i => i != null && i.forSale && i.kind == kind))
                {
                    EquipmentCatalog.Item it = item;
                    StationListRow row = _list.AddRow();
                    string price = StationShop.CanAfford(it.price) ? $"{it.price:N0} CR" : $"<color=#FF7B7B>{it.price:N0} CR</color>";
                    row.Bind(IconOf(it), it.displayName, EquipmentLine(it), price, () => Select("buyEq" + it.id));
                    entries.Add(("buyEq" + it.id, row, () => ShowEquipmentForSale(it)));
                }
            }
        }

        // 보관품 판매: 격납고 보관함의 무기·장비를 되판다 (장착 중인 것은 격납고 탭에서 먼저 보관함으로 옮긴다)
        var storedWeapons = PlayerHangar.Items.GroupBy(t => t).ToList();
        var storedEquipment = PlayerEquipment.Items.GroupBy(id => id).ToList();
        _list.AddHeader(storedWeapons.Count + storedEquipment.Count > 0 ? "판매 · 보관 중인 무기와 장비" : "판매 · 보관 중인 것 없음");
        foreach (var group in storedWeapons)
        {
            TurretItemData item = Item(group.Key);
            if (item == null)
                continue;

            int count = group.Count();
            StationListRow row = _list.AddRow();
            row.Bind(item.icon, ItemName(item, item.weaponType), count > 1 ? $"보관 ×{count}" : "보관 중", $"{_catalog.SellPrice(item):N0} CR",
                () => Select("sell" + item.weaponType));
            entries.Add(("sell" + item.weaponType, row, () => ShowSellWeapon(item, count)));
        }

        foreach (var group in storedEquipment)
        {
            EquipmentCatalog.Item item = _equipment != null ? _equipment.Find(group.Key) : null;
            if (item == null)
                continue;

            int count = group.Count();
            StationListRow row = _list.AddRow();
            row.Bind(IconOf(item), item.displayName, count > 1 ? $"보관 ×{count}" : "보관 중", $"{_equipment.SellPrice(item):N0} CR",
                () => Select("sellEq" + item.id));
            entries.Add(("sellEq" + item.id, row, () => ShowSellEquipment(item, count)));
        }

        // 광물 시장: 화물칸의 광석을 시장가로 판다
        OreCatalog ores = OreCatalog.Load();
        CargoHold cargo = CargoHold.Instance;
        if (ores != null && cargo != null)
        {
            _list.AddHeader($"광물 · 화물칸 {Mathf.FloorToInt(cargo.Ore)} / {Mathf.FloorToInt(cargo.Capacity)}");
            foreach (OreCatalog.Ore ore in ores.ores)
            {
                OreCatalog.Ore o = ore;
                int amount = Mathf.FloorToInt(cargo.Amount(o.id));
                StationListRow row = _list.AddRow();
                row.Bind(o.icon, o.displayName, $"화물칸 {amount:N0}", $"{o.price:N0} CR / 개", () => Select("ore" + o.id));
                entries.Add(("ore" + o.id, row, () => ShowOre(o, cargo)));
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

    private void ShowEquipmentForSale(EquipmentCatalog.Item item)
    {
        int owned = PlayerEquipment.Items.Count(id => id == item.id);
        bool equipped = PlayerEquipment.Get(item.kind) == item;
        ShowEquipment(item, owned > 0 || equipped ? $"보유: 장착 {(equipped ? 1 : 0)} · 보관 {owned}" : "보유하지 않음");

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

    private void ShowSellWeapon(TurretItemData item, int count)
    {
        ShowWeapon(item, count > 1 ? $"보관 중 ×{count}" : "보관 중");
        _detail.SetPrimary($"판매 · {_catalog.SellPrice(item):N0} CR", true, () =>
        {
            int credits = StationShop.Instance != null ? StationShop.Instance.Sell(item) : -1;
            _message = credits >= 0 ? $"{ItemName(item, item.weaponType)}을 팔아 {credits:N0} CR을 받았습니다." : "팔지 못했습니다.";
            Rebuild();
        });
        _detail.SetMessage(_message ?? "장착 중인 무기는 교체해 보관함으로 옮긴 뒤 팔 수 있습니다.");
    }

    private void ShowSellEquipment(EquipmentCatalog.Item item, int count)
    {
        ShowEquipment(item, count > 1 ? $"보관 중 ×{count}" : "보관 중");
        _detail.SetPrimary($"판매 · {_equipment.SellPrice(item):N0} CR", true, () =>
        {
            int credits = StationShop.Instance != null ? StationShop.Instance.Sell(item) : -1;
            _message = credits >= 0 ? $"{item.displayName}을 팔아 {credits:N0} CR을 받았습니다." : "팔지 못했습니다.";
            Rebuild();
        });
        _detail.SetMessage(_message ?? "장착 중인 장비는 해제해 보관함으로 옮긴 뒤 팔 수 있습니다.");
    }

    private void ShowOre(OreCatalog.Ore ore, CargoHold cargo)
    {
        int amount = Mathf.FloorToInt(cargo.Amount(ore.id));
        int total = amount * ore.price;
        _detail.Show(ore.icon, ore.displayName, $"{ore.price:N0} CR / 개", $"화물칸 {amount:N0}개");
        _detail.SetDescription(ore.description);
        _detail.AddStat("시장가", $"{ore.price:N0} CR");
        _detail.AddStat("화물칸", $"{amount:N0}개");
        _detail.AddStat("합계", $"{total:N0} CR");

        _detail.SetPrimary(amount > 0 ? $"전부 판매 · {total:N0} CR" : "판매할 광석 없음", amount > 0, () =>
        {
            int credits = cargo.Sell(ore.id);
            _message = $"{ore.displayName}을 팔아 {credits:N0} CR을 받았습니다.";
            Rebuild();
        });

        // 채굴 임무에 쓸 광석이면 알려 준다 (시장에 팔면 임무 납품에 쓸 수 없다)
        if (_message == null && amount > 0 && MissionDirector.Current is MiningMissionDirector mining
            && (string.IsNullOrEmpty(mining.Data.oreId) || mining.Data.oreId == ore.id))
            _detail.SetMessage($"진행 중인 임무에 납품할 수 있는 광석입니다. 팔면 납품할 수 없습니다.");
        else
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
