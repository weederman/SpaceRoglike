using System;
using System.Collections.Generic;
using FORGE3D;
using UnityEngine;

/// <summary>
/// 플레이어 함선의 무기 슬롯 선택. 슬롯은 함선에 장착한 무기 목록(터렛 수만큼, 격납고에서 장착)이다.
/// 숫자키 1~9로 슬롯을 고르면 모든 터렛이 그 슬롯의 무기로 바뀌고, 좌클릭하면 모든 터렛이 함께 쏜다.
/// 무기를 바꾸는 순간 쏘던 터렛은 멈췄다가, 버튼을 누르고 있으면 새 무기로 다시 쏜다.
/// 슬롯 수는 함선의 터렛 수다(함선 기준 왼쪽 터렛부터 슬롯 1, 2...). 터렛이 늘면 자동으로 다시 센다.
/// 스토리 cue로 슬롯 순서대로 무기 종류를 바꿀 수 있다:
///   loadout:무기1,무기2,...   (F3DFXType 이름, 예: loadout:LaserImpulse,Vulcan)
/// CombatHud 프리팹(Assets/Resources/UI/CombatHud.prefab)에 붙어 함께 생성된다.
/// </summary>
public class PlayerWeaponSelector : MonoBehaviour
{
    private const string LoadoutPrefix = "loadout:";
    private const float RefreshInterval = 0.5f;

    public static PlayerWeaponSelector Instance { get; private set; }

    /// <summary>선택한 슬롯이나 슬롯의 무기 종류가 바뀌었을 때 (UI 갱신용)</summary>
    public event Action OnChanged;

    private readonly List<F3DPlayerTurretController> _turrets = new List<F3DPlayerTurretController>();
    private readonly List<F3DFXType> _weapons = new List<F3DFXType>();
    private SpaceshipController _ship;
    private float _nextRefresh;
    private int _switchFrame = -10;

    public int SelectedIndex { get; private set; }
    public int SlotCount => _weapons.Count;

    /// <summary>
    /// 플레이어 터렛이 지금 쏠 수 있는지. 모든 터렛이 함께 쏘고, 무기를 바꾼 직후 잠깐만 막는다
    /// (쏘던 터렛이 멈췄다가 새 무기로 다시 쏘게).
    /// </summary>
    public static bool CanFire()
    {
        return Instance == null || Time.frameCount - Instance._switchFrame > 1;
    }

    public F3DFXType GetWeapon(int slot)
    {
        return _weapons[slot];
    }

    private void Awake()
    {
        Instance = this;
        StoryPlayer.OnCue += HandleCue;
    }

    private void OnDestroy()
    {
        StoryPlayer.OnCue -= HandleCue;
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextRefresh)
            RefreshSlots();

        if (GameInputLock.IsLocked)
            return;

        for (int n = 0; n < Mathf.Min(_weapons.Count, 9); n++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + n) || Input.GetKeyDown(KeyCode.Keypad1 + n))
            {
                Select(n);
                return;
            }
        }
    }

    public void Select(int slot)
    {
        if (slot < 0 || slot >= _weapons.Count || slot == SelectedIndex)
            return;

        SelectedIndex = slot;
        ApplySelected();
        OnChanged?.Invoke();
    }

    /// <summary>모든 터렛을 고른 슬롯의 무기로 바꾼다</summary>
    private void ApplySelected()
    {
        if (_weapons.Count == 0)
            return;

        F3DFXType weapon = _weapons[SelectedIndex];
        bool changed = false;
        foreach (F3DPlayerTurretController turret in _turrets)
        {
            if (turret != null && turret.fxController != null && turret.fxController.DefaultFXType != weapon)
            {
                turret.fxController.DefaultFXType = weapon;
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

        // 슬롯은 터렛 수만큼. 새로 생긴 슬롯은 그 터렛이 처음 들고 있던 무기로 채운다
        for (int i = _weapons.Count; i < found.Count; i++)
            _weapons.Add(found[i].fxController.DefaultFXType);
        if (_weapons.Count > found.Count)
            _weapons.RemoveRange(found.Count, _weapons.Count - found.Count);

        _turrets.Clear();
        _turrets.AddRange(found);
        SelectedIndex = Mathf.Clamp(SelectedIndex, 0, Mathf.Max(0, _weapons.Count - 1));
        ApplySelected();
        OnChanged?.Invoke();
    }

    private void HandleCue(string cue)
    {
        if (cue.StartsWith(LoadoutPrefix))
            ApplyLoadout(cue.Substring(LoadoutPrefix.Length).Split(','));
    }

    /// <summary>슬롯 하나의 무기 종류를 바꾼다 (격납고에서 장착할 때)</summary>
    public void SetWeapon(int slot, F3DFXType weapon)
    {
        if (slot < 0 || slot >= _weapons.Count)
            return;

        _weapons[slot] = weapon;
        ApplySelected();
        OnChanged?.Invoke();
    }

    /// <summary>슬롯 순서대로 지금 무기 종류 이름 (저장에 쓴다)</summary>
    public string[] CurrentLoadout()
    {
        RefreshSlots();
        var names = new string[_weapons.Count];
        for (int i = 0; i < _weapons.Count; i++)
            names[i] = _weapons[i].ToString();
        return names;
    }

    /// <summary>슬롯 순서대로 무기 종류를 바꾼다 (F3DFXType 이름)</summary>
    public void ApplyLoadout(string[] names)
    {
        RefreshSlots();

        for (int i = 0; i < names.Length && i < _weapons.Count; i++)
        {
            string name = names[i].Trim();
            if (Enum.TryParse(name, out F3DFXType type))
                _weapons[i] = type;
            else
                Debug.LogWarning($"[Story] loadout의 무기 이름 '{name}'을 알 수 없습니다. (F3DFXType 이름을 쓰세요)");
        }

        if (names.Length > _weapons.Count)
            Debug.LogWarning($"[Story] loadout 무기 {names.Length}개 중 슬롯이 {_weapons.Count}개뿐이라 나머지는 무시합니다.");

        ApplySelected();
        OnChanged?.Invoke();
    }
}
