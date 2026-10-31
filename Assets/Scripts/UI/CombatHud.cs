using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 HUD: 지정한 목표의 마커(거리·방위각·쉴드/선체 게이지,
/// 광맥이면 남은 광물과 채굴 사거리), 화물칸 게이지, 목적지 표시(ShowWaypoint), 화면 위쪽 가운데의 경고 표시(ShowAlert).
/// 무기 슬롯(PlayerWeaponSelector), 목표 지정(TargetingSystem), 채굴 장비(MiningLaser), 화물칸(CargoHold)도
/// 같은 프리팹에 붙어 있어 함께 생성된다.
/// 게임 시작 시 자동 생성(씬에 배치하지 않음), 씬을 다시 불러와도 유지된다.
/// 조작이 잠긴 스토리 재생 중과 정거장 안(도킹 중)에는 숨고, 조작하며 듣는 스토리(control 시퀀스) 중에는 보인다.
/// 모양은 Assets/Resources/UI/CombatHud.prefab (SCI-FI UI Pack CircularProgress / Linearprogress 기반).
/// </summary>
public class CombatHud : MonoBehaviour
{
    private const string PrefabPath = "UI/CombatHud";

    [SerializeField] private CanvasGroup _root;
    [SerializeField] private TargetingSystem _targeting;
    [SerializeField] private MiningLaser _mining;
    [SerializeField] private CargoHold _cargoHold;

    [Header("목표 마커")]
    [SerializeField] private RectTransform _marker;
    [SerializeField] private TMP_Text _markerInfo;
    [SerializeField] private Slider _shieldBar;
    [SerializeField] private Slider _hullBar;
    [Tooltip("목표 정보 문구. {0} 거리, {1} 방위각(북쪽=0°, 시계 방향)")]
    [SerializeField] private string _infoFormat = "거리 {0:0}  방위 {1:000}°";
    [Tooltip("화면 밖 목표를 가장자리에 붙일 때의 여백 (캔버스 기준 1920x1080 단위)")]
    [SerializeField, Min(0f)] private float _edgeMargin = 80f;
    [Tooltip("마커 중심 아래로 정보 글자·게이지가 차지하는 높이. 화면 아래 가장자리에서 이만큼 더 띄운다.")]
    [SerializeField, Min(0f)] private float _markerBelowHeight = 80f;
    [Tooltip("광맥 목표의 정보 문구. {0} 거리, {1} 방위각, {2} 사거리 안/밖 문구")]
    [SerializeField] private string _oreInfoFormat = "광맥  거리 {0:0}  {2}";
    [SerializeField] private string _oreInRangeText = "<color=#7CFFB2>채굴 가능</color>";
    [SerializeField] private string _oreOutOfRangeText = "<color=#FF8A7A>사거리 밖</color>";
    [Tooltip("광맥 목표일 때 아래 게이지(남은 광물) 색")]
    [SerializeField] private Color _oreBarColor = new Color(0.75f, 0.45f, 1f, 1f);

    [Header("화물칸")]
    [SerializeField] private CanvasGroup _cargo;
    [SerializeField] private Slider _cargoBar;
    [SerializeField] private TMP_Text _cargoText;
    [Tooltip("{0} 실린 양, {1} 적재 한도, {2} 비율(%)")]
    [SerializeField] private string _cargoFormat = "화물 · 광물 {0:0} / {1:0}";

    [Header("목적지 표시")]
    [SerializeField] private RectTransform _waypoint;
    [SerializeField] private TMP_Text _waypointText;
    [Tooltip("{0} 이름, {1} 거리")]
    [SerializeField] private string _waypointFormat = "{0}  {1:0}";

    [Header("경고 (화면 위쪽 가운데)")]
    [SerializeField] private CanvasGroup _alert;
    [SerializeField] private TMP_Text _alertTitle;
    [SerializeField] private TMP_Text _alertBody;
    [Tooltip("경고가 깜빡이는 주기(초)")]
    [SerializeField, Min(0.1f)] private float _alertPulseSeconds = 1.2f;

    private static CombatHud _instance;
    private RectTransform _canvasRect;
    private SpaceshipController _player;
    private float _alertShownAt;
    private Image _hullFill;
    private Color _hullFillColor;
    private Transform _waypointTarget;
    private string _waypointLabel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null)
            return;

        var prefab = Resources.Load<CombatHud>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[CombatHud] Assets/Resources/{PrefabPath}.prefab을 찾지 못해 무기 선택/목표 지정을 쓸 수 없습니다.");
            return;
        }

        _instance = Instantiate(prefab);
        DontDestroyOnLoad(_instance.gameObject);
    }

    private void Awake()
    {
        _canvasRect = (RectTransform)transform;

        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            GameFonts.ApplyKorean(text);

        _marker.gameObject.SetActive(false);
        _alert.gameObject.SetActive(false);
        _waypoint.gameObject.SetActive(false);

        _hullFill = _hullBar.fillRect != null ? _hullBar.fillRect.GetComponent<Image>() : null;
        if (_hullFill != null)
            _hullFillColor = _hullFill.color;
    }

    /// <summary>목적지 표시를 띄운다 (화면 밖이면 가장자리에 붙어 방향을 알려 준다). 대상이 사라지면 저절로 숨는다.</summary>
    public static void ShowWaypoint(Transform target, string label)
    {
        if (_instance == null)
            return;

        _instance._waypointTarget = target;
        _instance._waypointLabel = label;
    }

    public static void HideWaypoint()
    {
        if (_instance != null)
            _instance._waypointTarget = null;
    }

    /// <summary>화면 위쪽 가운데에 경고를 띄운다. HideAlert를 부를 때까지 깜빡이며 남아 있다.</summary>
    public static void ShowAlert(string title, string body)
    {
        if (_instance == null)
            return;

        _instance._alertTitle.text = title;
        _instance._alertBody.text = body;
        _instance._alertShownAt = Time.unscaledTime;
        _instance._alert.gameObject.SetActive(true);
    }

    public static void HideAlert()
    {
        if (_instance != null)
            _instance._alert.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void LateUpdate()
    {
        if (_player == null)
            _player = FindObjectOfType<SpaceshipController>();

        // 잠긴 대사 중과 정거장 안(도킹 중)에는 숨긴다
        bool storyLocked = StoryPlayer.IsPlaying && !StoryPlayer.IsControlMode;
        _root.alpha = _player != null && !storyLocked && !StationDocking.IsDocked ? 1f : 0f;

        UpdateMarker();
        UpdateWaypoint();
        UpdateCargo();
        UpdateAlert();
    }

    private void UpdateAlert()
    {
        if (!_alert.gameObject.activeSelf)
            return;

        // 처음 0.6초는 크게 튀어나오고, 이후로는 천천히 깜빡인다
        float t = Time.unscaledTime - _alertShownAt;
        float pop = Mathf.Clamp01(t / 0.6f);
        _alert.transform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, 1f - (1f - pop) * (1f - pop));
        _alert.alpha = pop < 1f ? pop : Mathf.Lerp(0.55f, 1f, 0.5f + 0.5f * Mathf.Cos(t * Mathf.PI * 2f / _alertPulseSeconds));
    }

    private void UpdateMarker()
    {
        Component target = _targeting.Target;
        Camera cam = Camera.main;

        bool show = target != null && cam != null && _player != null;
        if (_marker.gameObject.activeSelf != show)
            _marker.gameObject.SetActive(show);
        if (!show)
            return;

        Vector3 center = TargetingSystem.PositionOf(target);
        PlaceOnScreen(_marker, center, cam, _markerBelowHeight);

        // 거리와 방위각 (XZ 평면, 북쪽 = +Z = 0°, 시계 방향)
        Vector3 offset = center - _player.transform.position;
        offset.y = 0f;
        float bearing = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
        if (bearing < 0f)
            bearing += 360f;

        if (target is MineableAsteroid ore)
        {
            // 광맥: 남은 광물 게이지 하나와 채굴 사거리 안/밖
            bool inRange = _mining != null && _mining.InRange(ore);
            _markerInfo.text = string.Format(_oreInfoFormat, offset.magnitude, bearing, inRange ? _oreInRangeText : _oreOutOfRangeText);
            _shieldBar.gameObject.SetActive(false);
            _hullBar.value = ore.RemainingRatio;
            SetHullFillColor(_oreBarColor);
            return;
        }

        var hull = (HullHealth)target;
        _markerInfo.text = string.Format(_infoFormat, offset.magnitude, bearing);
        _shieldBar.gameObject.SetActive(true);
        SetHullFillColor(_hullFillColor);

        ShieldHealth shield = _targeting.CurrentShield;
        _shieldBar.value = shield != null && shield.MaxHp > 0f ? shield.CurrentHp / shield.MaxHp : 0f;
        _hullBar.value = hull.MaxHp > 0f ? hull.CurrentHp / hull.MaxHp : 0f;
    }

    private void SetHullFillColor(Color color)
    {
        if (_hullFill != null && _hullFill.color != color)
            _hullFill.color = color;
    }

    private void UpdateWaypoint()
    {
        Camera cam = Camera.main;
        bool show = _waypointTarget != null && cam != null && _player != null;
        if (_waypoint.gameObject.activeSelf != show)
            _waypoint.gameObject.SetActive(show);
        if (!show)
            return;

        PlaceOnScreen(_waypoint, _waypointTarget.position, cam, 40f);

        Vector3 offset = _waypointTarget.position - _player.transform.position;
        offset.y = 0f;
        _waypointText.text = string.Format(_waypointFormat, _waypointLabel, offset.magnitude);
    }

    private void UpdateCargo()
    {
        // 실은 광물이 있거나 캘 수 있는 광맥이 있을 때만 보인다
        bool show = _cargoHold != null && (_cargoHold.Used > 0f || MineableAsteroid.All.Count > 0);
        _cargo.alpha = show ? 1f : 0f;
        if (!show)
            return;

        _cargoBar.value = _cargoHold.Ratio;
        _cargoText.text = string.Format(_cargoFormat, _cargoHold.Used, _cargoHold.Capacity, _cargoHold.Ratio * 100f);
    }

    /// <summary>월드 위치를 화면(캔버스) 위치로 옮긴다. 카메라 뒤나 화면 밖이면 가장자리에 붙인다.
    /// 아래쪽은 표시 밑에 붙는 글자·게이지까지 보이도록 belowHeight만큼 더 띄운다.</summary>
    private void PlaceOnScreen(RectTransform rect, Vector3 world, Camera cam, float belowHeight)
    {
        Vector3 screen = cam.WorldToScreenPoint(world);
        if (screen.z < 0f)
            screen = new Vector3(Screen.width - screen.x, Screen.height - screen.y, 0f);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out Vector2 local))
            return;

        Vector2 half = _canvasRect.rect.size * 0.5f;
        local.x = Mathf.Clamp(local.x, -half.x + _edgeMargin, half.x - _edgeMargin);
        local.y = Mathf.Clamp(local.y, -half.y + _edgeMargin + belowHeight, half.y - _edgeMargin);
        rect.anchoredPosition = local;
    }
}
