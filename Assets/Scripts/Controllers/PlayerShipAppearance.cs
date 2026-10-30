using System;
using UnityEngine;

/// <summary>
/// 플레이어 함선의 겉모습(모델)만 바꾼다. 터렛·쉴드·체력·충돌 판정 같은 게임 기능은 원래 함선 그대로 두고,
/// 원래 선체 메시를 숨긴 뒤 다른 모델을 자식으로 붙여 같은 자리에 보이게 한다.
///  - Fighter  : 보상으로 받은 소형 전투선 (SF Fighter). 프롤로그 이후의 플레이어 함선. 기본 모습.
///  - Freighter: 주인공의 원래 함선, UTI 화물선 (SF RF5). 프롤로그 동안만. 무장이 없는 배라 포탑은 숨긴다.
/// 모델과 크기는 Assets/Resources/Ships/PlayerShipLooks.asset 에서 정한다. 게임 씬이 시작되면 자동으로 기본 모습(Fighter)이 된다.
/// 함선을 바꾸면(PlayerShips) Fighter 자리에 그 함선의 모습(ShipCatalog의 look)을 쓴다(SetShip).
/// </summary>
public class PlayerShipAppearance : MonoBehaviour
{
    public enum Look
    {
        Fighter,
        Freighter
    }

    [Serializable]
    public class LookSettings
    {
        [Tooltip("격납고에 보이는 함선 이름")]
        public string displayName;
        [Tooltip("격납고에 보이는 함선 아이콘")]
        public Sprite icon;
        [Tooltip("격납고 '설명' 탭에 보이는 글")]
        [TextArea(2, 5)] public string description;
        public GameObject model;
        [Tooltip("모델의 앞뒤 길이: 함선 선체 판정(HullHitbox) 길이의 몇 배인지. 함선 프리팹 배율을 바꾸면 모습도 함께 커진다.")]
        [Min(0.05f)] public float hullRatio = 1f;
        [Tooltip("함선 기준 위치 보정")]
        public Vector3 offset;
        [Tooltip("모델의 앞이 함선의 앞(+Z)을 향하도록 돌리는 각도")]
        public Vector3 rotation;
    }

    private const string ConfigPath = "Ships/PlayerShipLooks";

    private PlayerShipLooks _config;
    private LookSettings _shipLook; // 지금 탄 함선의 모습 (SetShip 전이면 ShipLook이 PlayerShips의 지금 함선을 쓴다)
    private MeshRenderer _originalHull;
    private GameObject _model;
    private bool _applied;

    public Look Current { get; private set; }

    /// <summary>지금 모습의 설정 (이름·아이콘 등). 설정 에셋이 없으면 null.</summary>
    public LookSettings CurrentSettings
    {
        get
        {
            if (Current == Look.Fighter && ShipLook != null)
                return ShipLook;
            if (_config == null)
                _config = Resources.Load<PlayerShipLooks>(ConfigPath);
            return _config != null ? _config.Get(Current) : null;
        }
    }

    /// <summary>
    /// 지금 탄 함선의 모습. 함선을 바꾸기 전(PlayerShips.Apply가 아직 불리지 않은 새 게임)에도 PlayerShips의 지금 함선을 쓴다
    /// (예전 PlayerShipLooks의 '소형 전투선' 이름·설명이 격납고에 남지 않게). 모델이 없으면 null.
    /// </summary>
    private LookSettings ShipLook
    {
        get
        {
            LookSettings look = _shipLook ?? PlayerShips.Current?.look;
            return look != null && look.model != null ? look : null;
        }
    }

    /// <summary>탄 함선의 모습을 정한다. 지금 Fighter 모습이면(프롤로그의 화물선이 아니면) 바로 바꾼다.</summary>
    public void SetShip(LookSettings settings)
    {
        _shipLook = settings;
        if (!_applied || Current == Look.Fighter)
            Set(Look.Fighter);
    }

