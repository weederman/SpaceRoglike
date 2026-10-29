using System;
using System.Collections.Generic;
using FORGE3D;
using UnityEngine;

/// <summary>
/// 플레이어 함선의 무기 슬롯 선택. 숫자키 1~9로 슬롯을 고르면 좌클릭은 그 슬롯의 터렛만 쏜다.
/// 슬롯 순서는 함선 기준 왼쪽 터렛부터(함선 로컬 x가 작은 순). 상점에서 터렛이 늘면 자동으로 다시 센다.
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

    private readonly List<F3DPlayerTurretController> _slots = new List<F3DPlayerTurretController>();
    private SpaceshipController _ship;
    private float _nextRefresh;

    public int SelectedIndex { get; private set; }
    public int SlotCount => _slots.Count;

    /// <summary>이 터렛이 지금 쏠 수 있는 슬롯인지. 선택기가 없거나 슬롯에 없는 터렛이면 막지 않는다.</summary>
    public static bool CanFire(F3DPlayerTurretController turret)
    {
        if (Instance == null || Instance._slots.Count == 0)
            return true;

        int index = Instance._slots.IndexOf(turret);
        return index < 0 || index == Instance.SelectedIndex;
    }

    public F3DFXType GetWeapon(int slot)
    {
        return _slots[slot].fxController.DefaultFXType;
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

        for (int n = 0; n < Mathf.Min(_slots.Count, 9); n++)
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
        if (slot < 0 || slot >= _slots.Count || slot == SelectedIndex)
            return;

        SelectedIndex = slot;
        OnChanged?.Invoke();
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

        bool same = found.Count == _slots.Count;
        for (int i = 0; same && i < found.Count; i++)
            same = found[i] == _slots[i];

        if (same)
            return;

        _slots.Clear();
        _slots.AddRange(found);
        SelectedIndex = Mathf.Clamp(SelectedIndex, 0, Mathf.Max(0, _slots.Count - 1));
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
        if (slot < 0 || slot >= _slots.Count)
            return;

        _slots[slot].fxController.DefaultFXType = weapon;
        OnChanged?.Invoke();
    }

    /// <summary>슬롯 순서대로 지금 무기 종류 이름 (저장에 쓴다)</summary>
    public string[] CurrentLoadout()
    {
        RefreshSlots();
        var names = new string[_slots.Count];
        for (int i = 0; i < _slots.Count; i++)
            names[i] = _slots[i].fxController.DefaultFXType.ToString();
        return names;
    }

    /// <summary>슬롯 순서대로 무기 종류를 바꾼다 (F3DFXType 이름)</summary>
    public void ApplyLoadout(string[] names)
    {
        RefreshSlots();

        for (int i = 0; i < names.Length && i < _slots.Count; i++)
        {
            string name = names[i].Trim();
            if (Enum.TryParse(name, out F3DFXType type))
                _slots[i].fxController.DefaultFXType = type;
            else
                Debug.LogWarning($"[Story] loadout의 무기 이름 '{name}'을 알 수 없습니다. (F3DFXType 이름을 쓰세요)");
        }

        if (names.Length > _slots.Count)
            Debug.LogWarning($"[Story] loadout 무기 {names.Length}개 중 슬롯이 {_slots.Count}개뿐이라 나머지는 무시합니다.");

        OnChanged?.Invoke();
    }
}
