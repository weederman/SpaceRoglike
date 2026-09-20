using UnityEngine;
using System.Collections;

namespace ProceduralForceField
{
    [DisallowMultipleComponent]
    public sealed class ProceduralForceFieldHit : MonoBehaviour
    {
        private const int MaxHits = 8;

        [SerializeField] private Renderer _targetRenderer;

        [Header("Hit Settings")]
        [SerializeField, Min(0f)]
        private float _hitStrength = 1.0f;

        [SerializeField, Min(0.05f)]
        private float _hitDuration = 0.6f;

        // 이 컴포넌트가 원본인지 복제본인지
        [SerializeField, HideInInspector]
        private bool _isHitClone = false;

        private MaterialPropertyBlock _propertyBlock;

        private ProceduralForceFieldHit[] _hitClones;
        private int _nextHitIndex = 0;

        // Shader property IDs
        private static readonly int HitPositionId =
            Shader.PropertyToID("_HitPosition");

        private static readonly int HitTimeId =
            Shader.PropertyToID("_HitTime");

        private static readonly int HitStrengthId =
            Shader.PropertyToID("_HitStrength");

        private static readonly int BoundsRadiusWSId =
            Shader.PropertyToID("_BoundsRadiusWS");


        private void Awake()
        {
            if (_targetRenderer == null)
                _targetRenderer = GetComponent<Renderer>();

            _propertyBlock = new MaterialPropertyBlock();

            // 복제본은 자신의 Renderer만 관리
            if (_isHitClone)
            {
                if (_targetRenderer != null)
                    _targetRenderer.enabled = false;

                return;
            }

            // 원본은 일반적인 Shield FX이므로 그대로 표시
            if (_targetRenderer != null)
                _targetRenderer.enabled = true;
        }


        private void Start()
        {
            // 복제본은 추가 복제를 만들지 않음
            if (_isHitClone)
                return;

            CreateHitClones();
        }


        private void CreateHitClones()
        {
            // [추가] 이미 만들었으면 다시 만들지 않음
            // (Start 전에 TriggerHit이 먼저 불리면 16개가 생기던 문제 방지)
            if (_isHitClone || _hitClones != null)
                return;

            if (_targetRenderer == null)
                return;

            _hitClones = new ProceduralForceFieldHit[MaxHits];

            for (int i = 0; i < MaxHits; i++)
            {
                GameObject cloneObject =
                    Instantiate(
                        gameObject,
                        transform.parent
                    );

                cloneObject.name =
                    gameObject.name + "_HitClone_" + (i + 1);

                ProceduralForceFieldHit clone =
                    cloneObject.GetComponent<ProceduralForceFieldHit>();

                if (clone == null)
                {
                    Destroy(cloneObject);
                    continue;
                }

                clone._isHitClone = true;
                clone._hitStrength = _hitStrength;
                clone._hitDuration = _hitDuration;

                cloneObject.transform.position =
                    transform.position;

                cloneObject.transform.rotation =
                    transform.rotation;

                cloneObject.transform.localScale =
                    transform.localScale;

                Renderer cloneRenderer =
                    clone._targetRenderer;

                if (cloneRenderer == null)
                    cloneRenderer =
                        cloneObject.GetComponent<Renderer>();

                clone._targetRenderer = cloneRenderer;

                if (cloneRenderer != null)
                    cloneRenderer.enabled = false;

                // [추가] 안전장치: 원본에 콜라이더가 남아 있더라도 복제본에서는 끈다.
                // (판정은 ShieldHitbox 한 곳에서만 해야 함)
                foreach (Collider col in cloneObject.GetComponentsInChildren<Collider>(true))
                    col.enabled = false;

                _hitClones[i] = clone;
            }
        }


        public void TriggerHit(Vector3 worldPosition)
        {
            if (!_isHitClone && _hitClones == null)
            {
                CreateHitClones();
            }

            if (_isHitClone)
            {
                PlayHit(worldPosition);
                return;
            }

            if (_hitClones == null)
                return;

            for (int i = 0; i < MaxHits; i++)
            {
                int index =
                    (_nextHitIndex + i) % MaxHits;

                ProceduralForceFieldHit clone =
                    _hitClones[index];

                if (clone == null)
                    continue;

                _nextHitIndex =
                    (index + 1) % MaxHits;

                clone.PlayHit(worldPosition);
                return;
            }
        }


        /// <summary>
        /// [추가] 원본과 모든 복제본의 렌더러를 끄고 진행 중인 코루틴을 정리한다.
        /// 쉴드 파괴 시 Overlay.SetShieldActive(false)에서 호출.
        /// </summary>
        public void HideAll()
        {
            StopAllCoroutines();

            if (_targetRenderer != null)
                _targetRenderer.enabled = false;

            if (_hitClones == null)
                return;

            for (int i = 0; i < _hitClones.Length; i++)
            {
                ProceduralForceFieldHit clone = _hitClones[i];
                if (clone == null)
                    continue;

                clone.StopAllCoroutines();

                if (clone._targetRenderer != null)
                    clone._targetRenderer.enabled = false;
            }
        }


        private void PlayHit(Vector3 worldPosition)
        {
            if (_targetRenderer == null)
                return;

            if (_propertyBlock == null)
                _propertyBlock =
                    new MaterialPropertyBlock();

            _targetRenderer.enabled = true;

            Bounds bounds =
                _targetRenderer.bounds;

            float boundsRadius =
                Mathf.Max(
                    0.0001f,
                    bounds.extents.magnitude
                );

            _targetRenderer.GetPropertyBlock(
                _propertyBlock
            );

            _propertyBlock.SetVector(
                HitPositionId,
                new Vector4(
                    worldPosition.x,
                    worldPosition.y,
                    worldPosition.z,
                    1.0f
                )
            );

            _propertyBlock.SetFloat(
                HitTimeId,
                Time.time
            );

            _propertyBlock.SetFloat(
                HitStrengthId,
                _hitStrength
            );

            _propertyBlock.SetFloat(
                BoundsRadiusWSId,
                boundsRadius
            );

            _targetRenderer.SetPropertyBlock(
                _propertyBlock
            );

            StopAllCoroutines();

            StartCoroutine(
                HideAfterHit()
            );
        }


        private IEnumerator HideAfterHit()
        {
            yield return new WaitForSeconds(
                _hitDuration
            );

            if (_targetRenderer != null)
                _targetRenderer.enabled = false;
        }


        public void SetHitStrength(float strength)
        {
            _hitStrength =
                Mathf.Max(0.0f, strength);

            if (_hitClones == null)
                return;

            for (int i = 0; i < _hitClones.Length; i++)
            {
                if (_hitClones[i] != null)
                    _hitClones[i]._hitStrength =
                        _hitStrength;
            }
        }
    }
}
