using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 함선에 단 장비와 격납고에 보관 중인 장비 (EquipmentCatalog의 id).
///  - 함선에는 장비 칸(지금 함선의 ShipCatalog.equipmentSlots)만큼 장비를 달 수 있다.
///    강화 장갑·보호막 증폭기는 여러 개 달 수 있고(EquipmentCatalog.Stacks) 효과를 더한다. 나머지 종류는 하나만 단다.
///  - 처음에는 아무것도 없다: 선체·속도·가속은 함선 기본값(ShipCatalog), 화물칸·채굴도 기본값, 보호막은 없다(꺼진 채로 맞지 않는다).
///  - 상점에서 산 장비는 보관함에 들어가고(Add), 장착(Equip)하면 하나만 다는 종류는 원래 장비가 보관함으로 돌아온다.
///    해제(Unequip)하면 칸이 빈다.
///  - 장착이 바뀌면 함선에 바로 적용한다(Apply): 선체 최대 체력·가속은 함선 기본값 × (1 + %), 보호막은 증폭기를 달았을 때만 함선의 기준값 × (1 + %).
///    %는 같은 종류를 모두 더한 값이다(Bonus). 화물칸 용량(CargoHold)과 채굴 속도(MiningLaser)도 Bonus로 읽어 간다.
/// 스토리 cue로 장비를 주고 바로 달 수 있다:  equip:장비id   (예: equip:armor_1)
/// 씬을 다시 시작하면 비워지고(그때 함선에 빈 장비를 적용), 저장(SaveSystem)에 함께 남는다.
/// </summary>
public static class PlayerEquipment
{
    private const string EquipPrefix = "equip:";

    private static readonly List<string> Stored = new List<string>();
    private static readonly List<string> Equipped = new List<string>(); // 단 장비 id (단 순서)

    /// <summary>보관함이나 장착이 바뀌었을 때 (UI 갱신용)</summary>
    public static event Action OnChanged;

    public static IReadOnlyList<string> Items => Stored;

    /// <summary>지금 함선의 장비 칸 수</summary>
    public static int SlotCount
    {
        get
        {
            ShipCatalog.Ship ship = PlayerShips.Current;
            return ship != null ? ship.equipmentSlots : 4;
        }
    }

    /// <summary>지금 단 장비 수</summary>
    public static int UsedSlots => Equipped.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Stored.Clear();
        Equipped.Clear();
        OnChanged = null;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // StoryPlayer도 플레이를 시작할 때 OnCue를 비우므로(정적 초기화) 씬을 불러올 때 구독한다 (중복 없이)
        StoryPlayer.OnCue -= HandleCue;
        StoryPlayer.OnCue += HandleCue;

