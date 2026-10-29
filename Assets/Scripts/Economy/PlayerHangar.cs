using System;
using System.Collections.Generic;
using FORGE3D;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어의 격납고 보관함: 사 두었지만 함선에 달려 있지 않은 무기들. 함선에 달린 무기는 PlayerWeaponSelector의 슬롯이다.
///  - 상점에서 산 무기는 보관함에 들어간다 (Add)
///  - 장착(Equip): 보관함의 무기를 슬롯에 달고, 그 슬롯에 있던 무기는 보관함으로 돌아온다
/// 씬을 다시 시작하면 비워지고, 저장(SaveSystem)에 함께 남는다.
/// </summary>
public static class PlayerHangar
{
    private static readonly List<F3DFXType> Stored = new List<F3DFXType>();

    /// <summary>보관함이나 장착이 바뀌었을 때 (UI 갱신용)</summary>
    public static event Action OnChanged;

    public static IReadOnlyList<F3DFXType> Items => Stored;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Stored.Clear();
        OnChanged = null;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Stored.Clear(); // 다시 시작(패배 후 재시작 등)하면 빈 보관함. 이어 하기는 SaveSystem이 다시 채운다
    }

    public static void Add(F3DFXType weapon)
    {
        Stored.Add(weapon);
        OnChanged?.Invoke();
    }

    /// <summary>보관함에서 weapon 하나를 뺀다 (상점에 팔 때). 없으면 false.</summary>
    public static bool Remove(F3DFXType weapon)
    {
        if (!Stored.Remove(weapon))
            return false;

        OnChanged?.Invoke();
        return true;
    }

    /// <summary>보관함의 weapon을 slot에 단다. 그 슬롯에 있던 무기는 보관함으로 돌아온다.</summary>
    public static bool Equip(F3DFXType weapon, int slot)
    {
        PlayerWeaponSelector selector = PlayerWeaponSelector.Instance;
        if (selector == null || slot < 0 || slot >= selector.SlotCount || !Stored.Contains(weapon))
            return false;

        F3DFXType previous = selector.GetWeapon(slot);
        Stored.Remove(weapon);
        Stored.Add(previous);
        selector.SetWeapon(slot, weapon);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>저장용: 보관 중인 무기 종류 이름</summary>
    public static string[] ToNames()
    {
        var names = new string[Stored.Count];
        for (int i = 0; i < Stored.Count; i++)
            names[i] = Stored[i].ToString();
        return names;
    }

    /// <summary>불러오기용: 보관함을 이 목록으로 바꾼다</summary>
    public static void Restore(string[] names)
    {
        Stored.Clear();
        if (names != null)
        {
            foreach (string name in names)
            {
                if (Enum.TryParse(name, out F3DFXType type))
                    Stored.Add(type);
            }
        }

        OnChanged?.Invoke();
    }
}
