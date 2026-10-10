using System;
using UnityEngine;

/// <summary>
/// 공통 재화(크레딧) 보관소. 씬에 하나만 두고 CreditWallet.Instance로 접근한다.
/// 씬을 다시 불러오면 새로 시작 크레딧으로 초기화된다.
/// </summary>
public class CreditWallet : MonoBehaviour
{
    public static CreditWallet Instance { get; private set; }

    [SerializeField, Min(0)] private int _startCredits = 0;

    /// <summary>현재 크레딧이 바뀔 때마다 (새 총량)</summary>
    public event Action<int> OnChanged;

    public int Amount { get; private set; }

    private void Awake()
    {
        Instance = this;
        Amount = _startCredits;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>크레딧이 충분하면 차감하고 true, 부족하면 아무것도 하지 않고 false.</summary>
    public bool TrySpend(int amount)
    {
        if (amount < 0 || Amount < amount)
            return false;

        Amount -= amount;
        OnChanged?.Invoke(Amount);
        return true;
    }

    /// <summary>크레딧 총량을 직접 정한다(개발자 모드 등). 0 미만은 0으로 맞춘다.</summary>
    public void SetAmount(int amount)
    {
        Amount = Mathf.Max(0, amount);
        OnChanged?.Invoke(Amount);
    }

    public void Add(int amount)
    {
        if (amount <= 0)
            return;

        Amount += amount;
        OnChanged?.Invoke(Amount);
    }
}
