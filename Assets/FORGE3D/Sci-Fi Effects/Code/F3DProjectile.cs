using UnityEngine;
using System.Collections;

namespace FORGE3D
{
    public class F3DProjectile : MonoBehaviour
    {
        public F3DFXType fxType;
        public LayerMask layerMask;
        public float lifeTime = 5f;
        public float despawnDelay;
        public float velocity = 300f;
        public float RaycastAdvance = 2f;
        public bool DelayDespawn = false;
        public ParticleSystem[] delayedParticles;

        // 이 발사체를 쏜 F3DFXController. 착탄 연출을 정적 싱글톤(instance) 대신
        // 발사한 컨트롤러로 향하게 해서, 함선이 여러 대(=컨트롤러가 여러 개)일 때
        // 착탄 이펙트가 마지막에 Awake된 컨트롤러로 섞이는 문제를 막는다.
        [System.NonSerialized] public F3DFXController controller;

        // [추가] 유도탄: 이 표적(락온한 함선)을 향해 초당 homingTurnRate도씩 방향을 튼다. 표적이 없거나 부서지면 직진.
        // 풀에서 다시 쓰므로 쏘는 쪽(F3DFXController)이 매번 정한다.
        [System.NonSerialized] public Component homingTarget;
        [System.NonSerialized] public float homingTurnRate;

        ParticleSystem[] particles;
        new Transform transform;
        RaycastHit hitPoint;

        bool isHit = false;
        bool isFXSpawned = false;

        float timer = 0f;
        float fxOffset;

        void Awake()
        {
            transform = GetComponent<Transform>();
            particles = GetComponentsInChildren<ParticleSystem>();
        }

        public void OnSpawned()
        {
            isHit = false;
            isFXSpawned = false;
            timer = 0f;
            hitPoint = new RaycastHit();
        }

        public void OnDespawned()
        {
        }

        void Delay()
        {
            if (particles.Length > 0 && delayedParticles.Length > 0)
            {
                bool delayed;

                for (int i = 0; i < particles.Length; i++)
                {
                    delayed = false;

                    for (int y = 0; y < delayedParticles.Length; y++)
                    {
                        if (particles[i] == delayedParticles[y])
                        {
                            delayed = true;
                            break;
                        }
                    }

                    particles[i].Stop(false);

                    if (!delayed)
                        particles[i].Clear(false);
                }
            }
        }

        void OnProjectileDestroy()
        {
            F3DPoolManager.Pools["GeneratedPool"].Despawn(transform);
        }

        void Update()
        {
            // =========================================
            // 충돌한 경우
            // =========================================

            if (isHit)
            {
                if (!isFXSpawned)
                {
                    // [추가] 착탄 연출을 먼저 처리한 것으로 표시한다. 연출(이펙트·소리)에서 예외가 나도
                    // 다음 프레임에는 아래에서 탄을 반드시 풀로 돌려보낸다 (예외가 나면 탄이 그 자리에 영원히 남던 문제).
                    isFXSpawned = true;

                    // 발사 주체(controller)가 있으면 그쪽으로, 없으면 기존처럼 싱글톤으로 폴백
                    F3DFXController fx = controller != null ? controller : F3DFXController.instance;

                    switch (fxType)
                    {
                        case F3DFXType.Vulcan:
                            fx.VulcanImpact(
                                hitPoint.point + hitPoint.normal * fxOffset
                            );
                            break;

                        case F3DFXType.SoloGun:
                            fx.SoloGunImpact(
                                hitPoint.point + hitPoint.normal * fxOffset
                            );
                            break;

                        case F3DFXType.Seeker:
                            fx.SeekerImpact(
                                hitPoint.point + hitPoint.normal * fxOffset
                            );
                            break;

                        case F3DFXType.PlasmaGun:
                            fx.PlasmaGunImpact(
                                hitPoint.point + hitPoint.normal * fxOffset
                            );
                            break;

                        case F3DFXType.LaserImpulse:
                            fx.LaserImpulseImpact(
                                hitPoint.point + hitPoint.normal * fxOffset
                            );
                            break;

                        case F3DFXType.FlameRed:
                            fx.FlameRedImpact(
                                hitPoint.point + hitPoint.normal * fxOffset
                            );
                            break;
                    }
                }

                // =========================================
                // 탄환 제거
                // =========================================

                if (!DelayDespawn ||
                    (DelayDespawn && timer >= despawnDelay))
                {
                    OnProjectileDestroy();
                }
            }

            // =========================================
            // 아직 충돌하지 않은 경우
            // =========================================

            else
            {
                Steer();

                Vector3 step =
                    transform.forward *
                    Time.deltaTime *
                    velocity;

                // [수정] 쏜 함선 자신(쉴드·선체·반대편 터렛)과 트리거는 무시한다
                if (HitResolver.Raycast(
                    transform.position,
                    transform.forward,
                    step.magnitude * RaycastAdvance,
                    layerMask,
                    controller != null ? controller.ShipRoot : null,
                    out hitPoint))
                {
                    isHit = true;

                    // =========================================
                    // 데미지 전달 (쉴드/선체 HP 처리 + 피격 연출까지 포함)
                    // =========================================
                    // 감지한 프레임에 즉시 처리한다. 대상을 찾아 데미지를 주고,
                    // 쉴드가 살아있다면 ShieldHealth가 알아서 Overlay.Trigger를 호출한다.

                    // 데미지: 무기 데이터 테이블(WeaponStatsTable)에서 (무기 종류, 쏜 쪽)으로 찾는다.
                    WeaponOwner owner = controller != null ? controller.owner : WeaponOwner.Player;
                    // [추가] 터렛마다 정한 공격력이 있으면 그 값 (EnemyShipCatalog)
                    float appliedDamage = controller != null ? controller.GetDamage(fxType) : WeaponStatsTable.GetDamage(fxType, owner);

                    HitResolver.Resolve(
                        hitPoint,
                        appliedDamage,
                        controller != null ? controller.GetShieldMultiplier(fxType) : WeaponStatsTable.GetShieldMultiplier(fxType),
                        controller != null ? controller.GetArmorMultiplier(fxType) : WeaponStatsTable.GetArmorMultiplier(fxType),
                        fxType,
                        owner);

                    // =========================================
                    // 탄환 파티클 처리
                    // =========================================

                    if (DelayDespawn)
                    {
                        timer = 0f;
                        Delay();
                    }
                }

                // =========================================
                // 아무것도 맞지 않은 경우
                // =========================================

                else
                {
                    if (timer >= lifeTime)
                    {
                        OnProjectileDestroy();
                    }
                }

                // 탄환 이동
                transform.position += step;
            }

            timer += Time.deltaTime;
        }

        // [추가] 유도탄이면 표적 쪽으로 방향을 돌린다
        void Steer()
        {
            if (homingTarget == null)
                return;

            if (homingTarget is HullHealth hull && !hull.IsActive)
            {
                homingTarget = null;
                return;
            }

            Vector3 to = TargetingSystem.PositionOf(homingTarget) - transform.position;
            if (to.sqrMagnitude < 0.01f)
                return;

            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to),
                homingTurnRate * Time.deltaTime);
        }

        public void SetOffset(float offset)
        {
            fxOffset = offset;
        }

    }
}