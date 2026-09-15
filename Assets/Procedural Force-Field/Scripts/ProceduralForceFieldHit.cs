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
            if (_targetRenderer == null)
                return;

            _hitClones = new ProceduralForceFieldHit[MaxHits];

            // 원본은 첫 번째 슬롯으로 사용하지 않고
            // 별도의 복제본 8개를 생성
            for (int i = 0; i < MaxHits; i++)
            {
                GameObject cloneObject =
                    Instantiate(
                        gameObject,
                        transform.parent
                    );

                cloneObject.name =
                    gameObject.name + "_HitClone_" + (i + 1);

                // 복제본임을 표시
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

                // 원본과 동일한 위치/회전/크기
                cloneObject.transform.position =
                    transform.position;

                cloneObject.transform.rotation =
                    transform.rotation;

                cloneObject.transform.localScale =
                    transform.localScale;

                // Renderer 가져오기
                Renderer cloneRenderer =
                    clone._targetRenderer;

                if (cloneRenderer == null)
                    cloneRenderer =
                        cloneObject.GetComponent<Renderer>();

                clone._targetRenderer = cloneRenderer;

                // 처음에는 보이지 않게
                if (cloneRenderer != null)
                    cloneRenderer.enabled = false;

                _hitClones[i] = clone;
            }
        }


        public void TriggerHit(Vector3 worldPosition)
        {
            // 아직 Start가 실행되지 않았다면
            // 안전하게 준비
            if (!_isHitClone &&
                (_hitClones == null || _hitClones.Length == 0))
            {
                CreateHitClones();
            }

            // 복제본 자체가 직접 호출된 경우
            if (_isHitClone)
            {
                PlayHit(worldPosition);
                return;
            }

            // 사용할 복제본 찾기
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


        private void PlayHit(Vector3 worldPosition)
        {
            if (_targetRenderer == null)
                return;

            if (_propertyBlock == null)
                _propertyBlock =
                    new MaterialPropertyBlock();

            // Renderer 켜기
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

            // 원본 Shader가 사용하는 단일 변수에 전달
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

            // 이전 코루틴이 있더라도
            // 새로운 피격부터 다시 유지
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