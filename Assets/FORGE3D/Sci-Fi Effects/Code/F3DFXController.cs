using UnityEngine;
using System.Collections;
using System;

namespace FORGE3D
{
    // Weapon types
    public enum F3DFXType
    {
        Vulcan,
        SoloGun,
        Sniper,
        ShotGun,
        Seeker,
        RailGun,
        PlasmaGun,
        PlasmaBeam,
        PlasmaBeamHeavy,
        LightningGun,
        FlameRed,
        LaserImpulse
    }

    public class F3DFXController : MonoBehaviour
    {
        // Singleton instance
        public static F3DFXController instance;

        // [추가] 이 컨트롤러를 쓰는 쪽(플레이어/적). 발사체·빔 프리팹은 둘이 공유하므로,
        // 데미지는 WeaponStatsTable에서 (무기 종류, 쏜 쪽)으로 찾는다.
        [Header("Damage")] public WeaponOwner owner = WeaponOwner.Player;

        // [추가] 이 터렛이 달린 함선의 루트. 발사체/빔이 자기 함선에 맞지 않도록 무시할 때 쓴다.
        private Transform shipRoot;

        public Transform ShipRoot
        {
            get
            {
                if (shipRoot == null)
                {
                    var body = GetComponentInParent<Rigidbody>();
                    shipRoot = body != null ? body.transform : transform.root;
                }

                return shipRoot;
            }
        }

        // [추가] 플레이어가 빠르게 연타해도 무기 발사 간격(WeaponStatsTable)보다 빨리 쏘지 않도록
        // 마지막 발사 시각을 기억하고, 간격이 덜 지났으면 첫 발을 그만큼 늦춘다.
        private float lastShotTime = float.NegativeInfinity;
        private Coroutine pendingFire;

        // Current firing socket
        private int curSocket = 0;

        // Timer reference                
        private int timerID = -1;

        [Header("Turret setup")] public Transform[] TurretSocket; // Sockets reference
        public ParticleSystem[] ShellParticles; // Bullet shells particle system

        public F3DFXType DefaultFXType; // Default starting weapon type

        [Header("Vulcan")] public Transform vulcanProjectile; // Projectile prefab
        public Transform vulcanMuzzle; // Muzzle flash prefab  
        public Transform vulcanImpact; // Impact prefab
        public float vulcanOffset;

        [Header("Solo gun")] public Transform soloGunProjectile;
        public Transform soloGunMuzzle;
        public Transform soloGunImpact;
        public float soloGunOffset;

        [Header("Sniper")] public Transform sniperBeam;
        public Transform sniperMuzzle;
        public Transform sniperImpact;
        public float sniperOffset;

        [Header("Shotgun")] public Transform shotGunProjectile;
        public Transform shotGunMuzzle;
        public Transform shotGunImpact;
        public float shotGunOffset;

        [Header("Seeker")] public Transform seekerProjectile;
        public Transform seekerMuzzle;
        public Transform seekerImpact;
        public float seekerOffset;

        [Header("Rail gun")] public Transform railgunBeam;
        public Transform railgunMuzzle;
        public Transform railgunImpact;
        public float railgunOffset;

        [Header("Plasma gun")] public Transform plasmagunProjectile;
        public Transform plasmagunMuzzle;
        public Transform plasmagunImpact;
        public float plasmaGunOffset;

        [Header("Plasma beam")] public Transform plasmaBeam;
        public float plasmaOffset;

        [Header("Plasma beam heavy")] public Transform plasmaBeamHeavy;
        public float plasmaBeamHeavyOffset;

        [Header("Lightning gun")] public Transform lightningGunBeam;
        public float lightingGunBeamOffset;

        [Header("Flame")] public Transform flameRed;
        public Transform flameImpact;
        public float flameOffset;

        [Header("Laser impulse")] public Transform laserImpulseProjectile;
        public Transform laserImpulseMuzzle;
        public Transform laserImpulseImpact;
        public float laserImpulseOffset;

        private void Awake()
        {
            // Initialize singleton  
            instance = this;

            // Initialize bullet shells particles
            for (int i = 0; i < ShellParticles.Length; i++)
            {
                var em = ShellParticles[i].emission;
                em.enabled = false;
                ShellParticles[i].Stop();
                ShellParticles[i].gameObject.SetActive(true);
            }
        }

