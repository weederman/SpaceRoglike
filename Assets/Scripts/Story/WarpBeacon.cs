using TMPro;
using UnityEngine;

/// <summary>
/// 워프 비콘: 월드에 떠 있는 발광 코어 + 회전하는 링 3개, 그리고 화면에 따라다니는 표시("워프 비콘 / 클릭하여 선택").
/// 스토리 줄이 waitFor "beacon_selected"로 기다리는 동안 마우스로 비콘을 클릭하면 선택되고, StoryPlayer에 신호를 보낸다.
/// 모양은 코드가 만든다(별도 에셋 없음). 표시용 UI(RectTransform/텍스트)는 연출 담당 프리팹의 캔버스에서 받는다.
/// </summary>
public class WarpBeacon : MonoBehaviour
{
    public const string SelectedSignal = "beacon_selected";

    [SerializeField] private Color _color = new Color(0.35f, 0.85f, 1f, 1f);
    [Tooltip("클릭으로 인정하는 반경(월드 단위). 코어보다 넉넉하게.")]
    [SerializeField, Min(1f)] private float _clickRadius = 40f;
    [SerializeField, Min(0.1f)] private float _appearSeconds = 0.9f;
    [Tooltip("표시를 비콘 머리 위에 띄우는 높이(화면 픽셀)")]
    [SerializeField] private float _markerLift = 95f;

    private RectTransform _marker;
    private CanvasGroup _markerGroup;
    private TMP_Text _hint;
    private RectTransform _canvasRect;
    private Camera _cam;

    private Transform _core;
    private Light _light;
    private Material _material;
    private LineRenderer[] _rings;
    private Transform[] _ringPivots;
    private readonly float[] _spin = { 28f, -45f, 18f };

    private float _appear;
    private bool _appearing;
    private bool _selected;
    private bool _hover;
    private float _charge; // 0 → 1: 워프 충전 중이면 링이 빨리 돌고 밝아진다
    private float _selectableSince = -1f;

    public bool IsSelected => _selected;

    /// <summary>링과 빛의 색을 바꾼다 (항성계 비콘의 레벨 표시 등)</summary>
    public void SetColor(Color color)
    {
        _color = color;
        if (_light != null)
            _light.color = color;
    }

    /// <summary>화면 표시용 UI를 연결한다. Show()로 나타나기 시작한다.</summary>
    public void Init(RectTransform marker, TMP_Text hint, RectTransform canvasRect)
    {
        _marker = marker;
        _markerGroup = marker.GetComponent<CanvasGroup>();
        _hint = hint;
        _canvasRect = canvasRect;
        _cam = Camera.main;
    }

    private void Awake()
    {
        _material = new Material(Shader.Find("Sprites/Default"));
        BuildVisuals();
        transform.localScale = Vector3.zero;
    }

    private void OnDestroy()
    {
        if (_material != null)
            Destroy(_material);

        if (_marker != null)
            _marker.gameObject.SetActive(false);
    }

    public void Show()
    {
        _appearing = true;
        if (_marker != null)
            _marker.gameObject.SetActive(true);
    }

    /// <summary>건너뛰기: 선택된 상태로 바로 만든다 (신호는 보내지 않는다).</summary>
    public void SelectInstant()
    {
        _appear = 1f;
        _appearing = true;
        _selected = true;
        _charge = Mathf.Max(_charge, 0.2f);
        RefreshHint();
    }

    /// <summary>워프 충전 연출 진행도(0~1). 링이 빨라지고 빛이 세진다.</summary>
    public void SetCharge(float value)
    {
        _charge = Mathf.Clamp01(value);
    }

