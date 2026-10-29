using FORGE3D;
using UnityEngine;

/// <summary>
/// 격납고·상점 '세부정보'에 보여줄 무기 성능. 따로 적어 두지 않고 실제로 쓰는 값에서 읽는다.
///  - 데미지·발사 간격·DPS: WeaponStatsTable (플레이어 기준)
///  - 사정거리·탄속: 터렛 프리팹(F3DFXController)이 쏘는 발사체(F3DProjectile 속도 × 수명)나 빔(F3DBeam 최대 길이)
/// </summary>
public static class WeaponSpecs
{
    public struct Specs
    {
        public float damage;
        /// <summary>초당 발사 수 (빔은 0 = 지속)</summary>
        public float shotsPerSecond;
        public float dps;
        public float range;
        /// <summary>발사체 속도 (빔은 0 = 즉시)</summary>
        public float projectileSpeed;
        public bool isBeam;
    }

    public static Specs Get(TurretItemData item)
    {
        F3DFXType type = item.weaponType;
        var specs = new Specs
        {
            damage = WeaponStatsTable.GetDamage(type, WeaponOwner.Player),
            dps = WeaponStatsTable.GetDps(type),
        };

        Transform shot = ShotPrefab(item.turretPrefab, type);
        var projectile = shot != null ? shot.GetComponent<F3DProjectile>() : null;
        var beam = shot != null ? shot.GetComponent<F3DBeam>() : null;

        if (projectile != null)
        {
            specs.projectileSpeed = projectile.velocity;
            specs.range = projectile.velocity * projectile.lifeTime;
        }
        else if (beam != null)
        {
            specs.isBeam = true;
            specs.range = beam.MaxBeamLength;
        }

        // 지속형 빔(발사 간격 0)은 쏘는 동안 계속 피해를 준다 → 발사 간격 대신 '지속'
        specs.shotsPerSecond = WeaponStatsTable.IsContinuous(type) ? 0f : 1f / WeaponStatsTable.GetFireInterval(type);
        return specs;
    }

    /// <summary>터렛 프리팹이 이 무기 종류로 쏘는 발사체/빔 프리팹</summary>
    private static Transform ShotPrefab(GameObject turretPrefab, F3DFXType type)
    {
        var fx = turretPrefab != null ? turretPrefab.GetComponentInChildren<F3DFXController>(true) : null;
        if (fx == null)
            return null;

        switch (type)
        {
            case F3DFXType.Vulcan: return fx.vulcanProjectile;
            case F3DFXType.SoloGun: return fx.soloGunProjectile;
            case F3DFXType.Sniper: return fx.sniperBeam;
            case F3DFXType.ShotGun: return fx.shotGunProjectile;
            case F3DFXType.Seeker: return fx.seekerProjectile;
            case F3DFXType.RailGun: return fx.railgunBeam;
            case F3DFXType.PlasmaGun: return fx.plasmagunProjectile;
            case F3DFXType.PlasmaBeam: return fx.plasmaBeam;
            case F3DFXType.PlasmaBeamHeavy: return fx.plasmaBeamHeavy;
            case F3DFXType.LightningGun: return fx.lightningGunBeam;
            case F3DFXType.FlameRed: return fx.flameRed;
            case F3DFXType.LaserImpulse: return fx.laserImpulseProjectile;
            default: return null;
        }
    }
}
