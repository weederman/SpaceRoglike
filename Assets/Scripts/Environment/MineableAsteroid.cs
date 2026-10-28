using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 채굴할 수 있는 광맥 소행성. 장식용 AsteroidField와 별개의 오브젝트로, 연출 담당(예: MiningTutorialDirector)이 만들고 Init으로 모양을 정한다.
/// AsteroidField의 암석처럼 스프라이트를 카메라를 향해 세운 빌보드다. 광맥이 드러나면 암석이 보랏빛을 띠고,
/// 뒤에 깔린 같은 모양의 조금 큰 겹이 테두리로 빛나며, 보랏빛 조명이 주변 함선을 비춘다.
/// 광맥은 Reveal()로 드러난다(처음엔 평범한 암석). 채굴 장비(MiningLaser)가 Extract로 광물을 꺼내 간다.
/// 목표 지정(TargetingSystem)의 대상이 된다(All 목록).
/// </summary>
public class MineableAsteroid : MonoBehaviour
{
    /// <summary>지금 남아 있는 광맥 소행성 전부 (목표 지정 후보)</summary>
    public static readonly List<MineableAsteroid> All = new List<MineableAsteroid>();

    [SerializeField] private Color _veinColor = new Color(0.72f, 0.35f, 1f, 1f);
    [SerializeField, Min(0.05f)] private float _revealSeconds = 1.6f;
    [SerializeField, Min(0f)] private float _lightRange = 160f;

    private SpriteRenderer _rock;
    private SpriteRenderer _vein;
    private Light _light;
    private Camera _camera;

    private float _size;
    private float _ore;
    private float _oreMax;
    private float _roll;
    private float _reveal;
    private bool _revealing;
    private float _miningGlow; // 채굴 빔이 닿는 동안 광맥이 더 밝아진다

    public float Radius => _size * 0.5f;
    public float Remaining => _ore;
    public float RemainingRatio => _oreMax > 0f ? _ore / _oreMax : 0f;
    public bool HasOre => _ore > 0f;
    public bool IsRevealed => _reveal >= 1f;

    /// <summary>모양(스프라이트, 긴 변 기준 월드 크기)과 광물 매장량을 정한다.</summary>
    public void Init(Sprite sprite, float size, float ore)
    {
        _size = size;
        _ore = _oreMax = ore;
        _roll = Random.value * 360f;

        _rock = CreateLayer("Rock", sprite, -18, Color.white);
        _vein = CreateLayer("Vein", sprite, -19, new Color(_veinColor.r, _veinColor.g, _veinColor.b, 0f));
        _vein.transform.localScale = Vector3.one * 1.1f; // 암석 뒤에서 테두리만 빛나 보인다

        float longSide = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        transform.localScale = Vector3.one * (size / longSide);

        var lightGo = new GameObject("VeinLight");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0f, -0.5f); // 카메라 쪽으로 조금
        _light = lightGo.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = _veinColor;
        _light.range = _lightRange;
        _light.intensity = 0f;
    }

    private SpriteRenderer CreateLayer(string name, Sprite sprite, int order, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order; // 함선보다 항상 뒤
        renderer.color = color;
        return renderer;
    }

    private void OnEnable()
    {
        All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
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
        if (_rock == null)
            return;

        if (_camera == null)
            _camera = Camera.main;

        // AsteroidField 암석처럼 카메라를 향해 서서 천천히 돈다
        Quaternion facing = _camera != null ? _camera.transform.rotation : Quaternion.identity;
        transform.rotation = facing * Quaternion.Euler(0f, 0f, _roll + Time.time * 2f);

        if (_revealing && _reveal < 1f)
            _reveal = Mathf.Min(1f, _reveal + Time.deltaTime / _revealSeconds);

        _miningGlow = Mathf.MoveTowards(_miningGlow, 0f, Time.deltaTime * 2f);

        // 광맥은 천천히 맥동하고, 다 캐면 빛이 사그라든다
        float pulse = 0.55f + 0.2f * Mathf.Sin(Time.time * 2.2f);
        float strength = Mathf.SmoothStep(0f, 1f, _reveal) * Mathf.Lerp(0.25f, 1f, RemainingRatio);
        Color c = _veinColor;
        c.a = Mathf.Clamp01((pulse + _miningGlow * 0.35f) * strength);
        _vein.color = c;
        _rock.color = Color.Lerp(Color.white, Color.Lerp(Color.white, _veinColor, 0.45f), strength);
        _light.intensity = (2.5f + _miningGlow * 3f) * strength;
    }
}
