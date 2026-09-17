using UnityEngine;
using System.Collections;

namespace FORGE3D
{
    public class F3DProjectile : MonoBehaviour
    {
        public F3DFXType fxType; // Weapon type
        public LayerMask layerMask;
        public float lifeTime = 5f; // Projectile life time
        public float despawnDelay; // Delay despawn (seconds)
        public float velocity = 300f; // Projectile velocity
        public float RaycastAdvance = 2f; // Raycast advance multiplier
        public bool DelayDespawn = false; // Projectile despawn flag
        public ParticleSystem[] delayedParticles; // Array of delayed particles

        [Header("Damage")] // [추가]
        public float damage = 10f;

        ParticleSystem[] particles; // Array of projectile particles
        new Transform transform; // Cached transform
        RaycastHit hitPoint; // Raycast structure
        bool isHit = false; // Projectile hit flag
        bool isFXSpawned = false; // Hit FX prefab spawned flag
        float timer = 0f; // Projectile timer
        float fxOffset; // Offset of fxImpact

        void Awake()
        {
            transform = GetComponent<Transform>();
            particles = GetComponentsInChildren<ParticleSystem>();
        }

        // OnSpawned called by pool manager
        public void OnSpawned()
        {
            isHit = false;
            isFXSpawned = false;
            timer = 0f;
            hitPoint = new RaycastHit();
        }

        // OnDespawned called by pool manager
        public void OnDespawned()
        {
        }

        // Stop attached particle systems emission and allow them to fade out before despawning
        void Delay()
        {
            if (particles.Length > 0 && delayedParticles.Length > 0)
            {
                bool delayed;
                for (int i = 0; i < particles.Length; i++)
                {
                    delayed = false;
                    for (int y = 0; y < delayedParticles.Length; y++)
                        if (particles[i] == delayedParticles[y])
                        {
                            delayed = true;
                            break;
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

        void ApplyForce(float force)
        {
            if (hitPoint.rigidbody != null)
                hitPoint.rigidbody.AddForceAtPosition(transform.forward * force, hitPoint.point, ForceMode.VelocityChange);
        }

        void Update()
        {
            if (isHit)
            {
                if (!isFXSpawned)
                {
                    switch (fxType)
                    {
                        case F3DFXType.Vulcan:
                            F3DFXController.instance.VulcanImpact(hitPoint.point + hitPoint.normal * fxOffset);
                            ApplyForce(2.5f);
                            break;

                        case F3DFXType.SoloGun:
                            F3DFXController.instance.SoloGunImpact(hitPoint.point + hitPoint.normal * fxOffset);
                            ApplyForce(25f);
                            break;

                        case F3DFXType.Seeker:
                            F3DFXController.instance.SeekerImpact(hitPoint.point + hitPoint.normal * fxOffset);
                            ApplyForce(30f);
                            break;

                        case F3DFXType.PlasmaGun:
                            F3DFXController.instance.PlasmaGunImpact(hitPoint.point + hitPoint.normal * fxOffset);
                            ApplyForce(25f);
                            break;

                        case F3DFXType.LaserImpulse:
                            F3DFXController.instance.LaserImpulseImpact(hitPoint.point + hitPoint.normal * fxOffset);
                            ApplyForce(25f);
                            break;
                    }

                    isFXSpawned = true;
                }

                if (!DelayDespawn || (DelayDespawn && (timer >= despawnDelay)))
                    OnProjectileDestroy();
            }
            else
            {
                Vector3 step = transform.forward * Time.deltaTime * velocity;

                // [변경] 중복 Raycast 제거 + Overlay 직접 호출 대신 HitResolver로 데미지 전달
                if (Physics.Raycast(transform.position, transform.forward, out hitPoint,
                        step.magnitude * RaycastAdvance, layerMask))
                {
                    isHit = true;

                    // 감지한 프레임에 즉시 처리 (다음 프레임엔 대상이 사라졌을 수 있음)
                    // 쉴드 연출은 ShieldHealth → Overlay.Trigger 경로로 실행된다.
                    HitResolver.Resolve(hitPoint, damage);

                    if (DelayDespawn)
                    {
                        timer = 0f;
                        Delay();
                    }
                }
                else
                {
                    if (timer >= lifeTime)
                        OnProjectileDestroy();
                }

                transform.position += step;
            }

            timer += Time.deltaTime;
        }

        public void SetOffset(float offset)
        {
            fxOffset = offset;
        }

        // [추가] 로그라이크 업그레이드 등에서 발사 시 데미지 설정용
        public void SetDamage(float value)
        {
            damage = Mathf.Max(0f, value);
        }
    }
}