    private void BuildVisuals()
    {
        // 발광 코어
        var coreGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        coreGo.name = "Core";
        Destroy(coreGo.GetComponent<Collider>());
        coreGo.transform.SetParent(transform, false);
        coreGo.transform.localScale = Vector3.one * 8f;
        coreGo.GetComponent<MeshRenderer>().sharedMaterial = _material;
        _core = coreGo.transform;

        // 회전하는 링 3개 (반지름, 기울기)
        _rings = new LineRenderer[3];
        _ringPivots = new Transform[3];
        float[] radius = { 17f, 25f, 34f };
        float[] tilt = { 0f, 68f, 24f };

        for (int i = 0; i < 3; i++)
        {
            var pivot = new GameObject($"RingPivot{i}").transform;
            pivot.SetParent(transform, false);
            _ringPivots[i] = pivot;

            var ringGo = new GameObject($"Ring{i}");
            ringGo.transform.SetParent(pivot, false);
            ringGo.transform.localRotation = Quaternion.Euler(tilt[i], 0f, 0f);

            var line = ringGo.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 72;
            line.widthMultiplier = 1.8f;
            line.sharedMaterial = _material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            for (int p = 0; p < line.positionCount; p++)
            {
                float a = p / (float)line.positionCount * Mathf.PI * 2f;
                line.SetPosition(p, new Vector3(Mathf.Cos(a) * radius[i], 0f, Mathf.Sin(a) * radius[i]));
            }

            _rings[i] = line;
        }

        var lightGo = new GameObject("Light");
        lightGo.transform.SetParent(transform, false);
        _light = lightGo.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = _color;
        _light.range = 140f;
        _light.intensity = 0f;
    }

    private void Update()
    {
        if (_appearing)
            _appear = Mathf.MoveTowards(_appear, 1f, Time.deltaTime / _appearSeconds);

        float ease = Mathf.SmoothStep(0f, 1f, _appear);
        transform.localScale = Vector3.one * ease;

        // 링 회전 (충전 중엔 빨라진다)
        float speed = 1f + _charge * 5f + (_selected ? 1.2f : 0f);
        for (int i = 0; i < _ringPivots.Length; i++)
            _ringPivots[i].Rotate(0f, _spin[i] * speed * Time.deltaTime, 0f, Space.Self);

        UpdateSelection();
        UpdateLook(ease);
        UpdateMarker(ease);
    }

    private void UpdateSelection()
    {
        // 대사가 이 비콘을 기다리는 동안에만 고를 수 있다 (다 나타난 뒤)
        bool selectable = !_selected && _appear >= 1f && StoryPlayer.WaitingFor == SelectedSignal;

        if (selectable && _selectableSince < 0f)
            _selectableSince = Time.unscaledTime;
        if (!selectable)
            _selectableSince = -1f;

        _hover = false;
        if (!selectable || _cam == null)
            return;

        Plane plane = new Plane(Vector3.up, transform.position);
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hit = ray.GetPoint(enter);
            _hover = (hit - transform.position).sqrMagnitude <= _clickRadius * _clickRadius;
        }

        // 대사를 넘기려고 누른 클릭이 곧바로 선택이 되지 않도록 잠깐 기다린다
        if (_hover && Input.GetMouseButtonDown(0) && Time.unscaledTime - _selectableSince > 0.35f)
        {
            _selected = true;
            StoryPlayer.Signal(SelectedSignal);
            RefreshHint();
        }
    }

    private void UpdateLook(float ease)
    {
        Color c = _color;
        if (_selected) c = Color.Lerp(_color, Color.white, 0.55f);
        else if (_hover) c = Color.Lerp(_color, Color.white, 0.35f);

        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 3f);
        c.a = pulse * ease;
        _material.color = c;

        _light.intensity = (2.2f + _charge * 5f) * ease * pulse;
        _core.localScale = Vector3.one * (8f + Mathf.Sin(Time.time * 4f) * 0.7f + _charge * 6f);
    }

    private void UpdateMarker(float ease)
    {
        if (_marker == null || _cam == null)
            return;

        Vector3 screen = _cam.WorldToScreenPoint(transform.position);
        bool visible = screen.z > 0f;
        _marker.gameObject.SetActive(visible && _appearing);
        if (!visible)
            return;

        // 화면(픽셀) 위치 → 캔버스 좌표. 위로 조금 띄운다.
        screen.y += _markerLift;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out Vector2 local))
            _marker.anchoredPosition = local;

        if (_markerGroup != null)
            _markerGroup.alpha = ease;

        RefreshHint();
    }

    private void RefreshHint()
    {
        if (_hint == null)
            return;

        if (_selected)
            _hint.text = "선택됨";
        else if (StoryPlayer.WaitingFor == SelectedSignal && _appear >= 1f)
            _hint.text = _hover ? "클릭하여 선택" : "비콘을 클릭하세요";
        else
            _hint.text = "";
    }
}
