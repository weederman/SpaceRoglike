using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 비콘 지역(소행성대)의 광맥. 비콘에 들어서면 그 비콘 둘레에 광맥 소행성(MineableAsteroid)을 깔고, 떠나면 걷어낸다.
///  - 광맥의 종류·수·매장량은 OreCatalog (비콘 레벨이 높을수록 많고, 희귀 광석은 높은 레벨에만)
///  - 자리는 비콘마다 늘 같다 (비콘 번호로 정한 무작위)
///  - 캐 간 양은 기억한다: 다시 와도 남은 만큼만 있다 (게임을 다시 시작하면 처음부터)
///  - 다 캔 광맥은 부서지고(폭발), OreCatalog.respawnSeconds(게임 시간)가 지나면 같은 자리에 가득 찬 채로 다시 생긴다
///    (그 지역에 있으면 그 자리에서, 떠나 있었으면 다음에 들어설 때)
///  - 튜토리얼 임무가 진행 중이면 튜토리얼 비콘에는 깔지 않는다 (튜토리얼은 자기 광맥을 쓴다)
/// 깔고 걷는 일은 StarSystem이 지역이 바뀔 때 부르고, 고갈·재생은 StarSystem이 매 프레임 Tick으로 살핀다.
/// </summary>
public static class OreDeposits
{
    // 비콘 하나의 광맥들 (처음 깔 때 정한 순서대로)
    private sealed class BeaconState
    {
        public float[] remaining;
        public float[] depletedAt; // 부서진 게임 시각 (부서지지 않았으면 NotDepleted)
    }

    // 지금 깔린 비콘의 광맥 자리 하나
    private sealed class Slot
    {
        public OreCatalog.Ore ore;
        public Vector3 position;
        public float amount;
        public MineableAsteroid.Look look;
        public MineableAsteroid deposit; // 부서져 있으면 null
    }

    private const float NotDepleted = float.NegativeInfinity;

    private static readonly Dictionary<int, BeaconState> States = new Dictionary<int, BeaconState>();
    private static readonly List<Slot> Slots = new List<Slot>();
    private static int _beacon = -1;
    private static Transform _parent;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        States.Clear();
        Slots.Clear();
        _beacon = -1;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        States.Clear();
        Slots.Clear();
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

        foreach (OreCatalog.Ore ore in catalog.ores)
        {
            int count = ore.depositsPerLevel.Length > 0
                ? ore.depositsPerLevel[Mathf.Clamp(level - 1, 0, ore.depositsPerLevel.Length - 1)]
                : 0;

            for (int i = 0; i < count; i++)
            {
                Vector3 position = PickPosition(rng, catalog, center, placed);
                float amount = Mathf.Lerp(ore.amount.x, ore.amount.y, (float)rng.NextDouble());
                MineableAsteroid.Look look = ore.looks.Length > 0 ? ore.looks[rng.Next(ore.looks.Length)] : null;
                placed.Add(position);
                Slots.Add(new Slot { ore = ore, position = position, amount = amount, look = look });
            }
        }

        if (!States.TryGetValue(beacon, out BeaconState state) || state.remaining.Length != Slots.Count)
        {
            state = new BeaconState { remaining = new float[Slots.Count], depletedAt = new float[Slots.Count] };
            for (int i = 0; i < Slots.Count; i++)
            {
                state.remaining[i] = Slots[i].amount;
                state.depletedAt[i] = NotDepleted;
            }

            States[beacon] = state;
        }

        _beacon = beacon;
        _parent = parent;
        for (int i = 0; i < Slots.Count; i++)
        {
            // 부서진 광맥은 다시 생길 때가 지났으면 가득 채워 깔고, 아니면 비워 둔다
            if (state.depletedAt[i] != NotDepleted)
            {
                if (Time.time - state.depletedAt[i] < catalog.respawnSeconds)
                    continue;
                Regrow(state, i);
            }

            Create(i, state.remaining[i]);
        }
    }

    /// <summary>깔린 광맥을 걷어낸다 (남은 매장량은 기억해 둔다)</summary>
    public static void Clear()
    {
        if (_beacon >= 0 && States.TryGetValue(_beacon, out BeaconState state))
        {
            for (int i = 0; i < Slots.Count && i < state.remaining.Length; i++)
            {
                if (Slots[i].deposit != null)
                    state.remaining[i] = Slots[i].deposit.Remaining;
            }
        }

        foreach (Slot slot in Slots)
        {
            if (slot.deposit != null)
                Object.Destroy(slot.deposit.gameObject);
        }

        Slots.Clear();
        _beacon = -1;
    }

    /// <summary>다 캔 광맥을 부수고, 다시 생길 때가 된 광맥을 깐다 (StarSystem이 매 프레임 부른다)</summary>
    public static void Tick()
    {
        if (_beacon < 0 || !States.TryGetValue(_beacon, out BeaconState state))
            return;

        OreCatalog catalog = OreCatalog.Load();
        for (int i = 0; i < Slots.Count; i++)
        {
            Slot slot = Slots[i];
            if (slot.deposit != null)
            {
                if (slot.deposit.HasOre)
                    continue;

                // 다 캤다: 부서진다
                state.remaining[i] = 0f;
                state.depletedAt[i] = Time.time;
                slot.deposit.Shatter(catalog != null ? catalog.breakEffect : null, catalog != null ? catalog.breakEffectScale : 1f);
                slot.deposit = null;
            }
            else if (state.depletedAt[i] != NotDepleted && catalog != null && Time.time - state.depletedAt[i] >= catalog.respawnSeconds)
            {
                Regrow(state, i);
                Create(i, state.remaining[i]);
            }
        }
    }

    private static void Regrow(BeaconState state, int index)
    {
        state.remaining[index] = Slots[index].amount;
        state.depletedAt[index] = NotDepleted;
    }

    private static void Create(int index, float remaining)
    {
        Slot slot = Slots[index];
        var go = new GameObject($"Deposit_{slot.ore.id}_{index}");
        go.transform.SetParent(_parent, true);
        go.transform.position = slot.position;

        var deposit = go.AddComponent<MineableAsteroid>();
        deposit.Init(slot.look, slot.ore.size, slot.amount, slot.ore.id);
        deposit.SetRemaining(remaining);
        deposit.Reveal(true);
        slot.deposit = deposit;
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
