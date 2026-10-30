using FORGE3D;
using UnityEngine;

/// <summary>
/// 적 선체가 파괴되면(HullHealth.OnBroken) 크레딧을 지급한다.
/// 플레이어가 한 번이라도 맞힌 적만 준다 (정거장 경비선 같은 아군 NPC 혼자 격파한 적은 주지 않는다).
/// </summary>
[RequireComponent(typeof(HullHealth))]
public class EnemyKillReward : MonoBehaviour
{
    [SerializeField, Min(0)] private int _creditAmount = 100;

    private HullHealth _hull;
    private bool _hitByPlayer;

    private void Awake()
    {
        _hull = GetComponent<HullHealth>();
        _hull.OnBroken += HandleBroken;
        HitResolver.OnHit += HandleHit;
    }

    private void OnDestroy()
    {
        if (_hull != null)
            _hull.OnBroken -= HandleBroken;
        HitResolver.OnHit -= HandleHit;
    }

    private void HandleHit(IDamageable target, F3DFXType weapon, WeaponOwner owner)
    {
        // 쉴드에 맞아도 같은 함선이면 센다
        if (owner == WeaponOwner.Player && target is Component part && part.transform.root == transform.root)
            _hitByPlayer = true;
    }

    private void HandleBroken()
    {
        if (_hitByPlayer && CreditWallet.Instance != null)
            CreditWallet.Instance.Add(_creditAmount);
    }
}
