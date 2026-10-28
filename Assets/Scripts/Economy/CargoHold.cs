using System;
using UnityEngine;

/// <summary>
/// 플레이어 함선의 화물칸 (채굴한 광물). 채굴 장비(MiningLaser)가 채우고, 정거장 정산 cue로 판다.
///   cargo_sell:가격     실린 광물을 단위당 가격으로 모두 팔아 크레딧에 더한다 (예: cargo_sell:20)
/// 적재 한도(Capacity)는 의뢰 목표치이기도 하다. CombatHud 프리팹(Assets/Resources/UI/CombatHud.prefab)에 붙어 함께 생성된다.
/// </summary>
public class CargoHold : MonoBehaviour
{
    private const string SellPrefix = "cargo_sell:";

    public static CargoHold Instance { get; private set; }

    [Tooltip("실을 수 있는 광물의 양")]
    [SerializeField, Min(1f)] private float _capacity = 100f;

    /// <summary>실린 양이 바뀌었을 때</summary>
    public event Action OnChanged;

    public float Ore { get; private set; }
    public float Capacity => _capacity;
    public float Ratio => Ore / _capacity;
    public bool IsFull => Ore >= _capacity - 0.001f;

    private void Awake()
    {
        Instance = this;
        StoryPlayer.OnCue += HandleCue;
    }

    private void OnDestroy()
    {
        StoryPlayer.OnCue -= HandleCue;
        if (Instance == this)
            Instance = null;
    }

    /// <summary>광물을 싣는다. 한도를 넘는 만큼은 버리고, 실제로 실은 양을 돌려준다.</summary>
    public float Add(float amount)
    {
        float added = Mathf.Clamp(amount, 0f, _capacity - Ore);
        if (added <= 0f)
            return 0f;

        Ore += added;
        OnChanged?.Invoke();
        return added;
    }

    /// <summary>실린 광물을 단위당 price에 모두 팔고 받은 크레딧을 돌려준다.</summary>
    public int SellAll(float price)
    {
        int credits = Mathf.RoundToInt(Ore * price);
        Ore = 0f;
        OnChanged?.Invoke();

        if (CreditWallet.Instance != null)
            CreditWallet.Instance.Add(credits);

        return credits;
    }

    private void HandleCue(string cue)
    {
        if (!cue.StartsWith(SellPrefix))
            return;

        if (float.TryParse(cue.Substring(SellPrefix.Length), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float price))
            SellAll(price);
        else
            Debug.LogWarning($"[Story] '{cue}'의 가격을 읽지 못했습니다. (예: cargo_sell:20)");
    }
}
