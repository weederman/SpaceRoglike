using UnityEngine;

namespace ProceduralForceField
{
    [DisallowMultipleComponent]
    public sealed class ProceduralForceFieldOverlay : MonoBehaviour
    {
        [SerializeField] private ProceduralForceFieldHit _forceFieldHit;
        [SerializeField] private Renderer _overlayRenderer;

        [Header("Startup")]
        [SerializeField] private bool _startVisible = true;

        [Header("Reveal Size")]
        [SerializeField]
        private bool _autoComputeRevealMaxDistance = true;

        [SerializeField, Min(0.01f)]
        private float _revealMaxDistanceMultiplier = 2.25f;

        [SerializeField, Min(0.01f)]
        private float _minimumRevealMaxDistance = 1.0f;

        private MaterialPropertyBlock _propertyBlock;

        private static readonly int FieldVisibilityId =
            Shader.PropertyToID("_FieldVisibility");

        private static readonly int RevealMaxDistanceId =
            Shader.PropertyToID("_RevealMaxDistance");

        private static readonly int DefaultVisibleId =
            Shader.PropertyToID("_DefaultVisible");

        private static readonly int ActivationRevealId =
            Shader.PropertyToID("_ActivationReveal");

        private void Awake()
        {
            CacheReferences();

            _propertyBlock = new MaterialPropertyBlock();

            if (_overlayRenderer != null)
            {
                _overlayRenderer.enabled = true;
                ApplyProperties();
            }
        }

        private void Update()
        {
            // 쉴드는 항상 켜져 있도록 유지
            if (_overlayRenderer == null)
                return;

            if (!_overlayRenderer.enabled)
                _overlayRenderer.enabled = true;

            ApplyProperties();
        }

        public void Trigger(Vector3 hitWorldPosition)
        {
            CacheReferences();

            if (_forceFieldHit == null ||
                _overlayRenderer == null)
                return;

            // 쉴드가 꺼져 있더라도 다시 켬
            _overlayRenderer.enabled = true;

            ApplyProperties();

            // 실제 피격 효과 실행
            _forceFieldHit.TriggerHit(hitWorldPosition);
        }

        private void CacheReferences()
        {
            if (_forceFieldHit == null)
            {
                _forceFieldHit =
                    GetComponentInChildren<
                        ProceduralForceFieldHit
                    >(true);
            }

            if (_overlayRenderer == null &&
                _forceFieldHit != null)
            {
                _overlayRenderer =
                    _forceFieldHit.GetComponent<Renderer>();
            }
        }

        private void ApplyProperties()
        {
            if (_overlayRenderer == null)
                return;

            _overlayRenderer.GetPropertyBlock(
                _propertyBlock
            );

            _propertyBlock.SetFloat(
                FieldVisibilityId,
                1.0f
            );

            _propertyBlock.SetFloat(
                DefaultVisibleId,
                0.0f
            );

            _propertyBlock.SetFloat(
                ActivationRevealId,
                1.0f
            );

            if (_autoComputeRevealMaxDistance)
            {
                float boundsRadius =
                    _overlayRenderer.bounds.extents.magnitude;

                float maxDistance =
                    Mathf.Max(
                        _minimumRevealMaxDistance,
                        boundsRadius *
                        _revealMaxDistanceMultiplier
                    );

                _propertyBlock.SetFloat(
                    RevealMaxDistanceId,
                    maxDistance
                );
            }

            _overlayRenderer.SetPropertyBlock(
                _propertyBlock
            );
        }
    }
}