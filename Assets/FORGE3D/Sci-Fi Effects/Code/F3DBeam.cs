using UnityEngine;
using System.Collections;

namespace FORGE3D
{
    [RequireComponent(typeof(LineRenderer))]
    public class F3DBeam : MonoBehaviour
    {
        public LayerMask layerMask;

        public F3DFXType fxType; // Weapon type
        public bool OneShot; // Constant or single beam?

        public Texture[] BeamFrames; // Animation frame sequence
        public float FrameStep; // Animation time

        public float beamScale; // Default beam scale to be kept over distance
        public float MaxBeamLength; // Maximum beam length

        public bool AnimateUV; // UV Animation
        public float UVTime; // UV Animation speed

        public Transform rayImpact; // Impact transform
        public Transform rayMuzzle; // Muzzle flash transform

        // [추가] 이 빔을 쏜 F3DFXController. F3DProjectile과 동일한 이유로 싱글톤 대신 사용.
        [System.NonSerialized] public F3DFXController controller;

        [Header("Damage")] // [추가]
        [Tooltip("OneShot = 한 발 데미지 / 지속형 = 초당 데미지(DPS)")]
        public float damage = 30f;

        LineRenderer lineRenderer; // Line rendered component
        RaycastHit hitPoint; // Raycast structure
        RaycastHit2D hitPoint2D; // Raycasthit in 2d

        int frameNo; // Frame counter
        int FrameTimerID = -1; // [변경] 0 → -1 : 다른 타이머(ID 0)를 잘못 지우는 문제 방지
        float beamLength; // Current beam length
        float initialBeamOffset; // Initial UV offset
        public float fxOffset; // Fx offset from bullet's touch point

        void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();

            if (!AnimateUV && BeamFrames.Length > 0)
                lineRenderer.material.mainTexture = BeamFrames[0];

            initialBeamOffset = Random.Range(0f, 5f);
        }

        void OnSpawned()
        {
            if (OneShot)
                Raycast();

            if (BeamFrames.Length > 1)
                Animate();
        }

        void OnDespawned()
        {
            frameNo = 0;

            if (FrameTimerID != -1)
            {
                F3DTime.time.RemoveTimer(FrameTimerID);
                FrameTimerID = -1;
            }
        }

        void Raycast()
        {
            hitPoint = new RaycastHit();
            Ray ray = new Ray(transform.position, transform.forward);
            float propMult = MaxBeamLength * (beamScale / 10f);

            // [변경] 발사 주체(controller)가 있으면 그쪽으로, 없으면 기존처럼 싱글톤으로 폴백
            F3DFXController fx = controller != null ? controller : F3DFXController.instance;

            if (Physics.Raycast(ray, out hitPoint, MaxBeamLength, layerMask))
            {
                beamLength = Vector3.Distance(transform.position, hitPoint.point);
                lineRenderer.SetPosition(1, new Vector3(0f, 0f, beamLength));

                propMult = beamLength * (beamScale / 10f);

                // [추가] 데미지 전달. 지속형 빔은 프레임레이트와 무관하게 DPS가 일정하도록 deltaTime을 곱한다.
                float frameDamage = OneShot ? damage : damage * Time.deltaTime;
                HitResolver.Resolve(hitPoint, frameDamage);

                switch (fxType)
                {
                    case F3DFXType.Sniper:
                        fx.SniperImpact(hitPoint.point + hitPoint.normal * fxOffset);
                        ApplyForce(4f);
                        break;

                    case F3DFXType.RailGun:
                        fx.RailgunImpact(hitPoint.point + hitPoint.normal * fxOffset);
                        ApplyForce(7f);
                        break;

                    case F3DFXType.PlasmaBeam:
                        ApplyForce(0.5f);
                        break;

                    case F3DFXType.PlasmaBeamHeavy:
                        ApplyForce(2f);
                        break;
                }

                if (rayImpact)
                    rayImpact.position = hitPoint.point - transform.forward * 0.5f;
            }
            else
            {
                // (원본 에셋의 2D 분기 — 3D 게임에서는 사용되지 않음. 데미지 처리 없음)
                RaycastHit2D ray2D = Physics2D.Raycast(new Vector2(transform.position.x, transform.position.y),
                    new Vector2(transform.forward.x, transform.forward.y), beamLength, layerMask);
                if (ray2D)
                {
                    beamLength = Vector3.Distance(transform.position, ray2D.point);
                    lineRenderer.SetPosition(1, new Vector3(0f, 0f, beamLength));

                    propMult = beamLength * (beamScale / 10f);
                    switch (fxType)
                    {
                        case F3DFXType.Sniper:
                            fx.SniperImpact(ray2D.point + ray2D.normal * fxOffset);
                            ApplyForce(4f);
                            break;

                        case F3DFXType.RailGun:
                            fx.RailgunImpact(ray2D.point + ray2D.normal * fxOffset);
                            ApplyForce(7f);
                            break;

                        case F3DFXType.PlasmaBeam:
                            ApplyForce(0.5f);
                            break;

                        case F3DFXType.PlasmaBeamHeavy:
                            ApplyForce(2f);
                            break;
                    }

                    if (rayImpact)
                        rayImpact.position = new Vector3(ray2D.point.x,
                            ray2D.point.y,
                            this.gameObject.transform.position.z) - transform.forward * 0.5f;
                }
                else
                {
                    beamLength = MaxBeamLength;
                    lineRenderer.SetPosition(1, new Vector3(0f, 0f, beamLength));

                    if (rayImpact)
                        rayImpact.position = transform.position + transform.forward * beamLength;
                }
            }

            if (rayMuzzle)
                rayMuzzle.position = transform.position + transform.forward * 0.1f;

            lineRenderer.material.SetTextureScale("_MainTex", new Vector2(propMult, 1f));
        }

        void OnFrameStep()
        {
            lineRenderer.material.mainTexture = BeamFrames[frameNo];
            frameNo++;

            if (frameNo == BeamFrames.Length)
                frameNo = 0;
        }

        void Animate()
        {
            if (BeamFrames.Length > 1)
            {
                frameNo = 0;
                lineRenderer.material.mainTexture = BeamFrames[frameNo];

                FrameTimerID = F3DTime.time.AddTimer(FrameStep, BeamFrames.Length - 1, OnFrameStep);

                frameNo = 1;
            }
        }

        void ApplyForce(float force)
        {
            if (hitPoint.rigidbody != null)
                hitPoint.rigidbody.AddForceAtPosition(transform.forward * force, hitPoint.point, ForceMode.VelocityChange);
        }

        public void SetOffset(float offset)
        {
            fxOffset = offset;
        }

        // [추가]
        public void SetDamage(float value)
        {
            damage = Mathf.Max(0f, value);
        }

        private float animateUVTime;

        void Update()
        {
            if (AnimateUV)
            {
                animateUVTime += Time.deltaTime;

                if (animateUVTime > 1.0f)
                    animateUVTime = 0f;

                lineRenderer.material.SetTextureOffset("_MainTex", new Vector2(animateUVTime * UVTime + initialBeamOffset, 0f));
            }

            if (!OneShot)
                Raycast();
        }
    }
}
