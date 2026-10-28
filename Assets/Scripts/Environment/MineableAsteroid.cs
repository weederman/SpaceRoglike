using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 채굴할 수 있는 희귀(광맥) 소행성. 장식용 AsteroidField와 별개의 오브젝트로, 연출 담당(예: MiningTutorialDirector)이 만들고 Init으로 모양을 정한다.
/// 모양은 FreeSciFi Planet Asteroids 모델(용암 소행성 파랑/빨강, Tinniuq) 중 하나이며 천천히 구른다.
/// 처음엔 발광이 꺼진 평범한 암석이고, 광맥이 드러나면(Reveal) 모델 본래의 발광(용암 균열)이 켜지며 광맥 색 조명이 주변을 비춘다.
/// 채굴 빔이 닿는 동안 더 밝아지고, 다 캐면 사그라든다. 발광이 없는 모델은 표면이 광맥 색을 띠고 조명만 켜진다.
/// 채굴 장비(MiningLaser)가 Extract로 광물을 꺼내 간다. 목표 지정(TargetingSystem)의 대상이 된다(All 목록).
/// </summary>
public class MineableAsteroid : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    /// <summary>희귀 소행성의 모양 하나와 그 광맥 빛깔</summary>
    [Serializable]
    public class Look
    {
        public GameObject model;
        [Tooltip("광맥이 드러났을 때 주변을 비추는 조명 색 (모델의 발광 색에 맞춘다)")]
        public Color glow = new Color(0.72f, 0.35f, 1f, 1f);
    }

    /// <summary>지금 남아 있는 광맥 소행성 전부 (목표 지정 후보)</summary>
    public static readonly List<MineableAsteroid> All = new List<MineableAsteroid>();

    [Tooltip("드러난 광맥의 발광 세기 (모델 본래 발광에 곱한다)")]
    [SerializeField, Min(0f)] private float _glowIntensity = 1.6f;
    [SerializeField, Min(0.05f)] private float _revealSeconds = 1.6f;
    [SerializeField, Min(0f)] private float _lightRange = 160f;
    [Tooltip("구르는 속도 (도/초)")]
    [SerializeField, Min(0f)] private float _tumble = 4f;

    private sealed class Surface
    {
        public Material material;
        public Color baseColor;
        public Color emission;  // 모델 본래의 발광 색 (없으면 검정)
    }

    private readonly List<Surface> _surfaces = new List<Surface>();
    private Transform _model;
    private Light _light;
    private Color _glow;

    private float _size;
    private float _ore;
    private float _oreMax;
    private Quaternion _baseRotation;
    private Vector3 _spinAxis;
    private float _reveal;
    private bool _revealing;
    private float _miningGlow; // 채굴 빔이 닿는 동안 광맥이 더 밝아진다

    public float Radius => _size * 0.5f;
    public float Remaining => _ore;
    public float RemainingRatio => _oreMax > 0f ? _ore / _oreMax : 0f;
    public bool HasOre => _ore > 0f;
    public bool IsRevealed => _reveal >= 1f;

    /// <summary>모양(모델과 광맥 빛깔, 긴 쪽 기준 월드 크기)과 광물 매장량을 정한다.</summary>
    public void Init(Look look, float size, float ore)
    {
        _size = size;
        _ore = _oreMax = ore;
        _glow = look != null ? look.glow : Color.white;

        if (look != null && look.model != null)
        {
            GameObject model = Instantiate(look.model, transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            _model = model.transform;

            // 장식용 판정은 쓰지 않는다 (채굴·목표 지정은 거리로 판정)
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true))
                Destroy(c);

            // 광맥 빛을 이 소행성만 바꿀 수 있게 재질을 복제한다 (renderer.materials가 복제본을 만든다)
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                foreach (Material m in r.materials)
                {
                    var surface = new Surface
                    {
                        material = m,
                        baseColor = m.HasProperty(ColorId) ? m.GetColor(ColorId) : Color.white,
                        emission = m.IsKeywordEnabled("_EMISSION") && m.HasProperty(EmissionColorId) ? m.GetColor(EmissionColorId) : Color.black,
                    };
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    _surfaces.Add(surface);
                }
            }

            float longest = LongestSide(model);
            if (longest > 0.001f)
                _model.localScale = Vector3.one * (size / longest);
        }

        _baseRotation = UnityEngine.Random.rotation;
        _spinAxis = UnityEngine.Random.onUnitSphere;

        var lightGo = new GameObject("VeinLight");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = new Vector3(0f, size * 0.6f, 0f); // 위쪽(카메라 쪽)에서 주변을 비춘다
        _light = lightGo.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = _glow;
        _light.range = _lightRange;
        _light.intensity = 0f;

        UpdateLook();
    }

    private static float LongestSide(GameObject root)
    {
        var bounds = new Bounds();
        bool any = false;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }

        return any ? Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)) : 0f;
    }

    private void OnEnable()
    {
        All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    private void OnDestroy()
    {
        foreach (Surface s in _surfaces)
        {
            if (s.material != null)
                Destroy(s.material);
        }
    }

    /// <summary>표면의 광맥이 드러난다. instant면 바로(건너뛰기).</summary>
    public void Reveal(bool instant)
    {
        _revealing = true;
        if (instant)
            _reveal = 1f;
    }

    /// <summary>광물을 최대 amount만큼 꺼내고 실제로 꺼낸 양을 돌려준다.</summary>
    public float Extract(float amount)
    {
        float taken = Mathf.Clamp(amount, 0f, _ore);
        _ore -= taken;
        _miningGlow = 1f;
        return taken;
    }

    private void LateUpdate()
    {
        if (_model != null)
            _model.localRotation = Quaternion.AngleAxis(Time.time * _tumble, _spinAxis) * _baseRotation;

        if (_revealing && _reveal < 1f)
            _reveal = Mathf.Min(1f, _reveal + Time.deltaTime / _revealSeconds);

        _miningGlow = Mathf.MoveTowards(_miningGlow, 0f, Time.deltaTime * 2f);
        UpdateLook();
    }

    private void UpdateLook()
    {
        // 광맥은 천천히 맥동하고, 다 캐면 빛이 사그라든다
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 2.2f);
        float strength = Mathf.SmoothStep(0f, 1f, _reveal) * Mathf.Lerp(0.25f, 1f, RemainingRatio);
        float glow = (pulse + _miningGlow * 0.6f) * strength;

        foreach (Surface s in _surfaces)
        {
            s.material.SetColor(EmissionColorId, s.emission * (_glowIntensity * glow));

            // 본래 발광이 없는 재질은 표면이 광맥 색을 살짝 띤다
            if (s.emission.maxColorComponent < 0.01f && s.material.HasProperty(ColorId))
                s.material.SetColor(ColorId, Color.Lerp(s.baseColor, s.baseColor * Color.Lerp(Color.white, _glow, 0.5f), strength));
        }

        if (_light != null)
            _light.intensity = (2.5f + _miningGlow * 3f) * strength;
    }
}
