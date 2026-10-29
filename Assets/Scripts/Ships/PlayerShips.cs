using System;
using System.Collections.Generic;
using System.Linq;
using FORGE3D;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어가 가진 함선과 지금 타고 있는 함선 (ShipCatalog의 id).
///  - 처음에는 기본 함선(스펙터) 하나. 메인 퀘스트 보상으로 새 함선을 받으면(Grant) 바로 그 함선을 탄다.
///  - 함선을 바꾸면(Switch) 플레이어 함선 오브젝트는 그대로 두고 겉모습·터렛 수·기본 성능만 바꾼다.
///  - 무기는 함선마다 따로 단다(함선별 무장 기억). 갈아타면 내리는 함선의 무장을 기억해 두고, 타는 함선의 무장을 단다.
///    새 함선을 처음 받으면 지금 무장을 그대로 옮겨 싣고(넘치면 보관함, 모자라면 기본 기관포), 내린 함선에는 기본 기관포를 남긴다.
///  - 장비는 함선끼리 함께 쓴다. 장비 칸이 줄면 넘치는 장비는 장비 보관함으로 옮긴다.
/// 스토리 cue로 함선을 줄 수 있다:  ship_grant:함선id   (예: ship_grant:ironwall)
/// 씬을 다시 시작하면 기본 함선으로 돌아가고, 저장(SaveSystem)에 함께 남는다.
/// </summary>
public static class PlayerShips
{
    private const string GrantPrefix = "ship_grant:";

    private static readonly List<string> Owned = new List<string>();
    private static readonly Dictionary<string, string[]> Loadouts = new Dictionary<string, string[]>();
    private static string _current;

    /// <summary>보유 함선이나 탑승 함선이 바뀌었을 때 (UI 갱신용)</summary>
    public static event Action OnChanged;

    public static IReadOnlyList<string> OwnedIds => Owned;
    public static string CurrentId => Current != null ? Current.id : null;