        // 다시 시작(패배 후 재시작 등)하면 빈 장비. 이어 하기는 SaveSystem이 다시 채운다
        Stored.Clear();
        Equipped.Clear();
        Apply();
    }

    /// <summary>그 종류로 단 장비 (여러 개면 처음 단 것, 없으면 null)</summary>
    public static EquipmentCatalog.Item Get(EquipmentCatalog.Kind kind)
    {
        return EquippedItems().FirstOrDefault(i => i.kind == kind);
    }

    /// <summary>그 장비를 몇 개 달았는지</summary>
    public static int EquippedCount(string id)
    {
        return Equipped.Count(e => e == id);
    }

    /// <summary>단 장비들 (종류 순서, 같은 종류는 단 순서)</summary>
    public static IEnumerable<EquipmentCatalog.Item> EquippedItems()
    {
        EquipmentCatalog catalog = EquipmentCatalog.Load();
        if (catalog == null)
            return Enumerable.Empty<EquipmentCatalog.Item>();

        return Equipped.Select(catalog.Find).Where(i => i != null)
            .OrderBy(i => Array.IndexOf(EquipmentCatalog.AllKinds, i.kind)).ToList();
    }

    /// <summary>그 종류 장비의 효과 비율. 여러 개 달았으면 더한다 (예: +10%와 +20%면 0.3, 안 달았으면 0)</summary>
    public static float Bonus(EquipmentCatalog.Kind kind)
    {
        return EquippedItems().Where(i => i.kind == kind).Sum(i => i.bonusPercent) / 100f;
    }

    public static void Add(string id)
    {
        Stored.Add(id);
        OnChanged?.Invoke();
    }

    /// <summary>보관함에서 하나를 뺀다 (상점에 팔 때). 없으면 false.</summary>
    public static bool Remove(string id)
    {
        if (!Stored.Remove(id))
            return false;

        OnChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 장착하면 바뀌는 장비 (하나만 다는 종류를 이미 달았으면 그것, 아니면 null).
    /// 강화 장갑·보호막 증폭기는 바꾸지 않고 빈 칸에 더 단다.
    /// </summary>
    public static EquipmentCatalog.Item Replaces(EquipmentCatalog.Item item)
    {
        return item != null && !EquipmentCatalog.Stacks(item.kind) ? Get(item.kind) : null;
    }

    /// <summary>보관함의 장비를 달 수 있는지 (못 달면 이유). 하나만 다는 종류를 달았으면 교체, 아니면 빈 칸이 있어야 한다.</summary>
    public static bool CanEquip(string id, out string reason)
    {
        EquipmentCatalog catalog = EquipmentCatalog.Load();
        EquipmentCatalog.Item item = catalog != null ? catalog.Find(id) : null;
        reason = null;
        if (item == null || !Stored.Contains(id))
            reason = "보관함에 없는 장비입니다.";
        else if (Replaces(item) == null && Equipped.Count >= SlotCount)
            reason = $"장비 칸({SlotCount}칸)이 가득 찼습니다. 다른 장비를 해제하세요.";
        return reason == null;
    }

    /// <summary>보관함의 장비를 단다. 하나만 다는 종류는 원래 장비가 보관함으로 돌아온다.</summary>
    public static bool Equip(string id)
    {
        if (!CanEquip(id, out _))
            return false;

        EquipmentCatalog.Item item = EquipmentCatalog.Load().Find(id);
        EquipmentCatalog.Item previous = Replaces(item);
        Stored.Remove(id);
        if (previous != null)
        {
            Equipped.Remove(previous.id);
            Stored.Add(previous.id);
        }

        Equipped.Add(id);

        Apply();
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>단 장비 하나를 떼어 보관함에 넣는다.</summary>
    public static bool Unequip(string id)
    {
        if (!Equipped.Remove(id))
            return false;

        Stored.Add(id);

        Apply();
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 장착한 장비를 플레이어 함선에 적용한다 (선체 최대 체력, 보호막, 속도·가속). 기본값은 지금 함선(PlayerShips.Current)의 것이다.
    /// 장갑이 바뀌면 선체는 가득 찬다.
    /// </summary>
    public static void Apply()
    {
        var ship = UnityEngine.Object.FindObjectOfType<SpaceshipController>();
        ShipCatalog.Ship data = PlayerShips.Current;
        if (ship == null || data == null)
            return;

        HullHealth hull = ship.GetComponentInChildren<HullHealth>();
        if (hull != null)
        {
            float max = Mathf.Round(data.hullHp * (1f + Bonus(EquipmentCatalog.Kind.Armor)));
            if (!Mathf.Approximately(hull.MaxHp, max))
                hull.SetMaxHp(max);
        }

        ShieldHealth shield = ship.GetComponentInChildren<ShieldHealth>(true);
        if (shield != null)
        {
            if (Get(EquipmentCatalog.Kind.Shield) == null)
            {
                shield.Uninstall();
            }
            else
            {
                float scale = 1f + Bonus(EquipmentCatalog.Kind.Shield);
                float maxHp = Mathf.Round(data.shieldBaseHp * scale);
                if (!shield.Installed || !Mathf.Approximately(shield.MaxHp, maxHp))
                    shield.Install(maxHp, data.shieldRechargeDelay, data.shieldRechargePerSecond * scale);
            }
        }

        var move = ship.GetComponent<ShipMovementBase>();
        if (move != null)
        {
            move.MaxSpeed = data.maxSpeed;
            move.Acceleration = data.acceleration * (1f + Bonus(EquipmentCatalog.Kind.Thruster));
        }
    }

    /// <summary>함선을 바꿔 장비 칸이 줄었으면 넘치는 장비(종류 순서 뒤쪽부터)를 보관함으로 옮긴다.</summary>
    public static void FitToSlots()
    {
        int slots = SlotCount;
        if (Equipped.Count <= slots)
            return;

        foreach (EquipmentCatalog.Item item in EquippedItems().Reverse())
        {
            if (Equipped.Count <= slots)
                break;
            Equipped.Remove(item.id);
            Stored.Add(item.id);
        }

        OnChanged?.Invoke();
    }

    private static void HandleCue(string cue)
    {
        if (!cue.StartsWith(EquipPrefix))
            return;

        string id = cue.Substring(EquipPrefix.Length).Trim();
        EquipmentCatalog catalog = EquipmentCatalog.Load();
        EquipmentCatalog.Item item = catalog != null ? catalog.Find(id) : null;
        if (item == null)
        {
            Debug.LogWarning($"[Story] '{cue}'의 장비 id를 찾지 못했습니다. (EquipmentCatalog의 id, 예: equip:armor_1)");
            return;
        }

        // 이미 같은 장비를 달고 있으면(이어 하기 등) 다시 주지 않는다
        if (Equipped.Contains(id))
            return;

        Stored.Add(id);
        if (!Equip(id))
            OnChanged?.Invoke(); // 칸이 없으면 보관함에 둔다
    }

    // ── 저장 ───────────────────────────────────────────

    /// <summary>저장용: 단 장비 id</summary>
    public static string[] EquippedIds()
    {
        return Equipped.ToArray();
    }

    public static string[] StoredIds()
    {
        return Stored.ToArray();
    }

    /// <summary>불러오기용: 장착과 보관함을 이 상태로 바꾸고 함선에 적용한다 (칸 수를 넘거나 하나만 다는 종류가 겹치면 보관함으로)</summary>
    public static void Restore(IEnumerable<string> equipped, string[] stored)
    {
        Stored.Clear();
        Equipped.Clear();
        EquipmentCatalog catalog = EquipmentCatalog.Load();

        if (catalog != null)
        {
            foreach (string id in equipped ?? Enumerable.Empty<string>())
            {
                EquipmentCatalog.Item item = catalog.Find(id);
                if (item == null)
                    continue;

                if (Replaces(item) == null && Equipped.Count < SlotCount)
                    Equipped.Add(id);
                else
                    Stored.Add(id);
            }

            foreach (string id in stored ?? new string[0])
            {
                if (catalog.Find(id) != null)
                    Stored.Add(id);
            }
        }

        Apply();
        OnChanged?.Invoke();
    }
}
