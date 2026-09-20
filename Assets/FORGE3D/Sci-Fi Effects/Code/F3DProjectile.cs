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

        [Header("Damage")]
        public float damage = 10f;

        // 이 발사체를 쏜 F3DFXController. 착탄 연출을 정적 싱글톤(instance) 대신
        // 발사한 컨트롤러로 향하게 해서, 함선이 여러 대(=컨트롤러가 여러 개)일 때
        // 착탄 이펙트가 마지막에 Awake된 컨트롤러로 섞이는 문제를 막는다.
        [System.NonSerialized] public F3DFXController controller;

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
                    }

                    isFXSpawned = true;
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
                Vector3 step =
                    transform.forward *
                    Time.deltaTime *
                    velocity;

                if (Physics.Raycast(
                    transform.position,
                    transform.forward,
                    out hitPoint,
                    step.magnitude * RaycastAdvance,
                    layerMask))
                {
                    isHit = true;

                    // =========================================
                    // 데미지 전달 (쉴드/선체 HP 처리 + 피격 연출까지 포함)
                    // =========================================
                    // 감지한 프레임에 즉시 처리한다. 대상을 찾아 데미지를 주고,
                    // 쉴드가 살아있다면 ShieldHealth가 알아서 Overlay.Trigger를 호출한다.

                    HitResolver.Resolve(hitPoint, damage);

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

        public void SetOffset(float offset)
        {
            fxOffset = offset;
        }

        // 로그라이크 업그레이드 등에서 발사 시 데미지 설정용
        public void SetDamage(float value)
        {
            damage = Mathf.Max(0f, value);
        }
    }
}