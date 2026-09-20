using System;
using UnityEngine;
using ProceduralForceField;

/// <summary>
/// 쉴드의 체력(게임 로직)을 담당한다. Overlay와 같은 부모 오브젝트에 붙인다.
/// 연출은 직접 하지 않고 ProceduralForceFieldOverlay에 위임한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ProceduralForceFieldOverlay))]
public class ShieldHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField, Min(1f)] private float _maxHp = 10000000f;

    [Header("References")]
    [Tooltip("총알 판정용 콜라이더 (ShieldHitbox 자식). 파괴 시 꺼진다.")]
    [SerializeField] private Collider _hitbox;
    [SerializeField] private ProceduralForceFieldOverlay _overlay;

    [Header("FX")]
    [Tooltip("피격 연출 최소 간격(초). 지속형 빔이 매 프레임 복제본을 덮어쓰는 것을 방지.")]
    [SerializeField, Min(0f)] private float _fxInterval = 0.08f;

    /// <summary>(현재 체력, 최대 체력) — UI 게이지 등에서 구독</summary>
    public event Action<float, float> OnHpChanged;
    /// <summary>체력이 0이 되어 쉴드가 깨졌을 때</summary>
    public event Action OnBroken;
    /// <summary>쉴드가 다시 켜졌을 때 (웨이브 종료, 업그레이드 등)</summary>
    public event Action OnRestored;

    private float _hp;
    private float _lastFxTime = float.NegativeInfinity;

    public float CurrentHp => _hp;
    public float MaxHp => _maxHp;
    public bool IsActive => _hp > 0f;

    private void Awake()
    {
        if (_overlay == null)
            _overlay = GetComponent<ProceduralForceFieldOverlay>();

        if (_hitbox == null)
        {
            _hitbox = GetComponentInChildren<Collider>(true);
            Debug.LogWarning(
                $"[ShieldHealth] {name}: _hitbox가 비어 있어 자식에서 자동으로 찾았습니다 ({_hitbox}). " +
                "인스펙터에서 ShieldHitbox를 직접 연결하는 것을 권장합니다.", this);
        }

        _hp = _maxHp;
    }

    public void TakeDamage(float damage, Vector3 hitPoint)
    {
        if (!IsActive || damage <= 0f)
            return;

        _hp = Mathf.Max(0f, _hp - damage);
        OnHpChanged?.Invoke(_hp, _maxHp);

        // 연출 빈도 제한 (데미지 계산과는 무관)
        if (Time.time - _lastFxTime >= _fxInterval)
        {
            _overlay.Trigger(hitPoint);
            _lastFxTime = Time.time;
        }

        if (_hp <= 0f)
            Break();
    }

    private void Break()
    {
        // 판정 먼저 끈다 → 같은 프레임의 다음 Raycast부터 선체에 맞는다
        if (_hitbox != null)
            _hitbox.enabled = false;

        _overlay.SetShieldActive(false);
        OnBroken?.Invoke();
    }

    /// <summary>
    /// 쉴드를 다시 켠다. hpRatio = 최대 체력 대비 회복 비율(0~1).
    /// </summary>
    public void Restore(float hpRatio = 1f)
    {
        _hp = _maxHp * Mathf.Clamp01(hpRatio);
        if (_hp <= 0f)
            return;

        if (_hitbox != null)
            _hitbox.enabled = true;

        _overlay.SetShieldActive(true);
        OnHpChanged?.Invoke(_hp, _maxHp);
        OnRestored?.Invoke();
    }
}