        // Switch to next weapon type
        public void NextWeapon()
        {
            if ((int) DefaultFXType < Enum.GetNames(typeof(F3DFXType)).Length - 1)
            {
                DefaultFXType++;
            }
        }

        // Switch to previous weapon type
        public void PrevWeapon()
        {
            if (DefaultFXType > 0)
            {
                DefaultFXType--;
            }
        }

        // Advance to next turret socket
        private void AdvanceSocket()
        {
            curSocket++;
            if (curSocket >= TurretSocket.Length)
                curSocket = 0;
        }

        // Fire turret weapon
        public void Fire()
        {
            switch (DefaultFXType)
            {
                case F3DFXType.Vulcan:
                    // Fire vulcan at specified rate until canceled
                    StartRepeating(F3DFXType.Vulcan, Vulcan);
                    break;

                case F3DFXType.SoloGun:
                    StartRepeating(F3DFXType.SoloGun, SoloGun);
                    break;

                case F3DFXType.Sniper:
                    StartRepeating(F3DFXType.Sniper, Sniper);
                    break;

                case F3DFXType.ShotGun:
                    StartRepeating(F3DFXType.ShotGun, ShotGun);
                    break;

                case F3DFXType.Seeker:
                    StartRepeating(F3DFXType.Seeker, Seeker);
                    break;

                case F3DFXType.RailGun:
                    StartRepeating(F3DFXType.RailGun, RailGun);
                    break;

                case F3DFXType.PlasmaGun:
                    StartRepeating(F3DFXType.PlasmaGun, PlasmaGun);
                    break;

                case F3DFXType.PlasmaBeam:
                    // Beams has no timer requirement
                    PlasmaBeam();
                    break;

                case F3DFXType.PlasmaBeamHeavy:
                    // Beams has no timer requirement
                    PlasmaBeamHeavy();
                    break;

                case F3DFXType.LightningGun:
                    // Beams has no timer requirement
                    LightningGun();
                    break;

                case F3DFXType.FlameRed:
                    // [수정] 화염 루프 대신 미사일 발사체 한 발씩 발사하도록 변경
                    StartRepeating(F3DFXType.FlameRed, FlameRed);
                    break;

                case F3DFXType.LaserImpulse:
                    StartRepeating(F3DFXType.LaserImpulse, LaserImpulse);
                    break;
            }
        }

        // [추가] 첫 발을 쏘고 발사 간격마다 반복한다. 플레이어는 직전 발사 후 간격이 덜 지났으면
        // 남은 시간만큼 기다렸다가 쏜다(연타로 간격을 건너뛰지 못하게).
        // 적은 EnemyTurretController가 자체 간격으로 Fire/Stop을 부르므로 기존처럼 바로 쏜다.
        private void StartRepeating(F3DFXType type, Action shot)
        {
            float interval = WeaponStatsTable.GetFireInterval(type);
            float wait = lastShotTime + interval - Time.time;

            if (owner == WeaponOwner.Player && wait > 0f)
            {
                pendingFire = StartCoroutine(StartRepeatingAfter(wait, interval, shot));
                return;
            }

            BeginRepeating(interval, shot);
        }

        private IEnumerator StartRepeatingAfter(float wait, float interval, Action shot)
        {
            yield return new WaitForSeconds(wait);
            pendingFire = null;
            BeginRepeating(interval, shot);
        }

        private void BeginRepeating(float interval, Action shot)
        {
            Action trackedShot = () =>
            {
                lastShotTime = Time.time;
                shot();
            };

            timerID = F3DTime.time.AddTimer(interval, trackedShot);
            // Invoke manually before the timer ticked to avoid initial delay
            trackedShot();
        }

        // Stop firing
        public void Stop()
        {
            // [추가] 간격을 기다리던 첫 발은 버튼을 떼면 취소
            if (pendingFire != null)
            {
                StopCoroutine(pendingFire);
                pendingFire = null;
            }

            // Remove firing timer
            if (timerID != -1)
            {
                F3DTime.time.RemoveTimer(timerID);
                timerID = -1;
            }

            switch (DefaultFXType)
            {
                case F3DFXType.PlasmaBeam:
                    F3DAudioController.instance.PlasmaBeamClose(transform.position);
                    break;

                case F3DFXType.PlasmaBeamHeavy:
                    F3DAudioController.instance.PlasmaBeamHeavyClose(transform.position);
                    break;

                case F3DFXType.LightningGun:
                    F3DAudioController.instance.LightningGunClose(transform.position);
                    break;
            }
        }

