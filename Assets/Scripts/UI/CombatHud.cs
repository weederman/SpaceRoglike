using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 HUD: 화면 아래 무기 슬롯 표시(1/2… 키, 선택된 무기 강조)와 지정한 목표의 마커(거리·방위각·쉴드/선체 게이지),
/// 화면 위쪽 가운데의 경고 표시(ShowAlert, 예: 튜토리얼 전투의 "비상 프로토콜 발동").
/// 무기 선택(PlayerWeaponSelector)과 목표 지정(TargetingSystem)도 같은 프리팹에 붙어 있어 함께 생성된다.
/// 게임 시작 시 자동 생성(씬에 배치하지 않음), 씬을 다시 불러와도 유지된다.
/// 조작이 잠긴 스토리 재생 중에는 숨고, 조작하며 듣는 스토리(control 시퀀스) 중에는 보인다.
/// 모양은 Assets/Resources/UI/CombatHud.prefab (SCI-FI UI Pack Btn_WeaponSelect / CircularProgress / Linearprogress 기반).
/// </summary>
public class CombatHud : MonoBehaviour
{
    private const string PrefabPath = "UI/CombatHud";

    [System.Serializable]
    private class SlotView
    {
        public CanvasGroup group;
        public TMP_Text key;
        public TMP_Text weaponName;
    }

    [SerializeField] private CanvasGroup _root;
    [SerializeField] private PlayerWeaponSelector _weapons;
    [SerializeField] private TargetingSystem _targeting;

    [Header("무기 슬롯 (왼쪽부터 1, 2, …)")]
    [SerializeField] private SlotView[] _slots = new SlotView[0];
    [SerializeField, Range(0f, 1f)] private float _unselectedAlpha = 0.35f;

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

        _weapons.OnChanged += RefreshSlots;
        _marker.gameObject.SetActive(false);
        _alert.gameObject.SetActive(false);
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
        if (_weapons != null)
            _weapons.OnChanged -= RefreshSlots;
        if (_instance == this)
            _instance = null;
    }

    private void LateUpdate()
    {
        if (_player == null)
            _player = FindObjectOfType<SpaceshipController>();

        bool storyLocked = StoryPlayer.IsPlaying && !StoryPlayer.IsControlMode;
        _root.alpha = _player != null && !storyLocked ? 1f : 0f;

        UpdateMarker();
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

    private void RefreshSlots()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            SlotView view = _slots[i];
            bool exists = i < _weapons.SlotCount;
            view.group.gameObject.SetActive(exists);
            if (!exists)
                continue;

            view.key.text = (i + 1).ToString();
            view.weaponName.text = WeaponStatsTable.GetDisplayName(_weapons.GetWeapon(i));
            view.group.alpha = i == _weapons.SelectedIndex ? 1f : _unselectedAlpha;
        }
    }

    private void UpdateMarker()
    {
        HullHealth target = _targeting.Current;
        Camera cam = Camera.main;

        bool show = target != null && cam != null && _player != null;
        if (_marker.gameObject.activeSelf != show)
            _marker.gameObject.SetActive(show);
        if (!show)
            return;

        // 화면 위치 (카메라 뒤나 화면 밖이면 가장자리에 붙인다). 선체 판정 자식이 아니라 함선 중심을 따른다.
        Rigidbody ship = target.GetComponentInParent<Rigidbody>();
        Vector3 center = ship != null ? ship.transform.position : target.transform.position;
        Vector3 screen = cam.WorldToScreenPoint(center);
        if (screen.z < 0f)
            screen = new Vector3(Screen.width - screen.x, Screen.height - screen.y, 0f);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out Vector2 local))
        {
            // 캔버스 좌표로 가장자리에 붙인다. 아래쪽은 마커 밑의 정보 글자·게이지까지 보이도록 그만큼 더 띄운다.
            Vector2 half = _canvasRect.rect.size * 0.5f;
            local.x = Mathf.Clamp(local.x, -half.x + _edgeMargin, half.x - _edgeMargin);
            local.y = Mathf.Clamp(local.y, -half.y + _edgeMargin + _markerBelowHeight, half.y - _edgeMargin);
            _marker.anchoredPosition = local;
        }

        // 거리와 방위각 (XZ 평면, 북쪽 = +Z = 0°, 시계 방향)
        Vector3 offset = center - _player.transform.position;
        offset.y = 0f;
        float bearing = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
        if (bearing < 0f)
            bearing += 360f;

        _markerInfo.text = string.Format(_infoFormat, offset.magnitude, bearing);

        ShieldHealth shield = _targeting.CurrentShield;
        _shieldBar.value = shield != null && shield.MaxHp > 0f ? shield.CurrentHp / shield.MaxHp : 0f;
        _hullBar.value = target.MaxHp > 0f ? target.CurrentHp / target.MaxHp : 0f;
    }
}
