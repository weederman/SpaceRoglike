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

        LineRenderer lineRenderer; // Line rendered component
        RaycastHit hitPoint; // Raycast structure
        RaycastHit2D hitPoint2D; // Raycast hit in 2D

        int frameNo; // Frame counter
        int FrameTimerID = -1; // Frame timer reference

        float beamLength; // Current beam length
        float initialBeamOffset; // Initial UV offset

        public float fxOffset; // Fx offset from bullet's touch point

        void Awake()
        {
            // Get line renderer component
            lineRenderer = GetComponent<LineRenderer>();

            // Assign first frame texture
            if (!AnimateUV && BeamFrames.Length > 0)
                lineRenderer.material.mainTexture = BeamFrames[0];

            // Randomize UV offset
            initialBeamOffset = Random.Range(0f, 5f);
        }

        // OnSpawned called by pool manager
        void OnSpawned()
        {
            // Do one time raycast in case of one shot flag
            if (OneShot)
                Raycast();

            // Start animation sequence if beam frames array has more than 2 elements
            if (BeamFrames.Length > 1)
                Animate();
        }

        // OnDespawned called by pool manager
        void OnDespawned()
        {
            // Reset frame counter
            frameNo = 0;

            // Clear timer
            if (FrameTimerID != -1)
            {
                F3DTime.time.RemoveTimer(FrameTimerID);
                FrameTimerID = -1;
            }
        }

        // Hit point calculation
        void Raycast()
        {
            // Prepare structure and create ray
            hitPoint = new RaycastHit();

            Ray ray = new Ray(
                transform.position,
                transform.forward
            );

            // Calculate default beam proportion multiplier
            // based on default scale and maximum length
            float propMult =
                MaxBeamLength * (beamScale / 10f);

            // --------------------------------------------------
            // 3D Raycast
            // --------------------------------------------------

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

                // --------------------------------------------------
                // Impact effects
                // 물리력은 적용하지 않음
                // --------------------------------------------------

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

                        // 기존 물리력 제거
                        break;

                    case F3DFXType.PlasmaBeamHeavy:

                        // 기존 물리력 제거
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

            // --------------------------------------------------
            // 2D Raycast
            // --------------------------------------------------

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

                    // --------------------------------------------------
                    // Impact effects
                    // 물리력은 적용하지 않음
                    // --------------------------------------------------

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

                            // 기존 물리력 제거
                            break;

                        case F3DFXType.PlasmaBeamHeavy:

                            // 기존 물리력 제거
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

                // --------------------------------------------------
                // Nothing was hit
                // --------------------------------------------------

                else
                {
                    // Set beam to maximum length
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

            // --------------------------------------------------
            // Muzzle position
            // --------------------------------------------------

            if (rayMuzzle)
            {
                rayMuzzle.position =
                    transform.position +
                    transform.forward * 0.1f;
            }

            // --------------------------------------------------
            // Beam texture scaling
            // --------------------------------------------------

            lineRenderer.material.SetTextureScale(
                "_MainTex",
                new Vector2(
                    propMult,
                    1f
                )
            );
        }

        // Advance texture frame
        void OnFrameStep()
        {
            if (BeamFrames == null ||
                BeamFrames.Length == 0)
                return;

            // Set current texture frame
            lineRenderer.material.mainTexture =
                BeamFrames[frameNo];

            frameNo++;

            // Reset frame counter
            if (frameNo >= BeamFrames.Length)
                frameNo = 0;
        }

        // Initialize frame animation
        void Animate()
        {
            if (BeamFrames == null ||
                BeamFrames.Length <= 1)
                return;

            // Set current frame
            frameNo = 0;

            lineRenderer.material.mainTexture =
                BeamFrames[frameNo];

            // Add timer
            FrameTimerID =
                F3DTime.time.AddTimer(
                    FrameStep,
                    BeamFrames.Length - 1,
                    OnFrameStep
                );

            frameNo = 1;
        }

        // Set offset of impact
        public void SetOffset(float offset)
        {
            fxOffset = offset;
        }

        private float animateUVTime;

        void Update()
        {
            // --------------------------------------------------
            // Animate texture UV
            // --------------------------------------------------

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

            // --------------------------------------------------
            // Raycast for continuous laser beams
            // --------------------------------------------------

            if (!OneShot)
                Raycast();
        }
    }
}