        // Fire vulcan weapon
        private void Vulcan()
        {
            // Get random rotation that offset spawned projectile
            var offset = Quaternion.Euler(UnityEngine.Random.onUnitSphere);
            // Spawn muzzle flash and projectile with the rotation offset at current socket position
            F3DPoolManager.Pools["GeneratedPool"].Spawn(vulcanMuzzle, TurretSocket[curSocket].position,
                TurretSocket[curSocket].rotation, TurretSocket[curSocket]);
            var newGO =
                F3DPoolManager.Pools["GeneratedPool"].Spawn(vulcanProjectile,
                    TurretSocket[curSocket].position + TurretSocket[curSocket].forward,
                    offset * TurretSocket[curSocket].rotation, null).gameObject;

            var proj = newGO.gameObject.GetComponent<F3DProjectile>();
            if (proj)
            {
                proj.controller = this; // [추가] 착탄 FX가 이 컨트롤러로 향하도록
                proj.SetOffset(vulcanOffset);
            }

            // Emit one bullet shell
            if (ShellParticles.Length > 0)
                ShellParticles[curSocket].Emit(1);

            // Play shot sound effect
            F3DAudioController.instance.VulcanShot(TurretSocket[curSocket].position);

            // Advance to next turret socket
            AdvanceSocket();
        }

        // Spawn vulcan weapon impact
        public void VulcanImpact(Vector3 pos)
        {
            // Spawn impact prefab at specified position
            F3DPoolManager.Pools["GeneratedPool"].Spawn(vulcanImpact, pos, Quaternion.identity, null);
            // Play impact sound effect
            F3DAudioController.instance.VulcanHit(pos);
        }

        // Fire sologun weapon
        private void SoloGun()
        {
            var offset = Quaternion.Euler(UnityEngine.Random.onUnitSphere);
            F3DPoolManager.Pools["GeneratedPool"].Spawn(soloGunMuzzle, TurretSocket[curSocket].position,
                TurretSocket[curSocket].rotation, TurretSocket[curSocket]);
            var newGO =
                F3DPoolManager.Pools["GeneratedPool"].Spawn(soloGunProjectile,
                    TurretSocket[curSocket].position + TurretSocket[curSocket].forward,
                    offset * TurretSocket[curSocket].rotation, null).gameObject;
            var proj = newGO.GetComponent<F3DProjectile>();
            if (proj)
            {
                proj.controller = this;
                proj.SetOffset(soloGunOffset);
            }

            F3DAudioController.instance.SoloGunShot(TurretSocket[curSocket].position);
            AdvanceSocket();
        }

        // Spawn sologun weapon impact
        public void SoloGunImpact(Vector3 pos)
        {
            F3DPoolManager.Pools["GeneratedPool"].Spawn(soloGunImpact, pos, Quaternion.identity, null);
            F3DAudioController.instance.SoloGunHit(pos);
        }

        // Fire sniper weapon
        private void Sniper()
        {
            var offset = Quaternion.Euler(UnityEngine.Random.onUnitSphere);

            F3DPoolManager.Pools["GeneratedPool"].Spawn(sniperMuzzle, TurretSocket[curSocket].position,
                TurretSocket[curSocket].rotation, TurretSocket[curSocket]);
            var newGO =
                F3DPoolManager.Pools["GeneratedPool"].Spawn(sniperBeam, TurretSocket[curSocket].position,
                    offset * TurretSocket[curSocket].rotation, null).gameObject;
            var beam = newGO.GetComponent<F3DBeam>();
            if (beam)
            {
                beam.controller = this;
                beam.SetOffset(sniperOffset);
            }

            F3DAudioController.instance.SniperShot(TurretSocket[curSocket].position);
            if (ShellParticles.Length > 0)
                ShellParticles[curSocket].Emit(1);
            AdvanceSocket();
        }

