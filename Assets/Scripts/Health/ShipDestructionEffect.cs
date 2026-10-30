using UnityEngine;

/// <summary>
/// 선체 체력이 0이 되면(HullHealth.OnBroken) 폭발 이펙트와 폭발음을 한 번 재생하고 함선을 제거한다.
/// 폭발음은 SoundTable의 이름으로 부른다(비우면 소리 없음).
/// </summary>
[RequireComponent(typeof(HullHealth))]
public class ShipDestructionEffect : MonoBehaviour
{
    [SerializeField] private GameObject _explosionPrefab;
    [SerializeField, Min(0f)] private float _explosionLifetime = 5f;
    [Tooltip("폭발 이펙트 크기 배율")]
    [SerializeField, Min(0.1f)] private float _explosionScale = 1f;
    [Tooltip("폭발음 (SoundTable 이름)")]
    [SerializeField] private string _explosionSound = "ship_explosion";

    private HullHealth _hull;
    private bool _hasTriggered;

    private void Awake()
    {
        _hull = GetComponent<HullHealth>();
        _hull.OnBroken += HandleBroken;
    }

    private void OnDestroy()
    {
        if (_hull != null)
            _hull.OnBroken -= HandleBroken;
    }

    private void HandleBroken()
    {
        if (_hasTriggered)
            return;

        _hasTriggered = true;

        if (_explosionPrefab != null)
        {
            GameObject explosion = Instantiate(_explosionPrefab, transform.position, transform.rotation);
            explosion.transform.localScale *= _explosionScale;

            // 이 폭발 프리팹의 파티클 시스템들이 전부 looping=true로 되어 있어서
            // 그대로 두면 정리될 때까지 계속 반복 재생된다(한 번이 아니라 여러 번 터지는 것처럼 보임).
            // 한 번만 재생되도록 생성 시점에 반복을 꺼준다.
            foreach (var ps in explosion.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.loop = false;
                // 크기 배율이 파티클에도 먹도록 (기본 Local이면 부모 배율을 무시한다)
                if (!Mathf.Approximately(_explosionScale, 1f))
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }

            Destroy(explosion, _explosionLifetime);
        }

        if (!string.IsNullOrEmpty(_explosionSound))
            GameAudio.Play(_explosionSound);

        Destroy(transform.root.gameObject);
    }
}
