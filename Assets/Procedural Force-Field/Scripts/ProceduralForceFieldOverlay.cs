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

        // [추가] 쉴드가 살아 있는지. false면 강제 켜기/피격 연출을 모두 막는다.
        private bool _isShieldActive = true;
        public bool IsShieldActive => _isShieldActive;

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

        // [추가] 시작 표시 여부 적용.
        // Awake가 아니라 Start인 이유: 자식 Hit의 Awake가 원본 렌더러를 켜기 때문에,
        // 모든 Awake가 끝난 뒤(Start)에 적용해야 _startVisible=false가 덮어써지지 않는다.
        private void Start()
        {
            SetShieldActive(_startVisible);
        }

        private void Update()
        {
            // [변경] 쉴드가 살아 있을 때만 항상 켜진 상태를 유지
            if (!_isShieldActive || _overlayRenderer == null)
                return;

            if (!_overlayRenderer.enabled)
                _overlayRenderer.enabled = true;

            ApplyProperties();
        }

        public void Trigger(Vector3 hitWorldPosition)
        {
            // [추가] 깨진 쉴드는 피격 연출도 없음
            if (!_isShieldActive)
                return;

            CacheReferences();

            if (_forceFieldHit == null ||
                _overlayRenderer == null)
                return;

            _overlayRenderer.enabled = true;

            ApplyProperties();

            // 실제 피격 효과 실행
            _forceFieldHit.TriggerHit(hitWorldPosition);
        }

        /// <summary>
        /// [추가] 쉴드 표시 on/off. false면 원본 + 복제본 렌더러를 모두 끈다.
        /// </summary>
        public void SetShieldActive(bool active)
        {
            _isShieldActive = active;

            CacheReferences();

            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();

            if (!active && _forceFieldHit != null)
                _forceFieldHit.HideAll();

            if (_overlayRenderer != null)
            {
                _overlayRenderer.enabled = active;

                if (active)
                    ApplyProperties();
            }
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