        // Spawn sniper weapon impact
        public void SniperImpact(Vector3 pos)
        {
            F3DPoolManager.Pools["GeneratedPool"].Spawn(sniperImpact, pos, Quaternion.identity, null);
            F3DAudioController.instance.SniperHit(pos);
        }

        // Fire shotgun weapon
        private void ShotGun()
        {
            var offset = Quaternion.Euler(UnityEngine.Random.onUnitSphere);
            F3DPoolManager.Pools["GeneratedPool"].Spawn(shotGunMuzzle, TurretSocket[curSocket].position,
                TurretSocket[curSocket].rotation, TurretSocket[curSocket]);
            F3DPoolManager.Pools["GeneratedPool"].Spawn(shotGunProjectile, TurretSocket[curSocket].position,
                offset * TurretSocket[curSocket].rotation, null);
            F3DAudioController.instance.ShotGunShot(TurretSocket[curSocket].position);
            if (ShellParticles.Length > 0)
                ShellParticles[curSocket].Emit(1);
            AdvanceSocket();
        }

        // Fire seeker weapon
        private void Seeker()
        {
            var offset = Quaternion.Euler(UnityEngine.Random.onUnitSphere);
            F3DPoolManager.Pools["GeneratedPool"].Spawn(seekerMuzzle, TurretSocket[curSocket].position,
                TurretSocket[curSocket].rotation, TurretSocket[curSocket]);
            var newGO =
                F3DPoolManager.Pools["GeneratedPool"].Spawn(seekerProjectile, TurretSocket[curSocket].position,
                    offset * TurretSocket[curSocket].rotation, null).gameObject;
            var proj = newGO.GetComponent<F3DProjectile>();
            if (proj)
            {
                proj.controller = this;
                proj.SetOffset(seekerOffset);
            }

            F3DAudioController.instance.SeekerShot(TurretSocket[curSocket].position);
            AdvanceSocket();
        }

        // Spawn seeker weapon impact
        public void SeekerImpact(Vector3 pos)
        {
            F3DPoolManager.Pools["GeneratedPool"].Spawn(seekerImpact, pos, Quaternion.identity, null);
            F3DAudioController.instance.SeekerHit(pos);
        }

        // Fire rail gun weapon
        private void RailGun()
        {
            var offset = Quaternion.Euler(UnityEngine.Random.onUnitSphere);
            F3DPoolManager.Pools["GeneratedPool"].Spawn(railgunMuzzle, TurretSocket[curSocket].position,
                TurretSocket[curSocket].rotation, TurretSocket[curSocket]);
            var newGO =
                F3DPoolManager.Pools["GeneratedPool"].Spawn(railgunBeam, TurretSocket[curSocket].position,
                    offset * TurretSocket[curSocket].rotation, null).gameObject;
            var beam = newGO.GetComponent<F3DBeam>();
            if (beam)
            {
                beam.controller = this;
                beam.SetOffset(railgunOffset);
            }

            F3DAudioController.instance.RailGunShot(TurretSocket[curSocket].position);
            if (ShellParticles.Length > 0)
                ShellParticles[curSocket].Emit(1);
            AdvanceSocket();
        }

        // Spawn rail gun weapon impact
        public void RailgunImpact(Vector3 pos)
        {
            F3DPoolManager.Pools["GeneratedPool"].Spawn(railgunImpact, pos, Quaternion.identity, null);
            F3DAudioController.instance.RailGunHit(pos);
        }

        // Fire plasma gun weapon
        private void PlasmaGun()
        {
            var offset = Quaternion.Euler(UnityEngine.Random.onUnitSphere);
            F3DPoolManager.Pools["GeneratedPool"].Spawn(plasmagunMuzzle, TurretSocket[curSocket].position,
                TurretSocket[curSocket].rotation, TurretSocket[curSocket]);
            var newGo =
                F3DPoolManager.Pools["GeneratedPool"].Spawn(plasmagunProjectile, TurretSocket[curSocket].position,
                    offset * TurretSocket[curSocket].rotation, null).gameObject;
            var proj = newGo.GetComponent<F3DProjectile>();
            if (proj)
            {
                proj.controller = this;
                proj.SetOffset(plasmaGunOffset); // [수정] PlasmaBeam용 plasmaOffset을 잘못 쓰던 원본 버그
            }

            F3DAudioController.instance.PlasmaGunShot(TurretSocket[curSocket].position);
            AdvanceSocket();
        }

