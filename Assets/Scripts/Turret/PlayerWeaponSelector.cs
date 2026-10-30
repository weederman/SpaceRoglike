using System;
using System.Collections.Generic;
using FORGE3D;
using UnityEngine;

/// <summary>
/// 플레이어 함선의 무기 슬롯. 슬롯은 함선에 장착한 무기 목록(터렛 수만큼, 격납고에서 장착)이다.
/// 터렛마다 자기 슬롯의 무기를 들고(함선 기준 왼쪽 터렛부터 슬롯 1, 2...), 좌클릭하면 모든 터렛이 함께 쏜다(일제 사격).
/// 무기를 바꾸는 순간 쏘던 터렛은 멈췄다가, 버튼을 누르고 있으면 새 무기로 다시 쏜다.
/// 슬롯 수는 함선의 터렛 수다. 터렛이 늘면 자동으로 다시 센다.
/// 슬롯의 무기는 무기 id(WeaponStatsTable·TurretItemData의 weaponId)다. 같은 이펙트의 변형 무기(강화 기관포 등)도 id로 구분한다.
/// 스토리 cue로 슬롯 순서대로 무기를 바꿀 수 있다:
///   loadout:무기1,무기2,...   (무기 id, 또는 F3DFXType 이름이면 그 종류의 기본형. 예: loadout:LaserImpulse,Vulcan)
/// CombatHud 프리팹(Assets/Resources/UI/CombatHud.prefab)에 붙어 함께 생성된다(씬을 다시 불러와도 남는다. 그때 무장은 비운다).
/// </summary>
public class PlayerWeaponSelector : MonoBehaviour
{
    private const string LoadoutPrefix = "loadout:";
    private const float RefreshInterval = 0.5f;

    public static PlayerWeaponSelector Instance { get; private set; }

    /// <summary>슬롯 수나 슬롯의 무기가 바뀌었을 때 (UI 갱신용)</summary>
    public event Action OnChanged;

    private readonly List<F3DPlayerTurretController> _turrets = new List<F3DPlayerTurretController>();
    private readonly List<string> _weapons = new List<string>();
    private SpaceshipController _ship;
    private float _nextRefresh;
    private int _switchFrame = -10;

    public int SlotCount => _weapons.Count;

    /// <summary>
    /// 플레이어 터렛이 지금 쏠 수 있는지. 모든 터렛이 함께 쏘고, 무기를 바꾼 직후 잠깐만 막는다
    /// (쏘던 터렛이 멈췄다가 새 무기로 다시 쏘게).
    /// </summary>
    public static bool CanFire()
    {
        return Instance == null || Time.frameCount - Instance._switchFrame > 1;
    }

    /// <summary>슬롯의 무기 id</summary>
    public string GetWeapon(int slot)
    {
        return _weapons[slot];
    }

    private void Awake()
    {
        Instance = this;
        StoryPlayer.OnCue += HandleCue;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        StoryPlayer.OnCue -= HandleCue;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (Instance == this)
            Instance = null;
    }

    // 패배 후 재시작: 이 컴포넌트는 씬을 다시 불러와도 남으므로(CombatHud) 지난 판의 무장을 버린다.
    // 새 함선의 터렛이 처음 들고 있던 무기로 다시 채우고, 이어 하기면 SaveSystem이 저장한 무장을 다시 단다.
    private void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        _ship = null;
        _turrets.Clear();
        _weapons.Clear();
        _nextRefresh = 0f;
        OnChanged?.Invoke();
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextRefresh)
            RefreshSlots();
    }

    /// <summary>터렛마다 자기 슬롯의 무기로 바꾼다</summary>
    private void ApplyWeapons()
    {
        bool changed = false;
        for (int i = 0; i < _turrets.Count && i < _weapons.Count; i++)
        {
            string weapon = _weapons[i];
            if (!WeaponStatsTable.TryGetType(weapon, out F3DFXType type))
                continue;

            F3DFXController fx = _turrets[i] != null ? _turrets[i].fxController : null;
            if (fx != null && (fx.DefaultFXType != type || fx.weaponId != weapon))
            {
                fx.DefaultFXType = type;
                fx.weaponId = weapon;
                changed = true;
            }
        }

        if (changed)
            _switchFrame = Time.frameCount;
    }

    private void RefreshSlots()
    {
        _nextRefresh = Time.unscaledTime + RefreshInterval;

        if (_ship == null)
            _ship = FindObjectOfType<SpaceshipController>();

        var found = new List<F3DPlayerTurretController>();
        if (_ship != null)
            found.AddRange(_ship.GetComponentsInChildren<F3DPlayerTurretController>(true));

        Transform ship = _ship != null ? _ship.transform : null;
        found.Sort((a, b) => ship.InverseTransformPoint(a.transform.position).x
            .CompareTo(ship.InverseTransformPoint(b.transform.position).x));

        bool same = found.Count == _turrets.Count;
        for (int i = 0; same && i < found.Count; i++)
            same = found[i] == _turrets[i];

        if (same)
            return;

        // 슬롯은 터렛 수만큼. 새로 생긴 슬롯은 그 터렛이 처음 들고 있던 무기(종류의 기본형)로 채운다
        for (int i = _weapons.Count; i < found.Count; i++)
            _weapons.Add(WeaponStatsTable.BaseId(found[i].fxController.DefaultFXType));
        if (_weapons.Count > found.Count)
            _weapons.RemoveRange(found.Count, _weapons.Count - found.Count);

        _turrets.Clear();
        _turrets.AddRange(found);
        ApplyWeapons();
        OnChanged?.Invoke();
    }

    private void HandleCue(string cue)
    {
        if (cue.StartsWith(LoadoutPrefix))
            ApplyLoadout(cue.Substring(LoadoutPrefix.Length).Split(','));
    }

    /// <summary>슬롯 하나의 무기를 바꾼다 (격납고에서 장착할 때)</summary>
    public void SetWeapon(int slot, string weapon)
    {
        if (slot < 0 || slot >= _weapons.Count)
            return;

        _weapons[slot] = weapon;
        ApplyWeapons();
        OnChanged?.Invoke();
    }

    /// <summary>슬롯 순서대로 지금 무기 id (저장에 쓴다)</summary>
    public string[] CurrentLoadout()
    {
        RefreshSlots();
        return _weapons.ToArray();
    }

    /// <summary>슬롯 순서대로 무기를 바꾼다 (무기 id 또는 F3DFXType 이름)</summary>
    public void ApplyLoadout(string[] names)
    {
        RefreshSlots();

        for (int i = 0; i < names.Length && i < _weapons.Count; i++)
        {
            string name = names[i].Trim();
            string id = WeaponStatsTable.ResolveId(name);
            if (id != null)
                _weapons[i] = id;
            else
                Debug.LogWarning($"[Story] loadout의 무기 이름 '{name}'을 알 수 없습니다. (무기 id나 F3DFXType 이름을 쓰세요)");
        }

        if (names.Length > _weapons.Count)
            Debug.LogWarning($"[Story] loadout 무기 {names.Length}개 중 슬롯이 {_weapons.Count}개뿐이라 나머지는 무시합니다.");

        ApplyWeapons();
        OnChanged?.Invoke();
    }
}
