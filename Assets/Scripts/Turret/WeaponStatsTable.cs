using System;
using System.Collections.Generic;
using UnityEngine;
using FORGE3D;

/// <summary>무기를 쏘는 쪽. 같은 무기라도 쏘는 쪽에 따라 공격력을 다르게 줄 수 있다.</summary>
public enum WeaponOwner
{
    Player,
    Enemy
}

/// <summary>
/// 무기별 공격력 / 발사 간격 / 쉴드·아머 배율을 한곳에서 관리하는 데이터 테이블(단일 출처).
/// 발사체(F3DProjectile)·빔(F3DBeam)의 데미지와 F3DFXController의 발사 간격은 모두 여기서만 읽는다.
/// 프리팹이나 컨트롤러에는 같은 의미의 수치를 따로 두지 않는다. 적이 쏘는 경우의 공격력은 enemyDamage 열에 둔다.
/// 새 무기를 쓰려면 이 표에 항목을 추가해야 하며, 없으면 경고를 한 번 남기고 안전한 기본값을 쓴다.
/// 위치: Assets/Resources/WeaponStatsTable.asset
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

        [Tooltip("적이 이 무기를 쏠 때의 공격력. 0이면 위의 공격력과 같다.")]
        [Min(0f)] public float enemyDamage = 0f;

        [Tooltip("발사 간격(초). 0이면 지속형(빔).")]
        [Min(0f)] public float fireInterval = 0.2f;

        [Tooltip("쉴드에 주는 데미지 배율 (1 = 그대로, 2 = 2배, 0.5 = 절반)")]
        [Min(0f)] public float shieldMultiplier = 1f;

        [Tooltip("아머(선체)에 주는 데미지 배율")]
        [Min(0f)] public float armorMultiplier = 1f;

        [Tooltip("초당 공격력 (자동 계산, 수정해도 덮어써짐)")]
        public float damagePerSecond;
    }

    // 표에 없는 무기(또는 표 에셋을 못 찾은 경우)에 쓰는 안전한 기본값
    private const float FallbackDamage = 10f;
    private const float FallbackInterval = 0.5f;
    private const float MinInterval = 0.02f;

    [SerializeField] private Entry[] _entries = new Entry[0];

    private static WeaponStatsTable _instance;
    private static readonly HashSet<F3DFXType> _warned = new HashSet<F3DFXType>();
    private static readonly Entry _fallback = new Entry();

    private static WeaponStatsTable Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<WeaponStatsTable>("WeaponStatsTable");
            return _instance;
        }
    }

    private static Entry Find(F3DFXType type)
    {
        var table = Instance;
        if (table != null)
        {
            foreach (var e in table._entries)
                if (e.weaponType == type)
                    return e;
        }

        if (_warned.Add(type))
        {
            Debug.LogWarning(
                $"[WeaponStatsTable] {type} 항목이 없어 기본값(공격력 {FallbackDamage}, 간격 {FallbackInterval}초, 배율 1)을 씁니다. " +
                "Assets/Resources/WeaponStatsTable.asset에 항목을 추가하세요.");
        }

        _fallback.damage = FallbackDamage;
        _fallback.fireInterval = FallbackInterval;
        _fallback.shieldMultiplier = 1f;
        _fallback.armorMultiplier = 1f;
        return _fallback;
    }

    /// <summary>1회(지속형 빔은 초당) 공격력. 적이 쏘고 enemyDamage가 설정돼 있으면 그 값.</summary>
    public static float GetDamage(F3DFXType type, WeaponOwner owner = WeaponOwner.Player)
    {
        var e = Find(type);
        bool byEnemy = owner == WeaponOwner.Enemy;
        float damage = byEnemy && e.enemyDamage > 0f ? e.enemyDamage : e.damage;

        // 개발자 모드가 덮어쓴 값은 에셋을 건드리지 않고 여기서만 적용한다
        return byEnemy ? DevTuning.OverrideEnemyDamage(type, damage) : damage;
    }

    /// <summary>발사 간격(초). 0 이하로 설정돼 타이머가 폭주하지 않도록 최소값을 둔다.</summary>
    public static float GetFireInterval(F3DFXType type)
    {
        return Mathf.Max(MinInterval, Find(type).fireInterval);
    }

    public static float GetShieldMultiplier(F3DFXType type)
    {
        return Find(type).shieldMultiplier;
    }

    public static float GetArmorMultiplier(F3DFXType type)
    {
        return Find(type).armorMultiplier;
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
