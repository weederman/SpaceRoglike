using UnityEngine;
using System.Collections;
using ProceduralForceField;

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

        private LineRenderer lineRenderer;
        private RaycastHit hitPoint;
        private RaycastHit2D hitPoint2D;

        private int frameNo;
        private int FrameTimerID = -1;

        private float beamLength;
        private float initialBeamOffset;

        public float fxOffset; // Fx offset from bullet's touch point

        void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();

            if (!AnimateUV &&
                BeamFrames != null &&
                BeamFrames.Length > 0)
            {
                lineRenderer.material.mainTexture = BeamFrames[0];
            }

            initialBeamOffset = Random.Range(0f, 5f);
        }

        // OnSpawned called by pool manager
        void OnSpawned()
        {
            // Do one time raycast in case of one shot flag
            if (OneShot)
                Raycast();

            // Start animation sequence if beam frames array has more than 1 element
            if (BeamFrames != null &&
                BeamFrames.Length > 1)
            {
                Animate();
            }
        }

        // OnDespawned called by pool manager
        void OnDespawned()
        {
            frameNo = 0;

            if (FrameTimerID != -1)
            {
                F3DTime.time.RemoveTimer(FrameTimerID);
                FrameTimerID = -1;
            }
        }

        // ============================================================
        // SHIELD INTERACTION
        // ============================================================

        private void TriggerShield(Vector3 hitPosition, Transform hitTransform)
        {
            if (hitTransform == null)
                return;

            // Collider may be on the shield mesh child,
            // while ProceduralForceFieldOverlay is on the parent.
            ProceduralForceFieldOverlay shield =
                hitTransform.GetComponentInParent<ProceduralForceFieldOverlay>();

            if (shield != null)
            {
                shield.Trigger(hitPosition);
            }
        }

        // ============================================================
        // RAYCAST
        // ============================================================

        void Raycast()
        {
            hitPoint = new RaycastHit();

            Ray ray = new Ray(
                transform.position,
                transform.forward
            );

            // Calculate default beam proportion multiplier
            float propMult =
                MaxBeamLength * (beamScale / 10f);

            // ========================================================
            // 3D RAYCAST
            // ========================================================

            if (Physics.Raycast(
                ray,
                out hitPoint,
                MaxBeamLength,
                layerMask))
            {
                // Get current beam length
                beamLength =
                    Vector3.Distance(
                        transform.position,
                        hitPoint.point
                    );

                lineRenderer.SetPosition(
                    1,
                    new Vector3(
                        0f,
                        0f,
                        beamLength
                    )
                );

                // Calculate beam proportion multiplier
                propMult =
                    beamLength * (beamScale / 10f);

                // ====================================================
                // SHIELD INTERACTION
                // ====================================================

                TriggerShield(
                    hitPoint.point,
                    hitPoint.transform
                );

                // ====================================================
                // IMPACT EFFECTS
                // ====================================================
                // IMPORTANT:
                // No AddForce / AddForceAtPosition is used here.
                // The beam will NOT physically push the ship.

                switch (fxType)
                {
                    case F3DFXType.Sniper:

                        F3DFXController.instance.SniperImpact(
                            hitPoint.point +
                            hitPoint.normal * fxOffset
                        );

                        break;

                    case F3DFXType.RailGun:

                        F3DFXController.instance.RailgunImpact(
                            hitPoint.point +
                            hitPoint.normal * fxOffset
                        );

                        break;

                    case F3DFXType.PlasmaBeam:

                        // Physical force removed.
                        break;

                    case F3DFXType.PlasmaBeamHeavy:

                        // Physical force removed.
                        break;
                }

                // Adjust impact effect position
                if (rayImpact)
                {
                    rayImpact.position =
                        hitPoint.point -
                        transform.forward * 0.5f;
                }
            }

            // ========================================================
            // 2D RAYCAST
            // ========================================================

            else
            {
                hitPoint2D =
                    Physics2D.Raycast(
                        new Vector2(
                            transform.position.x,
                            transform.position.y
                        ),
                        new Vector2(
                            transform.forward.x,
                            transform.forward.y
                        ),
                        MaxBeamLength,
                        layerMask
                    );

                if (hitPoint2D)
                {
                    // Get current beam length
                    beamLength =
                        Vector3.Distance(
                            transform.position,
                            hitPoint2D.point
                        );

                    lineRenderer.SetPosition(
                        1,
                        new Vector3(
                            0f,
                            0f,
                            beamLength
                        )
                    );

                    // Calculate beam proportion multiplier
                    propMult =
                        beamLength * (beamScale / 10f);

                    // =================================================
                    // SHIELD INTERACTION
                    // =================================================

                    TriggerShield(
                        new Vector3(
                            hitPoint2D.point.x,
                            hitPoint2D.point.y,
                            transform.position.z
                        ),
                        hitPoint2D.transform
                    );

                    // =================================================
                    // IMPACT EFFECTS
                    // =================================================
                    // No physical force.

                    switch (fxType)
                    {
                        case F3DFXType.Sniper:

                            F3DFXController.instance.SniperImpact(
                                hitPoint2D.point +
                                hitPoint2D.normal * fxOffset
                            );

                            break;

                        case F3DFXType.RailGun:

                            F3DFXController.instance.RailgunImpact(
                                hitPoint2D.point +
                                hitPoint2D.normal * fxOffset
                            );

                            break;

                        case F3DFXType.PlasmaBeam:

                            // Physical force removed.
                            break;

                        case F3DFXType.PlasmaBeamHeavy:

                            // Physical force removed.
                            break;
                    }

                    // Adjust impact effect position
                    if (rayImpact)
                    {
                        rayImpact.position =
                            new Vector3(
                                hitPoint2D.point.x,
                                hitPoint2D.point.y,
                                transform.position.z
                            )
                            - transform.forward * 0.5f;
                    }
                }

                // ====================================================
                // NOTHING WAS HIT
                // ====================================================

                else
                {
                    beamLength = MaxBeamLength;

                    lineRenderer.SetPosition(
                        1,
                        new Vector3(
                            0f,
                            0f,
                            beamLength
                        )
                    );

                    // Adjust impact effect position
                    if (rayImpact)
                    {
                        rayImpact.position =
                            transform.position +
                            transform.forward * beamLength;
                    }
                }
            }

            // ========================================================
            // MUZZLE POSITION
            // ========================================================

            if (rayMuzzle)
            {
                rayMuzzle.position =
                    transform.position +
                    transform.forward * 0.1f;
            }

            // ========================================================
            // BEAM TEXTURE SCALING
            // ========================================================

            lineRenderer.material.SetTextureScale(
                "_MainTex",
                new Vector2(
                    propMult,
                    1f
                )
            );
        }

        // ============================================================
        // FRAME ANIMATION
        // ============================================================

        void OnFrameStep()
        {
            if (BeamFrames == null ||
                BeamFrames.Length == 0)
                return;

            lineRenderer.material.mainTexture =
                BeamFrames[frameNo];

            frameNo++;

            if (frameNo >= BeamFrames.Length)
                frameNo = 0;
        }

        void Animate()
        {
            if (BeamFrames == null ||
                BeamFrames.Length <= 1)
                return;

            frameNo = 0;

            lineRenderer.material.mainTexture =
                BeamFrames[frameNo];

            FrameTimerID =
                F3DTime.time.AddTimer(
                    FrameStep,
                    BeamFrames.Length - 1,
                    OnFrameStep
                );

            frameNo = 1;
        }

        // ============================================================
        // IMPACT OFFSET
        // ============================================================

        public void SetOffset(float offset)
        {
            fxOffset = offset;
        }

        // ============================================================
        // UV ANIMATION
        // ============================================================

        private float animateUVTime;

        void Update()
        {
            if (AnimateUV)
            {
                animateUVTime += Time.deltaTime;

                if (animateUVTime > 1.0f)
                    animateUVTime = 0f;

                lineRenderer.material.SetTextureOffset(
                    "_MainTex",
                    new Vector2(
                        animateUVTime * UVTime +
                        initialBeamOffset,
                        0f
                    )
                );
            }

            // Continuous beam
            if (!OneShot)
                Raycast();
        }
    }
}