    /// <summary>지금 타고 있는 함선 (카탈로그가 없으면 null)</summary>
    public static ShipCatalog.Ship Current
    {
        get
        {
            ShipCatalog catalog = ShipCatalog.Load();
            if (catalog == null)
                return null;
            return catalog.Find(_current) ?? catalog.Default;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Owned.Clear();
        Loadouts.Clear();
        _current = null;
        OnChanged = null;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // StoryPlayer가 플레이를 시작할 때 OnCue를 비우므로(정적 초기화) 씬을 불러올 때 구독한다 (중복 없이)
        StoryPlayer.OnCue -= HandleCue;
        StoryPlayer.OnCue += HandleCue;

        // 다시 시작하면 기본 함선. 이어 하기는 SaveSystem이 다시 채운다
        Owned.Clear();
        Loadouts.Clear();
        _current = null;
        ShipCatalog.Ship start = Current;
        if (start != null)
            Owned.Add(start.id);
    }

    public static bool Owns(string id)
    {
        return Owned.Contains(id);
    }

    /// <summary>함선을 준다(이미 있으면 그대로). switchTo면 바로 그 함선을 탄다.</summary>
    public static bool Grant(string id, bool switchTo = true)
    {
        ShipCatalog catalog = ShipCatalog.Load();
        if (catalog == null || catalog.Find(id) == null)
        {
            Debug.LogWarning($"[Ships] '{id}' 함선이 없습니다. (Assets/Resources/{ShipCatalog.ResourcePath}.asset)");
            return false;
        }

        bool isNew = !Owned.Contains(id);
        if (isNew)
            Owned.Add(id);

        if (!switchTo || id == CurrentId)
        {
            OnChanged?.Invoke();
            return true;
        }

        // 처음 받은 함선: 지금 무장을 옮겨 싣고, 내리는 함선에는 기본 기관포를 남긴다
        if (isNew || !Loadouts.ContainsKey(id))
        {
            string[] carried = CurrentLoadout();
            ShipCatalog.Ship leaving = Current;
            if (leaving != null)
                Loadouts[leaving.id] = DefaultLoadout(leaving.weaponSlots);
            Loadouts[id] = Fit(carried, catalog.Find(id).weaponSlots, true);
            _current = id;
            Apply();
            OnChanged?.Invoke();
            return true;
        }

        Switch(id);
        return true;
    }

    /// <summary>가진 함선으로 갈아탄다. 진행 중인 임무가 있으면 못 바꾼다.</summary>
    public static bool CanSwitch(string id, out string reason)
    {
        reason = null;
        if (!Owned.Contains(id))
            reason = "가지고 있지 않은 함선입니다.";
        else if (id == CurrentId)
            reason = "지금 타고 있는 함선입니다.";
        else if (MissionDirector.IsRunning)
            reason = "임무를 진행하는 동안에는 함선을 바꿀 수 없습니다.";
        return reason == null;
    }

    public static bool Switch(string id)
    {
        if (!Owned.Contains(id))
            return false;

        // 내리는 함선의 무장을 기억해 둔다. 무장 기록이 없는 함선(예전 저장)은 기본 기관포로 (무기가 두 배가 되지 않게)
        ShipCatalog.Ship leaving = Current;
        if (leaving != null && leaving.id != id)
            Loadouts[leaving.id] = CurrentLoadout();
        if (!Loadouts.ContainsKey(id))
            Loadouts[id] = DefaultLoadout(ShipCatalog.Load().Find(id).weaponSlots);

        _current = id;
        Apply();
        OnChanged?.Invoke();
        return true;
    }

    private static string[] CurrentLoadout()
    {
        return PlayerWeaponSelector.Instance != null ? PlayerWeaponSelector.Instance.CurrentLoadout() : new string[0];
    }

    private static string[] DefaultLoadout(int slots)
    {
        var list = new string[slots];
        for (int i = 0; i < slots; i++)
            list[i] = WeaponStatsTable.BaseId(F3DFXType.Vulcan);
        return list;
    }

    /// <summary>무장을 슬롯 수에 맞춘다. 모자라면 기본 기관포, 넘치면(spill이면) 보관함으로.</summary>
    private static string[] Fit(string[] loadout, int slots, bool spill)
    {
        var list = new string[slots];
        for (int i = 0; i < slots; i++)
            list[i] = i < loadout.Length ? loadout[i] : WeaponStatsTable.BaseId(F3DFXType.Vulcan);
        for (int i = slots; spill && i < loadout.Length; i++)
            PlayerHangar.Add(loadout[i]);
        return list;
    }

    /// <summary>
    /// 지금 함선을 플레이어 함선 오브젝트에 적용한다: 겉모습, 터렛 수(무기 슬롯)와 그 함선의 무장, 장비(칸 수·기본 성능).
    /// </summary>
    public static void Apply()
    {
        ShipCatalog.Ship ship = Current;
        var controller = UnityEngine.Object.FindObjectOfType<SpaceshipController>();
        if (ship == null || controller == null)
            return;

        PlayerShipAppearance.For(controller).SetShip(ship.look);
        ApplyTurrets(controller, ship);
        if (PlayerWeaponSelector.Instance != null && Loadouts.TryGetValue(ship.id, out string[] loadout))
            PlayerWeaponSelector.Instance.ApplyLoadout(Fit(loadout, PlayerWeaponSelector.Instance.SlotCount, false));
        PlayerEquipment.FitToSlots();
        PlayerEquipment.Apply();
    }

    // ── 터렛 ───────────────────────────────────────────

    /// <summary>함선에 더 단 터렛 표시 (기본 터렛과 구분해 뗄 때 쓴다)</summary>
    private class ExtraTurret : MonoBehaviour { }

    private static void ApplyTurrets(SpaceshipController controller, ShipCatalog.Ship ship)
    {
        Transform root = controller.transform;
        List<Transform> extras = root.GetComponentsInChildren<ExtraTurret>(true).Select(e => e.transform).ToList();
        List<Transform> baseTurrets = root.GetComponentsInChildren<F3DPlayerTurretController>(true)
            .Select(t => TopUnder(root, t.transform))
            .Where(t => t != null && t.GetComponent<ExtraTurret>() == null)
            .Distinct()
            .ToList();

        int wantExtras = Mathf.Max(0, ship.weaponSlots - baseTurrets.Count);
        wantExtras = Mathf.Min(wantExtras, ship.extraTurretMounts.Length);
        if (extras.Count == wantExtras && extras.Select(e => e.localPosition).SequenceEqual(ship.extraTurretMounts.Take(wantExtras)))
            return;

        // 터렛 수가 바뀌어도 슬롯 무기가 섞이지 않게 지금 무장을 붙잡아 둔다 (어느 무장을 달지는 Apply가 정한다)
        PlayerWeaponSelector selector = PlayerWeaponSelector.Instance;
        string[] before = selector != null ? selector.CurrentLoadout() : new string[0];

        // 부모에서 먼저 떼어야 같은 프레임의 터렛 세기에서 빠진다
        foreach (Transform extra in extras)
        {
            extra.SetParent(null);
            UnityEngine.Object.Destroy(extra.gameObject);
        }

        Transform template = baseTurrets.Count > 0 ? baseTurrets[0] : null;
        for (int i = 0; i < wantExtras && template != null; i++)
        {
            Transform turret = UnityEngine.Object.Instantiate(template, root);
            turret.name = $"{template.name}_Extra{i + 1}";
            turret.localPosition = ship.extraTurretMounts[i];
            turret.localRotation = template.localRotation;
            turret.localScale = template.localScale;
            turret.gameObject.AddComponent<ExtraTurret>();

            // 새 터렛은 기본 기관포로 시작한다 (원본 터렛의 무기를 복사하지 않는다)
            foreach (F3DFXController fx in turret.GetComponentsInChildren<F3DFXController>(true))
            {
                fx.Stop();
                fx.DefaultFXType = F3DFXType.Vulcan;
                fx.weaponId = null;
            }
        }

        if (selector != null)
            selector.ApplyLoadout(Fit(before, baseTurrets.Count + wantExtras, false));
    }

    private static Transform TopUnder(Transform root, Transform t)
    {
        while (t != null && t.parent != root)
            t = t.parent;
        return t;
    }

    private static void HandleCue(string cue)
    {
        if (cue.StartsWith(GrantPrefix))
            Grant(cue.Substring(GrantPrefix.Length).Trim());
    }

    // ── 저장 ───────────────────────────────────────────

    public static string[] OwnedForSave()
    {
        return Owned.ToArray();
    }

    /// <summary>저장용: 타고 있지 않은 함선들의 무장 ("함선id=무기1,무기2,...")</summary>
    public static string[] LoadoutsForSave()
    {
        return Loadouts.Where(p => p.Key != CurrentId).Select(p => $"{p.Key}={string.Join(",", p.Value)}").ToArray();
    }

    /// <summary>
    /// 불러오기용: 보유 함선과 탑승 함선, 다른 함선의 무장을 이 상태로 바꾸고 적용한다 (예전 저장은 비어 있다 → 기본 함선).
    /// 탑승 함선의 무장은 저장의 loadout으로 따로 단다.
    /// </summary>
    public static void Restore(string current, string[] owned, string[] loadouts)
    {
        ShipCatalog catalog = ShipCatalog.Load();
        Owned.Clear();
        Loadouts.Clear();
        foreach (string entry in loadouts ?? new string[0])
        {
            int eq = entry.IndexOf('=');
            if (eq <= 0)
                continue;
            string[] weapons = entry.Substring(eq + 1).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(WeaponStatsTable.ResolveId).Where(w => w != null).ToArray();
            Loadouts[entry.Substring(0, eq)] = weapons;
        }

        if (catalog != null)
        {
            foreach (string id in owned ?? new string[0])
            {
                if (catalog.Find(id) != null && !Owned.Contains(id))
                    Owned.Add(id);
            }

            ShipCatalog.Ship start = catalog.Default;
            if (start != null && !Owned.Contains(start.id))
                Owned.Insert(0, start.id);
        }

        _current = Owned.Contains(current) ? current : null;
        Apply();
        OnChanged?.Invoke();
    }
}
