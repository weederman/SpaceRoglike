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
        if (_explosionPrefab != null)
        {
            GameObject explosion = Instantiate(_explosionPrefab, transform.position, transform.rotation);
            Destroy(explosion, _explosionLifetime);
        }

        Destroy(transform.root.gameObject);
    }
}
