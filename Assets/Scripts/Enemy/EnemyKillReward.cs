using UnityEngine;

/// <summary>
/// 적 선체가 파괴되면(HullHealth.OnBroken) 크레딧을 지급한다.
/// </summary>
[RequireComponent(typeof(HullHealth))]
public class EnemyKillReward : MonoBehaviour
{
    [SerializeField, Min(0)] private int _creditAmount = 100;

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
        if (CreditWallet.Instance != null)
            CreditWallet.Instance.Add(_creditAmount);
    }
}
