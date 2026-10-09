using System;
using UnityEngine;
using FORGE3D;

/// <summary>
/// 무기별 공격력 / 발사 간격 / 쉴드·아머 배율을 한곳에서 관리하는 데이터 테이블.
/// 인스펙터로 값을 확인·수정하면 발사체·빔의 데미지와 F3DFXController의 발사 간격에 그대로 반영된다.
/// 테이블에 없는 무기는 기존 프리팹/코드 값을 그대로 쓴다. 위치: Assets/Resources/WeaponStatsTable.asset
/// </summary>
[CreateAssetMenu(fileName = "WeaponStatsTable", menuName = "SpaceRoglike/Weapon Stats Table")]
public class WeaponStatsTable : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("인스펙터 목록에 표시되는 이름 (무기 종류에서 자동 입력)")]
        public string name;
        public F3DFXType weaponType;

        [Tooltip("1회 공격력. 지속형 빔(발사 간격 0)은 초당 공격력(DPS).")]
        [Min(0f)] public float damage = 10f;

        [Tooltip("발사 간격(초). 0이면 지속형(빔)이거나 코드 기본값을 쓴다.")]
        [Min(0f)] public float fireInterval = 0.2f;

        [Tooltip("쉴드에 주는 데미지 배율 (1 = 그대로, 2 = 2배, 0.5 = 절반)")]
        [Min(0f)] public float shieldMultiplier = 1f;

        [Tooltip("아머(선체)에 주는 데미지 배율")]
        [Min(0f)] public float armorMultiplier = 1f;

        [Tooltip("초당 공격력 (자동 계산, 수정해도 덮어써짐)")]
        public float damagePerSecond;
    }

    [SerializeField] private Entry[] _entries = new Entry[0];

    private static WeaponStatsTable _instance;

    private static WeaponStatsTable Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<WeaponStatsTable>("WeaponStatsTable");
            return _instance;
        }
    }

    private static bool TryGet(F3DFXType type, out Entry entry)
    {
        entry = null;
        var table = Instance;
        if (table == null)
            return false;

        foreach (var e in table._entries)
        {
            if (e.weaponType == type)
            {
                entry = e;
                return true;
            }
        }

        return false;
    }

    /// <summary>1회(또는 초당) 공격력. 테이블에 없으면 fallback(프리팹 값).</summary>
    public static float GetDamage(F3DFXType type, float fallback)
    {
        return TryGet(type, out var e) ? e.damage : fallback;
    }

    /// <summary>발사 간격(초). 테이블에 없거나 0이면 fallback(코드 기본값).</summary>
    public static float GetFireInterval(F3DFXType type, float fallback)
    {
        return TryGet(type, out var e) && e.fireInterval > 0f ? e.fireInterval : fallback;
    }

    public static float GetShieldMultiplier(F3DFXType type)
    {
        return TryGet(type, out var e) ? e.shieldMultiplier : 1f;
    }

    public static float GetArmorMultiplier(F3DFXType type)
    {
        return TryGet(type, out var e) ? e.armorMultiplier : 1f;
    }

    private void OnValidate()
    {
        if (_entries == null)
            return;

        foreach (var e in _entries)
        {
            e.name = e.weaponType.ToString();
            e.damagePerSecond = e.fireInterval > 0f ? e.damage / e.fireInterval : e.damage;
        }
    }
}