        // Spawn plasma gun weapon impact
        public void PlasmaGunImpact(Vector3 pos)
        {
            F3DPoolManager.Pools["GeneratedPool"].Spawn(plasmagunImpact, pos, Quaternion.identity, null);
            F3DAudioController.instance.PlasmaGunHit(pos);
        }

        // Fire plasma beam weapon
        private void PlasmaBeam()
        {
            for (var i = 0; i < TurretSocket.Length; i++)
            {
                F3DPoolManager.Pools["GeneratedPool"].Spawn(plasmaBeam, TurretSocket[i].position,
                    TurretSocket[i].rotation,
                    TurretSocket[i]);
            }

            F3DAudioController.instance.PlasmaBeamLoop(transform.position, transform.parent);
        }

        // Fire heavy beam weapon
        private void PlasmaBeamHeavy()
        {
            for (var i = 0; i < TurretSocket.Length; i++)
            {
                F3DPoolManager.Pools["GeneratedPool"].Spawn(plasmaBeamHeavy, TurretSocket[i].position,
                    TurretSocket[i].rotation,
                    TurretSocket[i]);
            }

            F3DAudioController.instance.PlasmaBeamHeavyLoop(transform.position, transform.parent);
        }

        // Fire lightning gun weapon
        private void LightningGun()
        {
            for (var i = 0; i < TurretSocket.Length; i++)
            {
                F3DPoolManager.Pools["GeneratedPool"].Spawn(lightningGunBeam, TurretSocket[i].position,
                    TurretSocket[i].rotation,
                    TurretSocket[i]);
            }

            F3DAudioController.instance.LightningGunLoop(transform.position, transform);
        }

        // [수정] 미사일 발사체를 한 발 발사(직선 비행 + 충돌 시 폭발)하도록 변경. 기존에는
        // 소켓마다 화염 이펙트만 붙여두는 연속 루프 무기였는데, 실제로는 미사일처럼
        // 포탄이 날아가서 맞는 무기여야 하므로 다른 발사체 무기(Vulcan/PlasmaGun 등)와
        // 같은 방식으로 F3DProjectile을 스폰한다.
        private void FlameRed()
        {
            var newGo =
                F3DPoolManager.Pools["GeneratedPool"].Spawn(flameRed, TurretSocket[curSocket].position,
                    TurretSocket[curSocket].rotation, null).gameObject;
            var proj = newGo.GetComponent<F3DProjectile>();
            if (proj)
            {
                proj.controller = this;
                proj.SetOffset(flameOffset);
            }

            AdvanceSocket();
        }

        // Spawn missile weapon impact (explosion)
        public void FlameRedImpact(Vector3 pos)
        {
            F3DPoolManager.Pools["GeneratedPool"].Spawn(flameImpact, pos, Quaternion.identity, null);
        }

        // Fire laser pulse weapon
        private void LaserImpulse()
        {
            var offset = Quaternion.Euler(UnityEngine.Random.onUnitSphere);
            F3DPoolManager.Pools["GeneratedPool"].Spawn(laserImpulseMuzzle, TurretSocket[curSocket].position,
                TurretSocket[curSocket].rotation, TurretSocket[curSocket]);
            var newGO =
                F3DPoolManager.Pools["GeneratedPool"].Spawn(laserImpulseProjectile, TurretSocket[curSocket].position,
                    offset * TurretSocket[curSocket].rotation, null).gameObject;
            var proj = newGO.GetComponent<F3DProjectile>();
            if (proj)
            {
                proj.controller = this;
                proj.SetOffset(laserImpulseOffset);
            }

            F3DAudioController.instance.LaserImpulseShot(TurretSocket[curSocket].position);

            AdvanceSocket();
        }

        // Spawn laser pulse weapon impact
        public void LaserImpulseImpact(Vector3 pos)
        {
            F3DPoolManager.Pools["GeneratedPool"].Spawn(laserImpulseImpact, pos, Quaternion.identity, null);
            F3DAudioController.instance.LaserImpulseHit(pos);
        }
    }
}