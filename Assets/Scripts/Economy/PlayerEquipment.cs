using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 함선의 장비(장갑 칸·쉴드 칸)와 격납고에 보관 중인 장비 (EquipmentCatalog의 id).
///  - 처음에는 두 칸 모두 비어 있다: 선체는 함선 기본 체력, 쉴드는 없다(꺼진 채로 맞지 않는다).
///  - 상점에서 산 장비는 보관함에 들어가고(Add), 장착(Equip)하면 그 칸에 있던 장비는 보관함으로 돌아온다. 해제(Unequip)하면 칸이 빈다.
///  - 장착이 바뀌면 함선에 바로 적용한다(Apply): 선체 최대 체력 = 기본 + 장갑 hullBonus, 쉴드 = 장착한 쉴드의 체력·재충전.
/// 씬을 다시 시작하면 비워지고(그때 함선에 빈 장비를 적용), 저장(SaveSystem)에 함께 남는다.
/// </summary>
public static class PlayerEquipment
{
    private static readonly List<string> Stored = new List<string>();
    private static readonly Dictionary<EquipmentCatalog.Kind, string> Equipped = new Dictionary<EquipmentCatalog.Kind, string>();
    private static float _baseHull = -1f; // 장갑 없는 함선의 선체 최대 체력 (처음 적용할 때 함선에서 읽는다)

    /// <summary>보관함이나 장착이 바뀌었을 때 (UI 갱신용)</summary>
    public static event Action OnChanged;

    public static IReadOnlyList<string> Items => Stored;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Stored.Clear();
        Equipped.Clear();
        _baseHull = -1f;
        OnChanged = null;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 다시 시작(패배 후 재시작 등)하면 빈 장비. 이어 하기는 SaveSystem이 다시 채운다
        Stored.Clear();
        Equipped.Clear();
        _baseHull = -1f;
        Apply();
    }

    /// <summary>그 칸에 장착한 장비 (비어 있으면 null)</summary>
    public static EquipmentCatalog.Item Get(EquipmentCatalog.Kind kind)
    {
        EquipmentCatalog catalog = EquipmentCatalog.Load();
        return catalog != null && Equipped.TryGetValue(kind, out string id) ? catalog.Find(id) : null;
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

    /// <summary>보관함의 장비를 그 칸에 단다. 그 칸에 있던 장비는 보관함으로 돌아온다.</summary>
    public static bool Equip(string id)
    {
        EquipmentCatalog catalog = EquipmentCatalog.Load();
        EquipmentCatalog.Item item = catalog != null ? catalog.Find(id) : null;
        if (item == null || !Stored.Contains(id))
            return false;

        Stored.Remove(id);
        if (Equipped.TryGetValue(item.kind, out string previous) && !string.IsNullOrEmpty(previous))
            Stored.Add(previous);
        Equipped[item.kind] = id;

        Apply();
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>그 칸의 장비를 떼어 보관함에 넣는다.</summary>
    public static bool Unequip(EquipmentCatalog.Kind kind)
    {
        if (!Equipped.TryGetValue(kind, out string id) || string.IsNullOrEmpty(id))
            return false;

        Equipped.Remove(kind);
        Stored.Add(id);

        Apply();
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>장착한 장비를 플레이어 함선에 적용한다 (선체 최대 체력, 쉴드). 장갑이 바뀌면 선체는 가득 찬다.</summary>
    public static void Apply()
    {
        var ship = UnityEngine.Object.FindObjectOfType<SpaceshipController>();
        if (ship == null)
            return;

        HullHealth hull = ship.GetComponentInChildren<HullHealth>();
        if (hull != null)
        {
            if (_baseHull < 0f)
                _baseHull = hull.MaxHp;

            EquipmentCatalog.Item armor = Get(EquipmentCatalog.Kind.Armor);
            float max = _baseHull + (armor != null ? armor.hullBonus : 0f);
            if (!Mathf.Approximately(hull.MaxHp, max))
                hull.SetMaxHp(max);
        }

        ShieldHealth shield = ship.GetComponentInChildren<ShieldHealth>(true);
        if (shield != null)
        {
            EquipmentCatalog.Item item = Get(EquipmentCatalog.Kind.Shield);
            if (item == null || item.shieldHp <= 0f)
                shield.Uninstall();
            else if (!shield.Installed || !Mathf.Approximately(shield.MaxHp, item.shieldHp))
                shield.Install(item.shieldHp, item.rechargeDelay, item.rechargePerSecond);
        }
    }

    // ── 저장 ───────────────────────────────────────────

    public static string ArmorId => Equipped.TryGetValue(EquipmentCatalog.Kind.Armor, out string id) ? id : "";
    public static string ShieldId => Equipped.TryGetValue(EquipmentCatalog.Kind.Shield, out string id) ? id : "";

    public static string[] StoredIds()
    {
        return Stored.ToArray();
    }

    /// <summary>불러오기용: 장착과 보관함을 이 상태로 바꾸고 함선에 적용한다</summary>
    public static void Restore(string armor, string shield, string[] stored)
    {
        Stored.Clear();
        Equipped.Clear();
        EquipmentCatalog catalog = EquipmentCatalog.Load();

        if (catalog != null)
        {
            if (catalog.Find(armor) != null)
                Equipped[EquipmentCatalog.Kind.Armor] = armor;
            if (catalog.Find(shield) != null)
                Equipped[EquipmentCatalog.Kind.Shield] = shield;
            if (stored != null)
            {
                foreach (string id in stored)
                {
                    if (catalog.Find(id) != null)
                        Stored.Add(id);
                }
            }
        }

        Apply();
        OnChanged?.Invoke();
    }
}
