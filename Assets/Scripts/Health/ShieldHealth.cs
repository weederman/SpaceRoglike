using System;
using UnityEngine;
using ProceduralForceField;

/// <summary>
/// 쉴드의 체력(게임 로직)을 담당한다. Overlay와 같은 부모 오브젝트에 붙인다.
/// 연출은 직접 하지 않고 ProceduralForceFieldOverlay에 위임한다.
/// 쉴드를 떼어 낼 수 있다(Uninstall: 판정·연출이 꺼지고 맞지 않는다, Restore도 무시). 플레이어 함선은 쉴드 장비를 달 때(Install)만 쉴드가 있다(PlayerEquipment).
/// 재충전 속도가 있으면(Install로 정한다) 마지막으로 맞은 뒤 일정 시간이 지나면 다시 차오르고, 깨졌던 쉴드도 다시 켜진다.
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
    private float _lastHitTime = float.NegativeInfinity;
    private float _rechargeDelay;
    private float _rechargePerSecond;

    public float CurrentHp => _hp;
    public float MaxHp => _maxHp;
    public bool IsActive => Installed && _hp > 0f;

    /// <summary>쉴드가 달려 있는지 (떼어 내면 false: 맞지 않고 보이지 않는다)</summary>
    public bool Installed { get; private set; } = true;

    /// <summary>true면 데미지를 전혀 받지 않는다(개발자 모드 무적 등). 피격 연출은 그대로 나온다.</summary>
    public bool Invincible { get; set; }

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

        _lastHitTime = Time.time;
        if (!Invincible)
        {
            _hp = Mathf.Max(0f, _hp - damage);
            OnHpChanged?.Invoke(_hp, _maxHp);
        }

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

    private void Update()
    {
        if (!Installed || _rechargePerSecond <= 0f || _hp >= _maxHp || Time.time - _lastHitTime < _rechargeDelay)
            return;

        bool wasBroken = _hp <= 0f;
        _hp = Mathf.Min(_maxHp, _hp + _rechargePerSecond * Time.deltaTime);
        if (wasBroken)
            TurnOn();
        OnHpChanged?.Invoke(_hp, _maxHp);
    }

    /// <summary>쉴드를 단다: 최대 체력과 재충전(마지막 피격 rechargeDelay초 뒤부터 초당 rechargePerSecond)을 정하고 가득 채운다.</summary>
    public void Install(float maxHp, float rechargeDelay, float rechargePerSecond)
    {
        Installed = true;
        _rechargeDelay = Mathf.Max(0f, rechargeDelay);
        _rechargePerSecond = Mathf.Max(0f, rechargePerSecond);
        _maxHp = Mathf.Max(1f, maxHp);
        Restore(1f);
    }

    /// <summary>쉴드를 떼어 낸다: 판정과 연출을 끄고, 다시 달 때까지 맞지 않는다.</summary>
    public void Uninstall()
    {
        if (!Installed)
            return;

        Installed = false;
        _hp = 0f;
        if (_hitbox != null)
            _hitbox.enabled = false;
        _overlay.SetShieldActive(false);
        OnHpChanged?.Invoke(_hp, _maxHp);
    }

    /// <summary>최대 체력을 바꾸고 가득 채운다(개발자 모드 등). 깨진 쉴드는 다시 켜진다. 떼어 낸 쉴드는 값만 바꾼다.</summary>
    public void SetMaxHp(float maxHp)
    {
        _maxHp = Mathf.Max(1f, maxHp);
        if (!Installed)
            return;

        if (_hp <= 0f)
        {
            Restore(1f);
            return;
        }

        _hp = _maxHp;
        OnHpChanged?.Invoke(_hp, _maxHp);
    }

    /// <summary>
    /// 쉴드를 다시 켠다. hpRatio = 최대 체력 대비 회복 비율(0~1).
    /// </summary>
    public void Restore(float hpRatio = 1f)
    {
        if (!Installed)
            return;

        _hp = _maxHp * Mathf.Clamp01(hpRatio);
        if (_hp <= 0f)
            return;

        TurnOn();
        OnHpChanged?.Invoke(_hp, _maxHp);
    }

    private void TurnOn()
    {
        if (_hitbox != null)
            _hitbox.enabled = true;

        _overlay.SetShieldActive(true);
        OnRestored?.Invoke();
    }
}
