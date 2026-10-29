using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 함선의 채굴 장비. 광맥 소행성(MineableAsteroid)을 목표로 지정하고 사거리 안에서 채굴 키(기본 F)를 누르고 있으면
/// 채굴 빔이 광맥에 고정되어 광물을 꺼내 화물칸(CargoHold)에 싣는다. 광석 파편이 함선 쪽으로 빨려 들어오는 연출이 함께 나온다.
/// 지정한 목표가 광맥이 아니면, 키를 누를 때 사거리 안에서 가장 가까운 광맥을 자동으로 지정한다.
/// 빔이 처음 고정될 때마다 스토리에 신호 "mining_locked"를 보낸다.
/// 소리: 빔이 켜질 때 / 채굴하는 동안 반복 / 꺼질 때 (SoundTable의 mining_start / mining_loop / mining_stop)
/// 조작이 잠겨 있거나 화물칸이 가득 차거나 광맥이 바닥나면 멈춘다.
/// CombatHud 프리팹(Assets/Resources/UI/CombatHud.prefab)에 붙어 함께 생성된다.
/// </summary>
public class MiningLaser : MonoBehaviour
{
    public const string LockedSignal = "mining_locked";

    // SoundTable 이름
    private const string StartSound = "mining_start";
    private const string LoopSound = "mining_loop";
    private const string StopSound = "mining_stop";

    public static MiningLaser Instance { get; private set; }

    [SerializeField] private KeyCode _key = KeyCode.F;
    [Tooltip("광맥 소행성 가장자리까지의 최대 거리")]
    [SerializeField, Min(1f)] private float _range = 170f;
    [Tooltip("초당 꺼내는 광물의 양")]
    [SerializeField, Min(0.01f)] private float _orePerSecond = 3f;

    [Header("연출")]
    [SerializeField] private Color _beamColor = new Color(0.78f, 0.5f, 1f, 1f);
    [SerializeField, Min(0.1f)] private float _beamWidth = 3f;
    [Tooltip("함선에서 빔이 나오는 위치 (함선 기준 앞쪽 거리)")]
    [SerializeField] private float _muzzleForward = 14f;
    [Tooltip("빨려 들어오는 광석 파편 모양 (2D Space Kit Debris 등)")]
    [SerializeField] private Sprite[] _fragmentSprites = new Sprite[0];
    [SerializeField, Min(0f)] private float _fragmentsPerSecond = 12f;
    [SerializeField] private Vector2 _fragmentSize = new Vector2(10f, 18f);

