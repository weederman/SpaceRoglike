using UnityEngine;

/// <summary>
/// 선체 체력이 0이 되면(HullHealth.OnBroken) 폭발 이펙트를 한 번 재생하고 함선을 제거한다.
/// </summary>
[RequireComponent(typeof(HullHealth))]
public class ShipDestructionEffect : MonoBehaviour
{
    [SerializeField] private GameObject _explosionPrefab;
    [SerializeField, Min(0f)] private float _explosionLifetime = 5f;

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

            // 이 폭발 프리팹의 파티클 시스템들이 전부 looping=true로 되어 있어서
            // 그대로 두면 정리될 때까지 계속 반복 재생된다(한 번이 아니라 여러 번 터지는 것처럼 보임).
            // 한 번만 재생되도록 생성 시점에 반복을 꺼준다.
            foreach (var ps in explosion.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.loop = false;
            }

            Destroy(explosion, _explosionLifetime);
        }

        Destroy(transform.root.gameObject);
    }
}
