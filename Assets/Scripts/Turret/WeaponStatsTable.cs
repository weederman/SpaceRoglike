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
/// 같은 무기 종류(이펙트)의 변형(강화 기관포·중형 미사일 등)은 id가 다른 항목으로 둔다. 플레이어 무기는 id(TurretItemData.weaponId)로,
/// 적처럼 무기 종류로만 찾을 때는 그 종류의 첫 항목(기본형)을 쓴다.
/// 위치: Assets/Resources/WeaponStatsTable.asset
/// </summary>
[CreateAssetMenu(fileName = "WeaponStatsTable", menuName = "SpaceRoglike/Weapon Stats Table")]
public class WeaponStatsTable : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("인스펙터 목록에 표시되는 이름 (id와 무기 종류에서 자동 입력)")]
        public string name;
        [Tooltip("무기 id (TurretItemData.weaponId, 저장에도 쓴다). 같은 무기 종류의 첫 항목이 그 종류의 기본형이다.")]
        public string id;
        public F3DFXType weaponType;

        [Tooltip("화면(무기 선택 표시 등)에 보이는 이름. 비우면 무기 종류 이름을 쓴다.")]
        public string displayName;

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

        [Tooltip("락온한 표적을 향해 날아가는 유도탄인지 (발사체 무기만)")]
        public bool homing;

        [Tooltip("유도탄이 방향을 트는 속도 (초당 각도)")]
        [Min(0f)] public float homingTurnRate = 120f;

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

    /// <summary>id의 항목. id가 비었거나 없거나 무기 종류가 다르면 그 종류의 기본형.</summary>
    private static Entry Find(F3DFXType type, string id)
    {
        if (!string.IsNullOrEmpty(id))
        {
            Entry byId = FindId(id);
            if (byId != null && byId.weaponType == type)
                return byId;
        }

        return Find(type);
    }

    private static Entry FindId(string id)
    {
        var table = Instance;
        if (table == null || string.IsNullOrEmpty(id))
            return null;

        foreach (var e in table._entries)
            if (e.id == id)
                return e;
        return null;
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
    public static float GetDamage(F3DFXType type, WeaponOwner owner = WeaponOwner.Player, string id = null)
    {
        var e = Find(type, id);
        bool byEnemy = owner == WeaponOwner.Enemy;
        float damage = byEnemy && e.enemyDamage > 0f ? e.enemyDamage : e.damage;

        // 개발자 모드가 덮어쓴 값은 에셋을 건드리지 않고 여기서만 적용한다
        return byEnemy ? DevTuning.OverrideEnemyDamage(type, damage) : damage;
    }

    /// <summary>발사 간격(초). 0 이하로 설정돼 타이머가 폭주하지 않도록 최소값을 둔다.</summary>
    public static float GetFireInterval(F3DFXType type, string id = null)
    {
        return Mathf.Max(MinInterval, Find(type, id).fireInterval);
    }

    /// <summary>발사 간격이 0인 지속형(빔)인지</summary>
    public static bool IsContinuous(F3DFXType type, string id = null)
    {
        return Find(type, id).fireInterval <= 0f;
    }

    /// <summary>플레이어 기준 초당 공격력 (지속형은 공격력 그대로)</summary>
    public static float GetDps(F3DFXType type, string id = null)
    {
        var e = Find(type, id);
        return e.fireInterval > 0f ? e.damage / e.fireInterval : e.damage;
    }

    /// <summary>화면에 보이는 무기 이름. 표에 이름이 없으면 무기 종류 이름.</summary>
    public static string GetDisplayName(F3DFXType type, string id = null)
    {
        var e = Find(type, id);
        return string.IsNullOrEmpty(e.displayName) ? type.ToString() : e.displayName;
    }

    public static float GetShieldMultiplier(F3DFXType type, string id = null)
    {
        return Find(type, id).shieldMultiplier;
    }

    public static float GetArmorMultiplier(F3DFXType type, string id = null)
    {
        return Find(type, id).armorMultiplier;
    }

    /// <summary>락온한 표적을 쫓는 유도탄인지</summary>
    public static bool IsHoming(F3DFXType type, string id = null)
    {
        return Find(type, id).homing;
    }

    public static float GetHomingTurnRate(F3DFXType type, string id = null)
    {
        return Find(type, id).homingTurnRate;
    }

    /// <summary>무기 id의 무기 종류(이펙트). 없으면 false.</summary>
    public static bool TryGetType(string id, out F3DFXType type)
    {
        Entry e = FindId(id);
        type = e != null ? e.weaponType : default;
        return e != null;
    }

    /// <summary>무기 종류의 기본형 id (표의 그 종류 첫 항목, 없으면 종류 이름)</summary>
    public static string BaseId(F3DFXType type)
    {
        var table = Instance;
        if (table != null)
        {
            foreach (var e in table._entries)
                if (e.weaponType == type && !string.IsNullOrEmpty(e.id))
                    return e.id;
        }

        return type.ToString();
    }

    /// <summary>
    /// 저장·스토리 cue의 무기 이름을 무기 id로 바꾼다. 무기 id면 그대로, 예전 저장의 무기 종류 이름(F3DFXType, 예: Vulcan)이면 그 종류의 기본형 id.
    /// 알 수 없으면 null.
    /// </summary>
    public static string ResolveId(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        if (FindId(name) != null)
            return name;
        return Enum.TryParse(name, out F3DFXType type) ? BaseId(type) : null;
    }

    private void OnValidate()
    {
        if (_entries == null)
            return;

        foreach (var e in _entries)
        {
            e.name = string.IsNullOrEmpty(e.id) ? e.weaponType.ToString() : $"{e.id} ({e.weaponType})";
            e.damagePerSecond = e.fireInterval > 0f ? e.damage / e.fireInterval : e.damage;
        }
    }
}
