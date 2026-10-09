using System;
using UnityEngine;

/// <summary>
/// 함선 아머(선체) 체력. 쉴드가 깨진 뒤 선체 콜라이더에 맞으면 이 컴포넌트가 데미지를 받는다.
/// ShieldHealth와 달리 연출을 강제하지 않는다(피격 연출이 필요하면 OnHpChanged/OnBroken을 구독).
/// </summary>
[DisallowMultipleComponent]
public class HullHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField, Min(1f)] private float _maxHp = 5000f;

    /// <summary>(현재 체력, 최대 체력) — UI 게이지 등에서 구독</summary>
    public event Action<float, float> OnHpChanged;
    /// <summary>체력이 0이 되어 선체가 파괴되었을 때 (패배/격파 처리에서 구독)</summary>
    public event Action OnBroken;

    private float _hp;
    private bool _isBroken;

    public float CurrentHp => _hp;
    public float MaxHp => _maxHp;
    public bool IsActive => !_isBroken;

    private void Awake()
    {
        _hp = _maxHp;
    }

    public void TakeDamage(float damage, Vector3 hitPoint)
    {
        if (_isBroken || damage <= 0f)
            return;

        _hp = Mathf.Max(0f, _hp - damage);
        OnHpChanged?.Invoke(_hp, _maxHp);

        if (_hp <= 0f)
            Break();
    }

    private void Break()
    {
        _isBroken = true;
        OnBroken?.Invoke();
    }

    /// <summary>
    /// 선체 체력을 다시 채운다(정비/업그레이드 등). hpRatio = 최대 체력 대비 비율(0~1).
    /// </summary>
    public void Restore(float hpRatio = 1f)
    {
        _hp = _maxHp * Mathf.Clamp01(hpRatio);
        _isBroken = _hp <= 0f;
        OnHpChanged?.Invoke(_hp, _maxHp);
    }
}
