using System.Collections.Generic;
using FORGE3D;
using UnityEngine;

/// <summary>
/// 개발자 모드(DevConsole)가 바꾼 값을 담는 정적 저장소.
/// 값이 null이면 "건드리지 않음"(프리팹/에셋의 원래 값 사용)이다. 에셋은 수정하지 않고 런타임에만 덮어쓰며,
/// 씬을 다시 불러와도 유지되고 새로 스폰되는 적에게도 적용된다. 개발자 모드 UI 자체는 DevConsole(에디터/개발 빌드 전용).
/// </summary>
public static class DevTuning
{
    /// <summary>개발자 모드 창이 열려 있어 게임 입력(이동/발사/줌)을 막아야 하는지</summary>
    public static bool InputBlocked;

    // ── 적 ─────────────────────────────────────────────
    public static float? EnemyCount;
    public static float? EnemyRespawnDelay;
    public static float? EnemyHp;
    public static float? EnemyShield;
    public static float? EnemySpeed;
    public static float? EnemyAcceleration;
    public static float? EnemyOrbitDistance;
    public static float? EnemyFireInterval;
    public static float? EnemyFireRange;

    /// <summary>모든 무기에 쓰는 적 공격력. 무기별 값이 있으면 그쪽이 우선.</summary>
    public static float? EnemyDamageAll;
    public static readonly Dictionary<F3DFXType, float> EnemyDamageByWeapon = new Dictionary<F3DFXType, float>();

    // ── 플레이어 ───────────────────────────────────────
    public static float? PlayerSpeed;
    public static float? PlayerAcceleration;
    public static float? PlayerHp;
    public static float? PlayerShield;
    public static bool PlayerGod;

    // 에디터에서 도메인 리로드를 꺼 둬도 플레이마다 깨끗이 시작하도록
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Clear()
    {
        InputBlocked = false;

        EnemyCount = null;
        EnemyRespawnDelay = null;
        EnemyHp = null;
        EnemyShield = null;
        EnemySpeed = null;
        EnemyAcceleration = null;
        EnemyOrbitDistance = null;
        EnemyFireInterval = null;
        EnemyFireRange = null;
        EnemyDamageAll = null;
        EnemyDamageByWeapon.Clear();

        PlayerSpeed = null;
        PlayerAcceleration = null;
        PlayerHp = null;
        PlayerShield = null;
        PlayerGod = false;
    }

    /// <summary>WeaponStatsTable이 적 공격력을 돌려주기 직전에 호출한다.</summary>
    public static float OverrideEnemyDamage(F3DFXType type, float damage)
    {
        if (EnemyDamageByWeapon.TryGetValue(type, out float perWeapon))
            return perWeapon;

        return EnemyDamageAll ?? damage;
    }

    /// <summary>적 한 기에 덮어쓸 값을 적용한다. 스폰 직후와, 값을 바꾼 직후 살아있는 적에게 호출한다.</summary>
    public static void ApplyToEnemy(GameObject enemy)
    {
        if (enemy == null)
            return;

        if (EnemyHp.HasValue)
        {
            var hull = enemy.GetComponentInChildren<HullHealth>();
            if (hull != null)
                hull.SetMaxHp(EnemyHp.Value);
        }

        if (EnemyShield.HasValue)
        {
            var shield = enemy.GetComponentInChildren<ShieldHealth>();
            if (shield != null)
                shield.SetMaxHp(EnemyShield.Value);
        }

        var move = enemy.GetComponentInChildren<ShipMovementBase>();
        if (move != null)
        {
            if (EnemySpeed.HasValue)
                move.MaxSpeed = EnemySpeed.Value;
            if (EnemyAcceleration.HasValue)
                move.Acceleration = EnemyAcceleration.Value;

            if (EnemyOrbitDistance.HasValue && move is EnemyOrbitController orbit)
                orbit.OrbitDistance = EnemyOrbitDistance.Value;
        }

        if (EnemyFireInterval.HasValue || EnemyFireRange.HasValue)
        {
            foreach (var turret in enemy.GetComponentsInChildren<EnemyTurretController>())
            {
                if (EnemyFireInterval.HasValue)
                    turret.fireInterval = EnemyFireInterval.Value;
                if (EnemyFireRange.HasValue)
                    turret.fireRange = EnemyFireRange.Value;
            }
        }
    }

    /// <summary>플레이어 함선에 덮어쓸 값을 적용한다. 씬이 (다시) 로드될 때와 값을 바꾼 직후 호출한다.</summary>
    /// <returns>플레이어 함선을 찾았는지</returns>
    public static bool ApplyToPlayer()
    {
        var player = Object.FindObjectOfType<SpaceshipController>();
        if (player == null)
            return false;

        if (PlayerSpeed.HasValue)
            player.MaxSpeed = PlayerSpeed.Value;
        if (PlayerAcceleration.HasValue)
            player.Acceleration = PlayerAcceleration.Value;

        // 최대 체력은 값이 다를 때만 바꾼다 (다른 명령을 칠 때마다 체력이 가득 차지 않도록)
        var hull = player.GetComponentInChildren<HullHealth>();
        if (hull != null)
        {
            if (PlayerHp.HasValue && !Mathf.Approximately(hull.MaxHp, PlayerHp.Value))
                hull.SetMaxHp(PlayerHp.Value);
            hull.Invincible = PlayerGod;
        }

        var shield = player.GetComponentInChildren<ShieldHealth>();
        if (shield != null)
        {
            if (PlayerShield.HasValue && !Mathf.Approximately(shield.MaxHp, PlayerShield.Value))
                shield.SetMaxHp(PlayerShield.Value);
            shield.Invincible = PlayerGod;
        }

        return true;
    }

    /// <summary>플레이어 선체와 쉴드를 가득 채운다(깨진 쉴드는 다시 켠다).</summary>
    /// <returns>플레이어 함선을 찾았는지</returns>
    public static bool HealPlayer()
    {
        var player = Object.FindObjectOfType<SpaceshipController>();
        if (player == null)
            return false;

        var hull = player.GetComponentInChildren<HullHealth>();
        if (hull != null)
            hull.Restore(1f);

        var shield = player.GetComponentInChildren<ShieldHealth>();
        if (shield != null)
            shield.Restore(1f);

        return true;
    }
}
