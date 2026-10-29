using System;
using System.Collections.Generic;
using FORGE3D;
using UnityEngine;

/// <summary>
/// 임무에 나오는 적 함종 목록(단일 출처). 위치: Assets/Resources/Missions/EnemyShips.asset
/// 함종마다 겉모습 모델·크기·체력·무기·공격력·행동 수치(PirateShip.Tuning)를 정하고, Spawn으로 만든다.
/// 모든 함종은 기본 적 프리팹(EnEmy)을 바탕으로 한다 — 판정(쉴드·선체 충돌체)·터렛·체력바는 그대로 두고,
/// 크기를 배율로 키우고 원래 선체 메시를 숨긴 뒤 함종 모델을 같은 자리에 붙인다(PlayerShipAppearance와 같은 방식).
/// 공격력은 전역 WeaponStatsTable이 아니라 여기 값을 터렛에 런타임으로 준다(일반 적은 그대로 표를 쓴다).
/// </summary>
[CreateAssetMenu(fileName = "EnemyShips", menuName = "SpaceRoglike/Enemy Ship Catalog")]
public class EnemyShipCatalog : ScriptableObject
{
    public const string ResourcePath = "Missions/EnemyShips";

    [Serializable]
    public class ShipClass
    {
        [Tooltip("임무 데이터에서 부르는 이름 (예: light_fighter)")]
        public string id;
        public string displayName;

        [Header("겉모습")]
        [Tooltip("함종 모델. 비우면 기본 적 프리팹의 모습 그대로")]
        public GameObject model;
        public Vector3 modelRotation;
        [Tooltip("모델의 가장 긴 변 = 선체 판정 길이 × 이 값")]
        [Min(0.1f)] public float modelLengthRatio = 1f;
        [Tooltip("기본 적 프리팹 대비 크기 배율 (판정·쉴드·터렛 모두)")]
        [Min(0.1f)] public float scale = 1f;

        [Header("체력")]
        [Min(1f)] public float shieldHp = 200f;
        [Min(1f)] public float hullHp = 150f;

        [Header("무기")]
        public F3DFXType weapon = F3DFXType.Vulcan;
        [Tooltip("터렛 한 발의 공격력")]
        [Min(0f)] public float damage = 40f;

        [Header("행동 (속도·사거리·발사 간격 등)")]
        public PirateShip.Tuning tuning = new PirateShip.Tuning();
    }

    [Tooltip("모든 함종의 바탕이 되는 적 프리팹 (EnEmy)")]
    [SerializeField] private GameObject _basePrefab;
    [SerializeField] private List<ShipClass> _classes = new List<ShipClass>();

    private static EnemyShipCatalog _loaded;

    public static EnemyShipCatalog Load()
    {
        if (_loaded == null)
            _loaded = Resources.Load<EnemyShipCatalog>(ResourcePath);
        return _loaded;
    }

    public ShipClass Find(string id)
    {
        foreach (ShipClass c in _classes)
        {
            if (c.id == id)
                return c;
        }

        return null;
    }

    /// <summary>그 함종의 적 함선을 만든다. PirateShip이 붙은 채로 돌려주며, 상태(Raid 등)는 부르는 쪽이 정한다.</summary>
    public PirateShip Spawn(string id, Vector3 position, Quaternion rotation, Transform player, GameObject warpPrefab)
    {
        ShipClass shipClass = Find(id);
        if (shipClass == null || _basePrefab == null)
        {
            Debug.LogWarning($"[EnemyShips] '{id}' 함종이 없습니다. (Assets/Resources/{ResourcePath}.asset)");
            return null;
        }

        GameObject go = Instantiate(_basePrefab, position, rotation);
        go.name = shipClass.displayName;
        go.transform.localScale = _basePrefab.transform.localScale * shipClass.scale;

        ApplyModel(go, shipClass);

        ShieldHealth shield = go.GetComponentInChildren<ShieldHealth>();
        if (shield != null)
            shield.SetMaxHp(shipClass.shieldHp);

        HullHealth hull = go.GetComponentInChildren<HullHealth>();
        if (hull != null)
            hull.SetMaxHp(shipClass.hullHp);

        foreach (F3DFXController fx in go.GetComponentsInChildren<F3DFXController>(true))
        {
            fx.owner = WeaponOwner.Enemy;
            fx.DefaultFXType = shipClass.weapon;
            fx.damageOverride = shipClass.damage;
        }

        var pirate = go.AddComponent<PirateShip>();
        pirate.Init(shipClass.tuning, player, warpPrefab);
        DevTuning.ApplyToEnemy(go); // 개발자 모드로 바꿔 둔 적 체력 등 (런타임만)
        return pirate;
    }

    private static void ApplyModel(GameObject ship, ShipClass shipClass)
    {
        if (shipClass.model == null)
            return;

        // 원래 선체 메시는 숨긴다. 쉴드·터렛·체력바 같은 자식은 그대로다.
        var original = ship.GetComponent<MeshRenderer>();
        if (original != null)
            original.enabled = false;

        GameObject model = Instantiate(shipClass.model, ship.transform);
        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(shipClass.modelRotation);
        model.transform.localScale = Vector3.one;

        // 장식용 모델이라 판정은 원래 함선의 것을 쓴다
        foreach (Collider c in model.GetComponentsInChildren<Collider>(true))
            Destroy(c);

        HullHealth hull = ship.GetComponentInChildren<HullHealth>();
        Collider hitbox = hull != null ? hull.GetComponent<Collider>() : null;
        if (hitbox == null)
            return;

        // 방금 만들고 크기를 바꾼 함선이라 충돌체 범위가 아직 옛 크기다 → 물리에 반영한 뒤 잰다
        Physics.SyncTransforms();
        Vector3 hullSize = hitbox.bounds.size;
        float hullLength = Mathf.Max(hullSize.x, Mathf.Max(hullSize.y, hullSize.z));
        float modelLength = LongestSide(model);
        if (modelLength > 0.001f)
            model.transform.localScale = Vector3.one * (hullLength * shipClass.modelLengthRatio / modelLength);
    }

    /// <summary>배율 1일 때 모델의 가장 긴 변 (월드)</summary>
    private static float LongestSide(GameObject root)
    {
        var bounds = new Bounds();
        bool any = false;
        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null)
                continue;

            Bounds b = mf.sharedMesh.bounds;
            var world = new Bounds(mf.transform.TransformPoint(b.center), Vector3.Scale(b.size, mf.transform.lossyScale));
            if (!any) { bounds = world; any = true; }
            else bounds.Encapsulate(world);
        }

        return any ? Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)) : 0f;
    }
}