    /// <summary>그 함선의 겉모습 담당 (없으면 붙인다)</summary>
    public static PlayerShipAppearance For(Component ship)
    {
        var appearance = ship.GetComponent<PlayerShipAppearance>();
        return appearance != null ? appearance : ship.gameObject.AddComponent<PlayerShipAppearance>();
    }

    // 게임 씬이 시작되면 기본 모습으로 (프롤로그가 먼저 다른 모습을 정했으면 그대로 둔다)
    // 게임을 켤 때뿐 아니라 씬을 다시 불러올 때(패배 후 재시작 등)도 다시 만든다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        Bootstrap();
    }

    private static void Bootstrap()
    {
        var ship = FindObjectOfType<SpaceshipController>();
        if (ship == null)
            return;

        PlayerShipAppearance appearance = For(ship);
        if (!appearance._applied)
            appearance.Set(Look.Fighter);
    }

    public void Set(Look look)
    {
        if (_config == null)
            _config = Resources.Load<PlayerShipLooks>(ConfigPath);

        LookSettings settings = look == Look.Fighter && ShipLook != null
            ? ShipLook
            : _config != null ? _config.Get(look) : null;
        if (settings == null || settings.model == null)
        {
            Debug.LogWarning($"[PlayerShip] Assets/Resources/{ConfigPath}.asset에 {look} 모델이 없어 원래 모습으로 둡니다.");
            return;
        }

        _applied = true;
        Current = look;

        if (_model != null)
            Destroy(_model);

        // 원래 선체(코르벳 메시)는 숨긴다. 터렛 등 자식 오브젝트는 그대로 보인다.
        if (_originalHull == null)
            _originalHull = GetComponent<MeshRenderer>();
        if (_originalHull != null)
            _originalHull.enabled = false;

        SetTurretsVisible(look != Look.Freighter);

        _model = Instantiate(settings.model, transform);
        _model.name = $"Look_{look}";
        _model.transform.localPosition = settings.offset;
        _model.transform.localRotation = Quaternion.Euler(settings.rotation);
        _model.transform.localScale = Vector3.one;

        // 장식용 모델이라 판정은 원래 함선의 것을 쓴다
        foreach (Collider c in _model.GetComponentsInChildren<Collider>(true))
            Destroy(c);

        // 배율 1일 때의 월드 크기(함선 오브젝트 배율 포함)를 재서, 가장 긴 변이 선체 판정 길이 × hullRatio가 되게 한다
        Bounds bounds = LocalBounds(_model);
        float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (longest > 0.001f)
            _model.transform.localScale = Vector3.one * (HullLength() * settings.hullRatio / longest);
    }

    /// <summary>선체 판정(HullHealth의 충돌체)의 가장 긴 변 (월드). 판정이 없으면 원래 코르벳 크기 기준.</summary>
    private float HullLength()
    {
        HullHealth hull = GetComponentInChildren<HullHealth>();
        Collider col = hull != null ? hull.GetComponent<Collider>() : null;
        if (col == null)
            return 112f * transform.lossyScale.z;

        Vector3 size = col.bounds.size;
        return Mathf.Max(size.x, Mathf.Max(size.y, size.z));
    }

    private void SetTurretsVisible(bool visible)
    {
        foreach (FORGE3D.F3DTurret turret in GetComponentsInChildren<FORGE3D.F3DTurret>(true))
        {
            foreach (Renderer r in turret.GetComponentsInChildren<Renderer>(true))
                r.forceRenderingOff = !visible;
        }
    }

    private static Bounds LocalBounds(GameObject root)
    {
        var bounds = new Bounds();
        bool any = false;
        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null)
                continue;

            Bounds b = mf.sharedMesh.bounds;
            Vector3 size = Vector3.Scale(b.size, mf.transform.lossyScale);
            var world = new Bounds(mf.transform.TransformPoint(b.center), size);
            if (!any) { bounds = world; any = true; }
            else bounds.Encapsulate(world);
        }

        return bounds;
    }
}
