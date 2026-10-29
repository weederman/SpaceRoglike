using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 플레이어 함선의 화물칸 (채굴한 광석). 광석은 종류(OreCatalog의 id)별로 싣고, 한도(Capacity)는 모든 종류의 합이다.
/// 채굴 장비(MiningLaser)가 채우고, 정거장 상점 탭에서 시장가로 팔거나(Sell) 채굴 임무에 납품한다(Take).
/// 운송 임무의 화물(MissionCargo)도 같은 칸에 실려 한도를 함께 쓴다. 임무 화물은 팔 수 없고 저장하지 않는다(진행 중 임무를 저장하지 않으므로).
/// 한도는 기본값(_capacity) × (1 + 화물창 확장 장비의 %, PlayerEquipment).
/// 튜토리얼 정산 cue:
///   cargo_sell:가격     실린 광석을 종류와 상관없이 단위당 가격으로 모두 팔아 크레딧에 더한다 (예: cargo_sell:20)
/// CombatHud 프리팹(Assets/Resources/UI/CombatHud.prefab)에 붙어 함께 생성된다. 저장(SaveSystem)에 함께 남는다.
/// </summary>
public class CargoHold : MonoBehaviour
{
    private const string SellPrefix = "cargo_sell:";

    /// <summary>종류를 모를 때 쓰는 광석 (튜토리얼 광맥 = 희귀 광석)</summary>
    public const string DefaultOre = "rare";

    public static CargoHold Instance { get; private set; }

    [Tooltip("실을 수 있는 광석의 양 (모든 종류 합)")]
    [SerializeField, Min(1f)] private float _capacity = 100f;

    /// <summary>실린 양이 바뀌었을 때</summary>
    public event Action OnChanged;

    private readonly Dictionary<string, float> _ores = new Dictionary<string, float>();

    /// <summary>실린 광석의 합</summary>
    public float Ore => _ores.Values.Sum();
    /// <summary>운송 임무 화물의 이름과 개수 (없으면 0)</summary>
    public string MissionCargoName { get; private set; }
    public int MissionCargo { get; private set; }
    /// <summary>광석 + 임무 화물</summary>
    public float Used => Ore + MissionCargo;
    public float Capacity => _capacity * (1f + PlayerEquipment.Bonus(EquipmentCatalog.Kind.Cargo));
    public float Ratio => Used / Capacity;
    public bool IsFull => Used >= Capacity - 0.001f;

    /// <summary>실려 있는 광석 종류와 양 (0보다 많은 것만)</summary>
    public IEnumerable<KeyValuePair<string, float>> Contents => _ores.Where(p => p.Value > 0.001f);

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

    public float Amount(string oreId)
    {
        return _ores.TryGetValue(oreId, out float amount) ? amount : 0f;
    }

    /// <summary>광석을 싣는다. 한도를 넘는 만큼은 버리고, 실제로 실은 양을 돌려준다.</summary>
    public float Add(string oreId, float amount)
    {
        float added = Mathf.Clamp(amount, 0f, Capacity - Used);
        if (added <= 0f)
            return 0f;

        _ores[oreId] = Amount(oreId) + added;
        OnChanged?.Invoke();
        return added;
    }

    /// <summary>운송 임무 화물을 싣는다. 빈 자리가 모자라면 싣지 않고 false.</summary>
    public bool LoadMissionCargo(string displayName, int amount)
    {
        if (amount <= 0 || Capacity - Used < amount - 0.001f)
            return false;

        MissionCargoName = displayName;
        MissionCargo = amount;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>운송 임무 화물을 모두 내린다 (납품·실패·중단). 내린 개수를 돌려준다.</summary>
    public int UnloadMissionCargo()
    {
        int amount = MissionCargo;
        MissionCargo = 0;
        MissionCargoName = null;
        if (amount > 0)
            OnChanged?.Invoke();
        return amount;
    }

    /// <summary>그 광석을 최대 max만큼 내린다 (납품). 실제로 내린 양을 돌려준다.</summary>
    public float Take(string oreId, float max)
    {
        float taken = Mathf.Min(Amount(oreId), Mathf.Max(0f, max));
        if (taken <= 0f)
            return 0f;

        _ores[oreId] = Amount(oreId) - taken;
        OnChanged?.Invoke();
        return taken;
    }

    /// <summary>그 광석을 시장가(OreCatalog)로 모두 팔고 받은 크레딧을 돌려준다.</summary>
    public int Sell(string oreId)
    {
        int credits = Mathf.FloorToInt(Take(oreId, float.MaxValue) * OreCatalog.PriceOf(oreId));
        if (CreditWallet.Instance != null && credits > 0)
            CreditWallet.Instance.Add(credits);
        return credits;
    }

    /// <summary>실린 광석을 종류와 상관없이 단위당 price에 모두 팔고 받은 크레딧을 돌려준다 (튜토리얼 정산).</summary>
    public int SellAll(float price)
    {
        int credits = Mathf.RoundToInt(Ore * price);
        _ores.Clear();
        OnChanged?.Invoke();

        if (CreditWallet.Instance != null)
            CreditWallet.Instance.Add(credits);

        return credits;
    }

    /// <summary>저장용: "id:양" 목록</summary>
    public string[] ToSave()
    {
        return Contents.Select(p => $"{p.Key}:{p.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}").ToArray();
    }

    /// <summary>불러오기용: 화물칸을 이 목록으로 바꾼다</summary>
    public void Restore(string[] entries)
    {
        _ores.Clear();
        foreach (string entry in entries ?? new string[0])
        {
            string[] parts = entry.Split(':');
            if (parts.Length == 2 && float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float amount))
                _ores[parts[0]] = amount;
        }

        OnChanged?.Invoke();
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
