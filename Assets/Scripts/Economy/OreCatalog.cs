using System;
using UnityEngine;

/// <summary>
/// 광석 종류와 광맥 배치(단일 출처). 위치: Assets/Resources/Shop/OreCatalog.asset
///  - 광석마다 이름·아이콘·시장 가격·설명, 광맥 모양(MineableAsteroid.Look)
///  - 비콘 레벨별 광맥 수와 광맥 하나의 매장량 → OreDeposits가 비콘 지역에 깐다
/// 화물칸(CargoHold)과 임무는 광석을 id(예: iron, rare)로 부른다.
/// </summary>
[CreateAssetMenu(fileName = "OreCatalog", menuName = "SpaceRoglike/Ore Catalog")]
public class OreCatalog : ScriptableObject
{
    public const string ResourcePath = "Shop/OreCatalog";

    [Serializable]
    public class Ore
    {
        [Tooltip("화물칸·임무에서 부르는 이름 (예: iron)")]
        public string id;
        public string displayName;
        public Sprite icon;
        [Tooltip("정거장 시장에서 한 개를 파는 값")]
        [Min(0)] public int price = 10;
        [TextArea(2, 4)] public string description;

        [Header("광맥")]
        public MineableAsteroid.Look[] looks = new MineableAsteroid.Look[0];
        [Tooltip("비콘 레벨 1~5에 까는 광맥 수 (차례대로)")]
        public int[] depositsPerLevel = { 4, 4, 5, 5, 6 };
        [Tooltip("광맥 하나의 매장량 (최소~최대)")]
        public Vector2 amount = new Vector2(60f, 100f);
        [Tooltip("광맥 소행성 크기 (가장 긴 변)")]
        [Min(1f)] public float size = 110f;
    }

    public Ore[] ores = new Ore[0];

    [Header("광맥 배치")]
    [Tooltip("비콘에서 광맥까지 거리 (최소~최대)")]
    public Vector2 depositDistance = new Vector2(300f, 800f);
    [Tooltip("광맥끼리 이보다 가깝게 놓지 않는다")]
    [Min(0f)] public float depositSpacing = 220f;
    [Tooltip("함선이 다니는 높이 기준 광맥 높이 (음수 = 아래, 함선이 위로 지나간다)")]
    public float depositHeight = -30f;

    [Header("고갈 / 재생")]
    [Tooltip("다 캔 광맥이 부서진 뒤 다시 생기기까지 (초, 게임 시간)")]
    [Min(0f)] public float respawnSeconds = 600f;
    [Tooltip("다 캔 광맥이 부서질 때의 폭발 이펙트")]
    public GameObject breakEffect;
    [Tooltip("폭발 이펙트 크기 (광맥 크기 대비)")]
    [Min(0f)] public float breakEffectScale = 0.6f;

    private static OreCatalog _loaded;

    public static OreCatalog Load()
    {
        if (_loaded == null)
            _loaded = Resources.Load<OreCatalog>(ResourcePath);
        return _loaded;
    }

    public Ore Find(string id)
    {
        foreach (Ore ore in ores)
        {
            if (ore.id == id)
                return ore;
        }

        return null;
    }

    /// <summary>화면에 보이는 광석 이름 (모르는 id면 id 그대로)</summary>
    public static string NameOf(string id)
    {
        OreCatalog catalog = Load();
        Ore ore = catalog != null ? catalog.Find(id) : null;
        return ore != null ? ore.displayName : id;
    }

    public static int PriceOf(string id)
    {
        OreCatalog catalog = Load();
        Ore ore = catalog != null ? catalog.Find(id) : null;
        return ore != null ? ore.price : 0;
    }
}
