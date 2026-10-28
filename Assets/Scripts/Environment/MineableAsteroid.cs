using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 채굴할 수 있는 광맥 소행성. 장식용 AsteroidField와 별개의 오브젝트로, 연출 담당(예: MiningTutorialDirector)이 만들고 Init으로 모양을 정한다.
/// 3D 소행성 모델(FreeSciFi 용암 소행성 메시 + 발광 균열 재질)이며 천천히 구른다.
/// 균열 발광은 흑백 마스크(Assets/Materials/Asteroids/AsteroidVein_Mask.png, 원본 발광 텍스처를 흑백으로 바꾼 것)에 광맥 색을 곱해 낸다. 처음엔 균열이 꺼진 평범한 암석이고,
/// 광맥이 드러나면(Reveal) 균열이 보랏빛으로 빛나고 보랏빛 조명이 주변 함선을 비춘다. 채굴 빔이 닿는 동안 더 밝아지고, 다 캐면 사그라든다.
/// 채굴 장비(MiningLaser)가 Extract로 광물을 꺼내 간다. 목표 지정(TargetingSystem)의 대상이 된다(All 목록).
/// </summary>
public class MineableAsteroid : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    /// <summary>지금 남아 있는 광맥 소행성 전부 (목표 지정 후보)</summary>
    public static readonly List<MineableAsteroid> All = new List<MineableAsteroid>();

    [SerializeField] private Color _veinColor = new Color(0.72f, 0.35f, 1f, 1f);
    [Tooltip("드러난 광맥의 발광 세기 (HDR 배율)")]
    [SerializeField, Min(0f)] private float _veinIntensity = 3f;
    [SerializeField, Min(0.05f)] private float _revealSeconds = 1.6f;
    [SerializeField, Min(0f)] private float _lightRange = 160f;
    [Tooltip("구르는 속도 (도/초)")]
    [SerializeField, Min(0f)] private float _tumble = 4f;

    private Transform _model;
    private Material _material;
    private Light _light;
    private Color _baseColor = Color.white;

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

    /// <summary>모양(메시, 발광 균열이 있는 재질, 긴 쪽 기준 월드 크기)과 광물 매장량을 정한다.</summary>
    public void Init(Mesh mesh, Material material, float size, float ore)
    {
        _size = size;
        _ore = _oreMax = ore;

        var modelGo = new GameObject("Model");
        modelGo.transform.SetParent(transform, false);
        _model = modelGo.transform;
        modelGo.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = modelGo.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // 균열의 빛을 이 소행성만 바꿀 수 있게 재질을 복제한다
        _material = new Material(material);
        _material.EnableKeyword("_EMISSION");
        _material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        if (_material.HasProperty(ColorId))
            _baseColor = _material.GetColor(ColorId);
        renderer.sharedMaterial = _material;

        Vector3 b = mesh != null ? mesh.bounds.size : Vector3.one;
        float longSide = Mathf.Max(b.x, Mathf.Max(b.y, b.z));
        _model.localScale = Vector3.one * (size / Mathf.Max(0.001f, longSide));

        _baseRotation = Random.rotation;
        _spinAxis = Random.onUnitSphere;

        var lightGo = new GameObject("VeinLight");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = new Vector3(0f, size * 0.6f, 0f); // 위쪽(카메라 쪽)에서 주변을 비춘다
        _light = lightGo.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = _veinColor;
        _light.range = _lightRange;
        _light.intensity = 0f;

        UpdateLook();
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
        if (_material != null)
            Destroy(_material);
    }

    /// <summary>표면의 보랏빛 광맥이 드러난다. instant면 바로(건너뛰기).</summary>
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
        if (_model == null)
            return;

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

        _material.SetColor(EmissionColorId, _veinColor * (_veinIntensity * glow));
        if (_material.HasProperty(ColorId))
            _material.SetColor(ColorId, Color.Lerp(_baseColor, _baseColor * Color.Lerp(Color.white, _veinColor, 0.35f), strength));

        _light.intensity = (2.5f + _miningGlow * 3f) * strength;
    }
}
