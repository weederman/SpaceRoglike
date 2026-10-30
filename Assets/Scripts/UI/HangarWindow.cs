using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 정거장의 격납고 창. 탭 두 개:
///  - 격납고: 함선(수리·교체) → 장착 중인 무기(슬롯) → 보관 중인 무기 → 장착 중인 장비(장비 칸. 강화 장갑·보호막 증폭기는 여러 개, 나머지는 종류마다 하나) → 보관 중인 장비.
///    보관품을 골라 장착하면 원래 것은 보관함으로 돌아온다. 장비는 떼어 보관함에 넣을 수도 있다(보호막 증폭기가 없으면 보호막 없음).
///  - 상점: 무기(WeaponCatalog의 forSale)·장비 5종(EquipmentCatalog). 가진 것은 이름 옆에 (보유중)과 개수가 붙고,
///    같은 상세 화면에서 구매와 판매(보관 중인 것을 구매가의 sellRatio로 되팔기)를 한다. 팔지 않는 무기·장비도 가지고 있으면 목록에 나와 되팔 수 있다.
///    광물 시장(화물칸의 광석을 시장가로 판매, OreCatalog), 함선(상점에서 팔지 않음). 산 것은 격납고 보관함에 들어간다.
/// 왼쪽 목록(StationListView)에서 고르면 오른쪽 상세(ItemDetailView)에 설명·세부정보(데미지·발사속도·DPS·사정거리·탄속·가격)와 버튼이 나온다.
/// 계산과 지불은 StationShop, 무기 보관함은 PlayerHangar, 무기 슬롯은 PlayerWeaponSelector, 장비는 PlayerEquipment. 무기는 무기 id(weaponId)로 다룬다.
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
    [Tooltip("장갑 아이콘 (장비에 아이콘이 없거나 장비 칸이 비었을 때)")]
    [SerializeField] private Sprite _armorIcon;
    [Tooltip("보호막 아이콘 (장비에 아이콘이 없을 때)")]
    [SerializeField] private Sprite _shieldIcon;

    private Tab _tab = Tab.Hangar;
    private int _targetSlot; // 무기 슬롯이 셋 이상일 때 보관 무기를 달 슬롯 (장착 중 슬롯을 고르거나 '다른 슬롯'으로 바꾼다)
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
        PlayerShips.OnChanged += Rebuild;
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
        PlayerShips.OnChanged -= Rebuild;
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

        // 가진 다른 함선 (갈아탈 수 있다)
        ShipCatalog ships = ShipCatalog.Load();
        foreach (string id in PlayerShips.OwnedIds)
        {
            ShipCatalog.Ship owned = ships != null ? ships.Find(id) : null;
            if (owned == null || id == PlayerShips.CurrentId)
                continue;

            string key = "ownedShip" + id;
            StationListRow row = _list.AddRow();
            row.Bind(owned.look.icon != null ? owned.look.icon : _shipIcon, owned.DisplayName,
                $"{owned.shipClass} · 무기 {owned.weaponSlots} · 장비 {owned.equipmentSlots}", "보유", () => Select(key));
            entries.Add((key, row, () => ShowOwnedShip(owned)));
        }

        // 무기: 장착 중(슬롯) → 보관 중
        PlayerWeaponSelector selector = PlayerWeaponSelector.Instance;
        int slots = selector != null ? selector.SlotCount : 0;
        _list.AddHeader($"장착 중인 무기 ({slots})");
        for (int i = 0; i < slots; i++)
        {
            int slot = i;
            string weapon = selector.GetWeapon(slot);
            TurretItemData item = Item(weapon);
            StationListRow row = _list.AddRow();
            row.Bind(item != null ? item.icon : null, ItemName(item, weapon), WeaponLine(item), $"슬롯 {slot + 1}",
                () => Select("slot" + slot));
            entries.Add(("slot" + slot, row, () => ShowEquipped(item, slot)));
        }

        // 보관 중인 무기 (같은 무기는 한 줄로)
        var storedWeapons = PlayerHangar.Items.GroupBy(t => t).ToList();
        _list.AddHeader(storedWeapons.Count > 0 ? $"보관 중인 무기 ({PlayerHangar.Items.Count})" : "보관 중인 무기 · 없음 (상점에서 구매)");
        foreach (var group in storedWeapons)
        {
            string weapon = group.Key;
            int count = group.Count();
            TurretItemData item = Item(weapon);
            StationListRow row = _list.AddRow();
            row.Bind(item != null ? item.icon : null, ItemName(item, weapon), WeaponLine(item), count > 1 ? $"×{count}" : "",
                () => Select("stored" + weapon));
            entries.Add(("stored" + weapon, row, () => ShowStored(item, weapon, count)));
        }

        // 장비: 장착 중(장비 칸, 빈 칸 포함) → 보관 중
        _list.AddHeader($"장착 중인 장비 ({PlayerEquipment.UsedSlots} / {PlayerEquipment.SlotCount})");
        // 같은 장비를 여러 개 달 수 있으므로(강화 장갑·보호막 증폭기) 몇 번째인지도 키에 넣는다
        var equippedSeen = new Dictionary<string, int>();
        foreach (EquipmentCatalog.Item equipped in PlayerEquipment.EquippedItems())
        {
            EquipmentCatalog.Item item = equipped;
            equippedSeen.TryGetValue(item.id, out int nth);
            equippedSeen[item.id] = nth + 1;
            string key = EquippedKey(item.id, nth);
            StationListRow row = _list.AddRow();
            row.Bind(IconOf(item), item.displayName, EquipmentLine(item), EquipmentCatalog.KindName(item.kind), () => Select(key));
            entries.Add((key, row, () => ShowEquipmentSlot(item)));
        }

        for (int i = PlayerEquipment.UsedSlots; i < PlayerEquipment.SlotCount; i++)
        {
            string key = "eqEmpty" + i;
            StationListRow row = _list.AddRow();
            row.Bind(_armorIcon, "빈 장비 칸", "상점에서 장비 구매", "", () => Select(key));
            entries.Add((key, row, ShowEmptySlot));
        }

        var storedEquipment = PlayerEquipment.Items.GroupBy(id => id).ToList();
        _list.AddHeader(storedEquipment.Count > 0 ? $"보관 중인 장비 ({PlayerEquipment.Items.Count})" : "보관 중인 장비 · 없음 (상점에서 구매)");
        foreach (var group in storedEquipment)
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

    private void ShowShip(SpaceshipController ship, PlayerShipAppearance.LookSettings look, string shipName, Sprite icon,
        HullHealth hull, ShieldHealth shield)
    {
        _detail.Show(icon, shipName, "현재 함선", hull != null ? $"선체 {Mathf.CeilToInt(hull.CurrentHp)} / {Mathf.CeilToInt(hull.MaxHp)}" : "");
        _detail.SetDescription(look != null ? look.description : "");

        if (hull != null)
            _detail.AddStat("선체", $"{Mathf.CeilToInt(hull.CurrentHp)} / {Mathf.CeilToInt(hull.MaxHp)}", hull.CurrentHp / hull.MaxHp);
        if (shield != null && shield.Installed)
            _detail.AddStat("보호막", $"{Mathf.CeilToInt(shield.CurrentHp)} / {Mathf.CeilToInt(shield.MaxHp)}", shield.CurrentHp / Mathf.Max(1f, shield.MaxHp));
        else
            _detail.AddStat("보호막", "없음 (보호막 증폭기 필요)");

        _detail.AddStat("장비 칸", $"{PlayerEquipment.UsedSlots} / {PlayerEquipment.SlotCount}");
        if (CargoHold.Instance != null)
            _detail.AddStat("화물칸", $"{Mathf.FloorToInt(CargoHold.Instance.Used)} / {Mathf.FloorToInt(CargoHold.Instance.Capacity)}");

        var move = ship != null ? ship.GetComponent<ShipMovementBase>() : null;
        if (move != null)
        {
            _detail.AddStat("최고 속도", move.MaxSpeed.ToString("0"));
            _detail.AddStat("가속", move.Acceleration.ToString("0"));
        }

        if (PlayerWeaponSelector.Instance != null)
            _detail.AddStat("무기 슬롯", PlayerWeaponSelector.Instance.SlotCount.ToString());
        ShipCatalog.Ship data = PlayerShips.Current;
        if (data != null && !string.IsNullOrEmpty(data.shipClass))
            _detail.AddStat("함급", data.shipClass);

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

    /// <summary>가진 다른 함선: 성능과 '이 함선으로 교체'</summary>
    private void ShowOwnedShip(ShipCatalog.Ship ship)
    {
        Sprite icon = ship.look.icon != null ? ship.look.icon : _shipIcon;
        _detail.Show(icon, ship.DisplayName, "보유 함선", ship.shipClass);
        _detail.SetDescription(ship.look.description);
        _detail.AddStat("함급", ship.shipClass);
        _detail.AddStat("무기 슬롯", ship.weaponSlots.ToString());
        _detail.AddStat("장비 칸", ship.equipmentSlots.ToString());
        _detail.AddStat("기본 선체", ship.hullHp.ToString("N0"));
        _detail.AddStat("보호막 기준", ship.shieldBaseHp.ToString("N0"));
        _detail.AddStat("최고 속도", ship.maxSpeed.ToString("0"));
        _detail.AddStat("가속", ship.acceleration.ToString("0"));

        bool can = PlayerShips.CanSwitch(ship.id, out string reason);
        _detail.SetPrimary("이 함선으로 교체", can, () =>
        {
            _message = PlayerShips.Switch(ship.id)
                ? $"{ship.DisplayName}으로 바꿨습니다. 무기는 함선마다 따로 달려 있고, 장비 칸을 넘는 장비는 보관함으로 옮겼습니다."
                : "바꾸지 못했습니다.";
            _selectedKey = "ship";
            Rebuild();
        });
        _detail.SetMessage(_message ?? reason);
    }

    private void ShowEquipped(TurretItemData item, int slot)
    {
        _targetSlot = slot;
        ShowWeapon(item, $"장착 중 · 슬롯 {slot + 1}");
        _detail.SetMessage(_message ?? "보관 중인 무기를 골라 이 슬롯에 장착할 수 있습니다.");
    }

    private void ShowStored(TurretItemData item, string weapon, int count)
    {
        ShowWeapon(item, count > 1 ? $"보관 중 ×{count}" : "보관 중");

        // 슬롯 버튼: 슬롯이 둘이면 둘 다, 셋 이상이면 고른 슬롯 하나와 '다른 슬롯' (장착 중 슬롯을 먼저 골라도 된다)
        PlayerWeaponSelector selector = PlayerWeaponSelector.Instance;
        int slots = selector != null ? selector.SlotCount : 0;
        if (slots <= 2)
        {
            _detail.SetPrimary(slots > 0 ? $"슬롯 1에 장착 ({ItemName(Item(selector.GetWeapon(0)), selector.GetWeapon(0))})" : null,
                true, () => Equip(weapon, 0));
            _detail.SetSecondary(slots > 1 ? $"슬롯 2에 장착 ({ItemName(Item(selector.GetWeapon(1)), selector.GetWeapon(1))})" : null,
                true, () => Equip(weapon, 1));
        }
        else
        {
            int slot = Mathf.Clamp(_targetSlot, 0, slots - 1);
            _detail.SetPrimary($"슬롯 {slot + 1}에 장착 ({ItemName(Item(selector.GetWeapon(slot)), selector.GetWeapon(slot))})",
                true, () => Equip(weapon, slot));
            _detail.SetSecondary($"다른 슬롯 ▶ 슬롯 {(slot + 1) % slots + 1}", true, () =>
            {
                _targetSlot = (slot + 1) % slots;
                Rebuild();
            });
        }

        _detail.SetMessage(_message);
    }

    private void Equip(string weapon, int slot)
    {
        _message = PlayerHangar.Equip(weapon, slot) ? $"슬롯 {slot + 1}에 장착했습니다. 원래 무기는 보관함으로 옮겼습니다." : "장착하지 못했습니다.";
        _selectedKey = "slot" + slot;
        Rebuild(); // PlayerHangar.OnChanged로도 다시 그려진다
    }

    private void ShowEmptySlot()
    {
        _detail.Show(_armorIcon, "빈 장비 칸", "", $"장비 {PlayerEquipment.UsedSlots} / {PlayerEquipment.SlotCount}칸 사용 중");
        _detail.SetDescription("강화 장갑·보호막 증폭기·추진기 개량·화물창 확장·채굴 장비 개량 중에서 고를 수 있습니다. " +
            "강화 장갑과 보호막 증폭기는 여러 개 달 수 있고 효과가 더해집니다. 나머지는 종류마다 하나만 달 수 있고, 다른 등급을 달면 교체됩니다.\n\n" +
            "보호막 증폭기를 달아야 보호막이 생깁니다. 상점에서 장비를 사서 장착하세요.");
        _detail.SetMessage(_message);
    }

    private void ShowEquipmentSlot(EquipmentCatalog.Item item)
    {
        ShowEquipment(item, $"장착 중 · {EquipmentCatalog.KindName(item.kind)}");
        _detail.SetPrimary("해제 · 보관함으로", true, () =>
        {
            _message = PlayerEquipment.Unequip(item.id) ? $"{item.displayName}을 떼어 보관함으로 옮겼습니다." : "해제하지 못했습니다.";
            Rebuild();
        });
        _detail.SetMessage(_message);
    }

    private void ShowStoredEquipment(EquipmentCatalog.Item item, int count)
    {
        ShowEquipment(item, count > 1 ? $"보관 중 ×{count}" : "보관 중");

        EquipmentCatalog.Item current = PlayerEquipment.Replaces(item);
        bool canEquip = PlayerEquipment.CanEquip(item.id, out string reason);
        _detail.SetPrimary(current != null ? $"교체 (지금: {current.displayName})" : canEquip ? "장착" : "장비 칸 없음", canEquip, () =>
        {
            bool ok = PlayerEquipment.Equip(item.id);
            _message = !ok ? "장착하지 못했습니다."
                : current != null ? $"{item.displayName}을 장착했습니다. 원래 장비는 보관함으로 옮겼습니다."
                : EquipmentCatalog.Stacks(item.kind) && PlayerEquipment.EquippedItems().Count(i => i.kind == item.kind) > 1
                    ? $"{item.displayName}을 장착했습니다. 같은 종류의 효과가 더해집니다."
                    : $"{item.displayName}을 장착했습니다.";
            _selectedKey = EquippedKey(item.id, PlayerEquipment.EquippedCount(item.id) - 1);
            Rebuild();
        });
        if (reason == null && current == null && EquipmentCatalog.Stacks(item.kind) && PlayerEquipment.Get(item.kind) != null)
            reason = $"{EquipmentCatalog.KindName(item.kind)}는 여러 개 달 수 있고 효과가 더해집니다 (지금 {EquipmentCatalog.EffectName(item.kind)} +{PlayerEquipment.Bonus(item.kind) * 100f:0.#}%).";
        _detail.SetMessage(_message ?? reason);
    }

    private void ShowEquipment(EquipmentCatalog.Item item, string info)
    {
        _detail.Show(IconOf(item), item.displayName, item.forSale ? $"{item.price:N0} CR" : "", info);
        _detail.SetDescription(item.description);

        _detail.AddStat("종류", EquipmentCatalog.KindName(item.kind));
        _detail.AddStat("등급", $"{item.tier}단계");
        _detail.AddStat(EquipmentCatalog.EffectName(item.kind), $"+{item.bonusPercent:0.#}%",
            Ratio(item.bonusPercent, MaxOf(item.kind, i => i.bonusPercent)));

        // 보호막 증폭기: 지금 함선에서 실제로 생기는 보호막
        ShipCatalog.Ship ship = PlayerShips.Current;
        if (item.kind == EquipmentCatalog.Kind.Shield && ship != null)
        {
            float scale = 1f + item.bonusPercent / 100f;
            _detail.AddStat("보호막", $"{Mathf.Round(ship.shieldBaseHp * scale):N0} · 초당 {ship.shieldRechargePerSecond * scale:0.#} 재충전");
        }

        _detail.AddStat("가격", item.forSale ? $"{item.price:N0} CR" : "판매하지 않음");
    }

    private float MaxOf(EquipmentCatalog.Kind kind, System.Func<EquipmentCatalog.Item, float> value)
    {
        return _equipment != null ? _equipment.items.Where(i => i != null && i.kind == kind).Select(value).DefaultIfEmpty(0f).Max() : 0f;
    }

    private Sprite IconOf(EquipmentCatalog.Item item)
    {
        if (item.icon != null)
            return item.icon;
        return item.kind == EquipmentCatalog.Kind.Shield ? _shieldIcon : _armorIcon;
    }

    private static string EquipmentLine(EquipmentCatalog.Item item)
    {
        return EquipmentCatalog.EffectLine(item);
    }

    // ── 상점 탭 ────────────────────────────────────────

    private void BuildShop(List<(string, StationListRow, System.Action)> entries)
    {
        // 무기: 파는 무기와, 팔지 않지만 가진 무기(되팔 수 있게). 가진 것은 이름 옆에 (보유중)과 개수
        _list.AddHeader("무기");
        if (_catalog != null)
        {
            foreach (TurretItemData item in _catalog.weapons.Where(w => w != null && (w.forSale || OwnedWeapon(w.weaponId) > 0)))
            {
                TurretItemData it = item;
                int owned = OwnedWeapon(it.weaponId);
                StationListRow row = _list.AddRow();
                row.Bind(it.icon, OwnedName(ItemName(it, it.weaponId), owned), OwnedLine(WeaponLine(it), owned),
                    PriceText(it.forSale, it.price), () => Select("buy" + it.weaponId));
                entries.Add(("buy" + it.weaponId, row, () => ShowForSale(it)));
            }
        }

        // 장비 5종 (무기와 같이 가진 것은 (보유중)과 개수)
        if (_equipment != null)
        {
            foreach (EquipmentCatalog.Kind kind in EquipmentCatalog.AllKinds)
            {
                _list.AddHeader(EquipmentCatalog.KindName(kind));
                foreach (EquipmentCatalog.Item item in _equipment.items.Where(i => i != null && i.kind == kind && (i.forSale || OwnedEquipment(i) > 0)))
                {
                    EquipmentCatalog.Item it = item;
                    int owned = OwnedEquipment(it);
                    StationListRow row = _list.AddRow();
                    row.Bind(IconOf(it), OwnedName(it.displayName, owned), OwnedLine(EquipmentLine(it), owned),
                        PriceText(it.forSale, it.price), () => Select("buyEq" + it.id));
                    entries.Add(("buyEq" + it.id, row, () => ShowEquipmentForSale(it)));
                }
            }
        }

        // 광물 시장: 화물칸의 광석을 시장가로 판다
        OreCatalog ores = OreCatalog.Load();
        CargoHold cargo = CargoHold.Instance;
        if (ores != null && cargo != null)
        {
            _list.AddHeader($"광물 · 화물칸 {Mathf.FloorToInt(cargo.Used)} / {Mathf.FloorToInt(cargo.Capacity)}");
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
        shipRow.Bind(_shipIcon, "함선 업그레이드 · 교체", "라스의 블루프린트", "", () => Select("ships"));
        entries.Add(("ships", shipRow, () =>
        {
            _detail.Show(_shipIcon, "함선 업그레이드 · 교체", "", "상점에서 팔지 않음");
            _detail.SetDescription("새 함선은 상점에서 팔지 않습니다. 라스의 블루프린트로 재료를 모아 제작합니다 (메인 퀘스트).\n\n" +
                "가진 함선은 격납고 탭의 함선 목록에서 갈아탈 수 있습니다.");
        }));
    }

    /// <summary>상점의 무기: 구매 버튼과 판매 버튼(보관 중인 것을 구매가의 sellRatio로 되판다)</summary>
    private void ShowForSale(TurretItemData item)
    {
        int stored = PlayerHangar.Items.Count(t => t == item.weaponId);
        int equipped = EquippedWeapon(item.weaponId);
        ShowWeapon(item, stored + equipped > 0 ? $"보유 {stored + equipped}개 · 장착 {equipped} · 보관 {stored}" : "보유하지 않음");

        SetBuyButton(item.forSale, item.price, () => StationShop.Instance != null && StationShop.Instance.Buy(item));

        int sell = _catalog.SellPrice(item);
        _detail.SetSecondary(stored > 0 ? $"판매 · {sell:N0} CR" : $"판매 · 보관 중인 것 없음", stored > 0, () =>
        {
            int credits = StationShop.Instance != null ? StationShop.Instance.Sell(item) : -1;
            _message = credits >= 0 ? $"{ItemName(item, item.weaponId)}을 팔아 {credits:N0} CR을 받았습니다." : "팔지 못했습니다.";
            Rebuild();
        });
        _detail.SetMessage(_message ?? (stored == 0 && equipped > 0 ? "장착 중인 무기는 격납고 탭에서 교체해 보관함으로 옮긴 뒤 팔 수 있습니다." : null));
    }

    /// <summary>상점의 장비: 구매 버튼과 판매 버튼(보관 중인 것을 구매가의 sellRatio로 되판다)</summary>
    private void ShowEquipmentForSale(EquipmentCatalog.Item item)
    {
        int stored = PlayerEquipment.Items.Count(id => id == item.id);
        int equipped = PlayerEquipment.EquippedCount(item.id);
        ShowEquipment(item, stored + equipped > 0 ? $"보유 {stored + equipped}개 · 장착 {equipped} · 보관 {stored}" : "보유하지 않음");

        SetBuyButton(item.forSale, item.price, () => StationShop.Instance != null && StationShop.Instance.Buy(item));

        int sell = _equipment.SellPrice(item);
        _detail.SetSecondary(stored > 0 ? $"판매 · {sell:N0} CR" : $"판매 · 보관 중인 것 없음", stored > 0, () =>
        {
            int credits = StationShop.Instance != null ? StationShop.Instance.Sell(item) : -1;
            _message = credits >= 0 ? $"{item.displayName}을 팔아 {credits:N0} CR을 받았습니다." : "팔지 못했습니다.";
            Rebuild();
        });
        _detail.SetMessage(_message ?? (stored == 0 && equipped > 0 ? "장착 중인 장비는 격납고 탭에서 해제해 보관함으로 옮긴 뒤 팔 수 있습니다." : null));
    }

    private void SetBuyButton(bool forSale, int price, System.Func<bool> buy)
    {
        if (!forSale)
        {
            _detail.SetPrimary("판매하지 않는 상품", false, null);
            return;
        }

        bool afford = StationShop.CanAfford(price);
        _detail.SetPrimary(afford ? $"구매 · {price:N0} CR" : "크레딧 부족", afford, () =>
        {
            _message = buy() ? "구매했습니다. 격납고 탭에서 장착할 수 있습니다." : "구매하지 못했습니다.";
            Rebuild();
        });
    }

    /// <summary>가진 개수 (장착 + 보관)</summary>
    private static int OwnedWeapon(string weaponId)
    {
        return PlayerHangar.Items.Count(t => t == weaponId) + EquippedWeapon(weaponId);
    }

    private static int EquippedWeapon(string weaponId)
    {
        int equipped = 0;
        PlayerWeaponSelector selector = PlayerWeaponSelector.Instance;
        for (int i = 0; selector != null && i < selector.SlotCount; i++)
            if (selector.GetWeapon(i) == weaponId) equipped++;
        return equipped;
    }

    /// <summary>가진 개수 (장착 + 보관)</summary>
    private static int OwnedEquipment(EquipmentCatalog.Item item)
    {
        return PlayerEquipment.Items.Count(id => id == item.id) + PlayerEquipment.EquippedCount(item.id);
    }

    /// <summary>격납고 목록에서 단 장비 줄의 키 (같은 장비를 여러 개 달면 몇 번째인지로 구분)</summary>
    private static string EquippedKey(string id, int nth)
    {
        return $"eq{id}#{nth}";
    }

    private static string OwnedName(string name, int owned)
    {
        return owned > 0 ? $"{name} <color=#7CFFB2>(보유중)</color>" : name;
    }

    private static string OwnedLine(string line, int owned)
    {
        if (owned <= 0)
            return line;
        return string.IsNullOrEmpty(line) ? $"보유 {owned}개" : $"{line} · 보유 {owned}개";
    }

    private static string PriceText(bool forSale, int price)
    {
        if (!forSale)
            return "비매품";
        return StationShop.CanAfford(price) ? $"{price:N0} CR" : $"<color=#FF7B7B>{price:N0} CR</color>";
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

        // 사기: 화물칸 빈 자리만큼 한 번에 buyBatch개까지
        OreCatalog catalog = OreCatalog.Load();
        int unit = catalog != null ? catalog.BuyPrice(ore) : 0;
        int batch = catalog != null ? Mathf.Min(catalog.buyBatch, Mathf.FloorToInt(cargo.Capacity - cargo.Used)) : 0;
        _detail.AddStat("사는 값", $"{unit:N0} CR");
        if (batch <= 0)
            _detail.SetSecondary("화물칸 가득 참", false, null);
        else
            _detail.SetSecondary($"구매 · {batch}개 · {batch * unit:N0} CR", StationShop.CanAfford(batch * unit), () =>
            {
                int bought = StationShop.Instance != null ? StationShop.Instance.BuyOre(ore, batch) : 0;
                _message = bought > 0 ? $"{ore.displayName} {bought}개를 샀습니다." : "사지 못했습니다.";
                Rebuild();
            });

        // 채굴 임무에 쓸 광석이면 알려 준다 (시장에 팔면 임무 납품에 쓸 수 없다)
        if (_message == null && amount > 0 && MissionDirector.Current is MiningMissionDirector mining
            && (string.IsNullOrEmpty(mining.Data.oreId) || mining.Data.oreId == ore.id))
            _detail.SetMessage($"진행 중인 임무에 납품할 수 있는 광석입니다. 팔면 납품할 수 없습니다.");
        else if (_message == null && FirstShipQuest.Current == FirstShipQuest.Stage.Minerals
                 && FirstShipQuest.Data.materials.Any(m => m.oreId == ore.id && FirstShipQuest.DeliveredOf(m.oreId) < m.amount))
            _detail.SetMessage("라스에게 납품할 함선 재료입니다. 모항의 스페이스 라운지에서 납품하세요.");
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

        _detail.Show(item.icon, ItemName(item, item.weaponId), item.forSale ? $"{item.price:N0} CR" : "기본 장비", info);

        string bonus = $"쉴드 피해 ×{WeaponStatsTable.GetShieldMultiplier(item.weaponType, item.weaponId):0.##} · " +
                       $"선체 피해 ×{WeaponStatsTable.GetArmorMultiplier(item.weaponType, item.weaponId):0.##}";
        if (WeaponStatsTable.IsHoming(item.weaponType, item.weaponId))
            bonus += " · 락온한 적을 추적하는 유도탄";
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

    private TurretItemData Item(string weaponId)
    {
        return _catalog != null ? _catalog.Find(weaponId) : null;
    }

    private static string ItemName(TurretItemData item, string weaponId)
    {
        return item != null && !string.IsNullOrEmpty(item.displayName) ? item.displayName : WeaponCatalog.NameOf(weaponId);
    }

    private static string WeaponLine(TurretItemData item)
    {
        return item != null ? $"DPS {WeaponStatsTable.GetDps(item.weaponType, item.weaponId):0.#}" : "";
    }
}
