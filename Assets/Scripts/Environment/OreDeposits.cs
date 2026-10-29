using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 비콘 지역(소행성대)의 광맥. 비콘에 들어서면 그 비콘 둘레에 광맥 소행성(MineableAsteroid)을 깔고, 떠나면 걷어낸다.
///  - 광맥의 종류·수·매장량은 OreCatalog (비콘 레벨이 높을수록 많고, 희귀 광석은 높은 레벨에만)
///  - 자리는 비콘마다 늘 같다 (비콘 번호로 정한 무작위)
///  - 캐 간 양은 기억한다: 다시 와도 남은 만큼만 있다 (게임을 다시 시작하면 처음부터)
///  - 튜토리얼 임무가 진행 중이면 튜토리얼 비콘에는 깔지 않는다 (튜토리얼은 자기 광맥을 쓴다)
/// 깔고 걷는 일은 StarSystem이 지역이 바뀔 때 부른다.
/// </summary>
public static class OreDeposits
{
    // 비콘 번호 → 광맥마다 남은 매장량 (처음 깔 때 정한 순서대로)
    private static readonly Dictionary<int, float[]> Remaining = new Dictionary<int, float[]>();
    private static readonly List<MineableAsteroid> Spawned = new List<MineableAsteroid>();
    private static int _beacon = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Remaining.Clear();
        Spawned.Clear();
        _beacon = -1;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Remaining.Clear();
        Spawned.Clear();
        _beacon = -1;
    }

    /// <summary>비콘 둘레에 광맥을 깐다. center = 비콘 위치, level = 비콘 레벨.</summary>
    public static void Spawn(int beacon, int level, Vector3 center, Transform parent)
    {
        Clear();

        OreCatalog catalog = OreCatalog.Load();
        if (catalog == null)
            return;

        // 같은 비콘이면 늘 같은 자리·같은 매장량
        var rng = new System.Random(beacon * 7919 + 17);
        var placed = new List<Vector3>();
        var slots = new List<(OreCatalog.Ore ore, Vector3 position, float amount)>();

        foreach (OreCatalog.Ore ore in catalog.ores)
        {
            int count = ore.depositsPerLevel.Length > 0
                ? ore.depositsPerLevel[Mathf.Clamp(level - 1, 0, ore.depositsPerLevel.Length - 1)]
                : 0;

            for (int i = 0; i < count; i++)
            {
                Vector3 position = PickPosition(rng, catalog, center, placed);
                float amount = Mathf.Lerp(ore.amount.x, ore.amount.y, (float)rng.NextDouble());
                placed.Add(position);
                slots.Add((ore, position, amount));
            }
        }

        if (!Remaining.TryGetValue(beacon, out float[] left) || left.Length != slots.Count)
        {
            left = new float[slots.Count];
            for (int i = 0; i < slots.Count; i++)
                left[i] = slots[i].amount;
            Remaining[beacon] = left;
        }

        _beacon = beacon;
        for (int i = 0; i < slots.Count; i++)
        {
            var (ore, position, amount) = slots[i];
            var go = new GameObject($"Deposit_{ore.id}_{i}");
            go.transform.SetParent(parent, true);
            go.transform.position = position;

            var deposit = go.AddComponent<MineableAsteroid>();
            MineableAsteroid.Look look = ore.looks.Length > 0 ? ore.looks[rng.Next(ore.looks.Length)] : null;
            deposit.Init(look, ore.size, amount, ore.id);
            deposit.SetRemaining(left[i]);
            deposit.Reveal(true);
            Spawned.Add(deposit);
        }
    }

    /// <summary>깔린 광맥을 걷어낸다 (남은 매장량은 기억해 둔다)</summary>
    public static void Clear()
    {
        if (_beacon >= 0 && Remaining.TryGetValue(_beacon, out float[] left))
        {
            for (int i = 0; i < Spawned.Count && i < left.Length; i++)
            {
                if (Spawned[i] != null)
                    left[i] = Spawned[i].Remaining;
            }
        }

        foreach (MineableAsteroid deposit in Spawned)
        {
            if (deposit != null)
                Object.Destroy(deposit.gameObject);
        }

        Spawned.Clear();
        _beacon = -1;
    }

    private static Vector3 PickPosition(System.Random rng, OreCatalog catalog, Vector3 center, List<Vector3> placed)
    {
        Vector3 best = center;
        float bestGap = -1f;

        // 다른 광맥과 충분히 떨어진 자리를 몇 번 찾아보고, 못 찾으면 가장 떨어진 곳
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Lerp(catalog.depositDistance.x, catalog.depositDistance.y, (float)rng.NextDouble());
            Vector3 candidate = center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * distance;
            candidate.y = center.y + catalog.depositHeight;

            float gap = float.PositiveInfinity;
            foreach (Vector3 other in placed)
                gap = Mathf.Min(gap, Vector2.Distance(new Vector2(candidate.x, candidate.z), new Vector2(other.x, other.z)));

            if (gap >= catalog.depositSpacing)
                return candidate;

            if (gap > bestGap)
            {
                bestGap = gap;
                best = candidate;
            }
        }

        return best;
    }
}