    private sealed class Fragment
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public Vector3 from;
        public float t;
        public float duration;
        public float size;
        public float spin;
        public bool active;
    }

    /// <summary>지금 빔이 광맥에 고정되어 채굴 중인지</summary>
    public bool IsMining { get; private set; }

    public float Range => _range;
    public KeyCode Key => _key;

    private SpaceshipController _ship;
    private Transform _fxRoot; // 빔/파편 같은 월드 연출은 HUD 캔버스(배율이 다르다) 밖에 둔다
    private Material _material;
    private LineRenderer _beam;
    private Light _contactLight;
    private readonly List<Fragment> _fragments = new List<Fragment>();
    private float _fragmentTimer;
    private Camera _camera;

    private void Awake()
    {
        Instance = this;

        _material = new Material(Shader.Find("Sprites/Default"));

        _fxRoot = new GameObject("MiningLaserFX").transform;
        DontDestroyOnLoad(_fxRoot.gameObject);

        var beamGo = new GameObject("MiningBeam");
        beamGo.transform.SetParent(_fxRoot, false);
        _beam = beamGo.AddComponent<LineRenderer>();
        _beam.useWorldSpace = true;
        _beam.positionCount = 2;
        _beam.sharedMaterial = _material;
        _beam.numCapVertices = 4;
        _beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _beam.enabled = false;

        var lightGo = new GameObject("MiningContactLight");
        lightGo.transform.SetParent(_fxRoot, false);
        _contactLight = lightGo.AddComponent<Light>();
        _contactLight.type = LightType.Point;
        _contactLight.color = _beamColor;
        _contactLight.range = 90f;
        _contactLight.enabled = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        GameAudio.StopLoop(this);

        if (_material != null)
            Destroy(_material);

        if (_fxRoot != null)
            Destroy(_fxRoot.gameObject);
    }

    /// <summary>이 광맥이 채굴 사거리 안에 있는지</summary>
    public bool InRange(MineableAsteroid ore)
    {
        if (ore == null || !EnsureShip())
            return false;

        return HorizontalDistance(_ship.transform.position, ore.transform.position) - ore.Radius <= _range;
    }

    private void Update()
    {
        MineableAsteroid ore = null;
        bool wantMining = !GameInputLock.IsLocked && Input.GetKey(_key) && EnsureShip();

        if (wantMining)
        {
            ore = PickOre();
            wantMining = ore != null && InRange(ore) && ore.HasOre
                && CargoHold.Instance != null && !CargoHold.Instance.IsFull;
        }

        if (wantMining && !IsMining)
        {
            StoryPlayer.Signal(LockedSignal);
            GameAudio.Play(StartSound);
            GameAudio.StartLoop(LoopSound, this);
        }
        else if (!wantMining && IsMining)
        {
            GameAudio.StopLoop(this);
            GameAudio.Play(StopSound);
        }

        IsMining = wantMining;

        if (IsMining)
        {
            // 채굴 장비 개량을 달았으면 그만큼 빨리 캔다
            float rate = _orePerSecond * (1f + PlayerEquipment.Bonus(EquipmentCatalog.Kind.Mining));
            float taken = ore.Extract(rate * Time.deltaTime);
            CargoHold.Instance.Add(ore.OreId, taken);
        }

        UpdateBeam(ore);
        UpdateFragments(ore);
    }

    /// <summary>지정한 광맥, 없으면 사거리 안에서 가장 가까운 광맥을 지정해 돌려준다.</summary>
    private MineableAsteroid PickOre()
    {
        TargetingSystem targeting = TargetingSystem.Instance;
        if (targeting != null && targeting.CurrentOre != null)
            return targeting.CurrentOre;

        MineableAsteroid nearest = null;
        float best = float.PositiveInfinity;
        foreach (MineableAsteroid ore in MineableAsteroid.All)
        {
            if (!ore.HasOre || !InRange(ore))
                continue;

            float d = HorizontalDistance(_ship.transform.position, ore.transform.position);
            if (d < best)
            {
                best = d;
                nearest = ore;
            }
        }

        if (nearest != null && targeting != null)
            targeting.Select(nearest);

        return nearest;
    }

    // ── 연출 ──────────────────────────────────────────

    private Vector3 Muzzle => _ship.transform.position + _ship.transform.forward * _muzzleForward;

    /// <summary>빔이 닿는 광맥 표면 (함선 쪽 가장자리 조금 안쪽)</summary>
    private Vector3 ContactPoint(MineableAsteroid ore)
    {
        Vector3 toShip = _ship.transform.position - ore.transform.position;
        toShip.y = 0f;
        return ore.transform.position + toShip.normalized * (ore.Radius * 0.55f);
    }

    private void UpdateBeam(MineableAsteroid ore)
    {
        _beam.enabled = IsMining;
        _contactLight.enabled = IsMining;
        if (!IsMining)
            return;

        Vector3 contact = ContactPoint(ore);
        _beam.SetPosition(0, Muzzle);
        _beam.SetPosition(1, contact);

        // 빔이 가늘게 떨리며 맥동한다
        float flicker = 0.75f + 0.25f * Mathf.Sin(Time.time * 37f) * Mathf.Sin(Time.time * 13f);
        _beam.widthMultiplier = _beamWidth * flicker;
        Color c = _beamColor;
        c.a = 0.85f;
        _beam.startColor = c;
        c.a = 0.55f;
        _beam.endColor = c;

        _contactLight.transform.position = contact + Vector3.up * 10f;
        _contactLight.intensity = 3f * flicker;
    }

    private void UpdateFragments(MineableAsteroid ore)
    {
        if (_camera == null)
            _camera = Camera.main;

        if (IsMining && _fragmentSprites.Length > 0)
        {
            _fragmentTimer += Time.deltaTime * _fragmentsPerSecond;
            while (_fragmentTimer >= 1f)
            {
                _fragmentTimer -= 1f;
                SpawnFragment(ore);
            }
        }

        Quaternion facing = _camera != null ? _camera.transform.rotation : Quaternion.identity;
        Vector3 target = _ship != null ? _ship.transform.position : Vector3.zero;

        foreach (Fragment f in _fragments)
        {
            if (!f.active)
                continue;

            f.t += Time.deltaTime / f.duration;
            if (f.t >= 1f || _ship == null)
            {
                f.active = false;
                f.renderer.enabled = false;
                continue;
            }

            // 처음엔 느리게 떨어져 나오다가 함선 쪽으로 빨려 들어간다
            float k = Mathf.Pow(f.t, 1.6f);
            Vector3 side = Vector3.Cross(target - f.from, Vector3.up).normalized * Mathf.Sin(f.t * Mathf.PI) * f.size * 2f;
            f.transform.position = Vector3.Lerp(f.from, target, k) + side;
            f.transform.rotation = facing * Quaternion.Euler(0f, 0f, f.spin * f.t);
            f.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, k) * f.size;
        }
    }

    private void SpawnFragment(MineableAsteroid ore)
    {
        Fragment f = null;
        foreach (Fragment candidate in _fragments)
        {
            if (!candidate.active)
            {
                f = candidate;
                break;
            }
        }

        if (f == null)
        {
            var go = new GameObject("OreFragment");
            go.transform.SetParent(_fxRoot, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = -16;
            f = new Fragment { transform = go.transform, renderer = renderer };
            _fragments.Add(f);
        }

        Sprite sprite = _fragmentSprites[Random.Range(0, _fragmentSprites.Length)];
        float longSide = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);

        Vector2 jitter = Random.insideUnitCircle * ore.Radius * 0.35f;
        f.from = ContactPoint(ore) + new Vector3(jitter.x, 0f, jitter.y);
        f.t = 0f;
        f.duration = Random.Range(0.7f, 1.1f);
        f.size = Random.Range(_fragmentSize.x, _fragmentSize.y) / longSide;
        f.spin = Random.Range(-360f, 360f);
        f.active = true;
        f.renderer.sprite = sprite;
        f.renderer.color = Color.Lerp(Color.white, _beamColor, Random.Range(0.15f, 0.45f)); // 보랏빛 광맥 위에서도 보이게 밝게
        f.renderer.enabled = true;
        f.transform.position = f.from;
    }

    private bool EnsureShip()
    {
        if (_ship == null)
            _ship = FindObjectOfType<SpaceshipController>();

        return _ship != null;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
