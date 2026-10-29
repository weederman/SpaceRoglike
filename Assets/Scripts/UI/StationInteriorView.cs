using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 정거장 내부(도킹 중) 화면. 처음에는 왼쪽 사이드바만 보인다:
///   정거장 상징·이름, 그리고 격납고 / 스페이스 라운지 / 지도 / 미션 / 정보 버튼.
/// 버튼을 누르면 가운데에 그 창이 열린다 (한 번에 하나, 닫기 버튼이나 Esc로 닫는다). 지도는 항성계 지도(StarMapUI)를 연다.
/// 스페이스 라운지는 임무를 받는 곳, 미션은 받은 임무를 보는 곳이다 (둘 다 MissionBoardWindow, 모드만 다르다).
/// 창이 모두 닫혀 있을 때만 F로 출항할 수 있다 (StationDocking이 AnyWindowOpen을 본다).
/// 화면 아래 가운데의 안내(입항·출항 키)와, 도킹/출항 때 쓰는 화면 전체 페이드도 여기에 있다.
/// 프리팹: Assets/Resources/UI/StationInterior.prefab
/// </summary>
public class StationInteriorView : MonoBehaviour
{
    [Header("사이드바")]
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_Text _stationName;
    [SerializeField] private TMP_Text _stationSub;
    [SerializeField] private Button _hangarButton;
    [SerializeField] private Button _loungeButton;
    [SerializeField] private Button _mapButton;
    [SerializeField] private Button _missionButton;
    [SerializeField] private Button _infoButton;
    [SerializeField] private Sprite _buttonOn;
    [SerializeField] private Sprite _buttonOff;

    [Header("창")]
    [SerializeField] private HangarWindow _hangar;
    [SerializeField] private MissionBoardWindow _lounge;
    [SerializeField] private MissionBoardWindow _missions;
    [SerializeField] private StationInfoWindow _info;

    [Header("안내 / 페이드")]
    [SerializeField] private GameObject _promptRoot;
    [SerializeField] private TMP_Text _prompt;
    [SerializeField] private Image _fade;

    private StationWindow[] _windows;
    private StationZone _zone;
    private StarMapUI _map;

    public bool IsVisible => _root.activeSelf;

    /// <summary>가운데 창이나 지도가 열려 있는지</summary>
    public bool AnyWindowOpen
    {
        get
        {
            foreach (StationWindow window in _windows)
            {
                if (window.IsOpen)
                    return true;
            }

            return Map != null && Map.IsOpen;
        }
    }

    private StarMapUI Map
    {
        get
        {
            if (_map == null)
                _map = FindObjectOfType<StarMapUI>();
            return _map;
        }
    }

    private void Awake()
    {
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            GameFonts.ApplyKorean(text);

        _windows = new StationWindow[] { _hangar, _lounge, _missions, _info };
        foreach (StationWindow window in _windows)
        {
            window.CloseRequested += w => CloseWindows();
            window.gameObject.SetActive(false);
        }

        _hangarButton.onClick.AddListener(() => Toggle(_hangar));
        _loungeButton.onClick.AddListener(() => Toggle(_lounge));
        _missionButton.onClick.AddListener(() => Toggle(_missions));
        _infoButton.onClick.AddListener(() => Toggle(_info));
        _mapButton.onClick.AddListener(OpenMap);
        _lounge.Accepted += CloseWindows;
        _missions.Accepted += CloseWindows;

        SetFade(0f);
        ShowPrompt(null);
    }

    private void Update()
    {
        if (!IsVisible)
            return;

        // Esc: 열린 창을 닫는다 (지도는 스스로 Esc로 닫힌다)
        if (Input.GetKeyDown(KeyCode.Escape) && !StoryPlayer.IsPlaying)
            CloseWindows();

        // 대사가 시작되면(임무 수락 등) 창을 닫는다
        if (StoryPlayer.IsPlaying && !StoryPlayer.IsControlMode)
            CloseWindows();

        RefreshButtons();
    }

    public void SetVisible(bool visible)
    {
        if (!visible)
            CloseWindows();
        _root.SetActive(visible);
    }

    /// <summary>도킹한 정거장을 사이드바에 표시한다</summary>
    public void SetStation(StationZone zone)
    {
        _zone = zone;
        _stationName.text = zone != null && !string.IsNullOrEmpty(zone.DisplayName) ? zone.DisplayName : "정거장";

        StarSystem system = StarSystem.Instance;
        string sub = zone != null && zone.IsHome ? "모항" : "우주정거장";
        if (system != null && zone != null && zone.StationIndex >= 0 && zone.StationIndex < system.Data.stations.Length)
            sub = $"{system.Data.orbits[system.Data.stations[zone.StationIndex].orbit].name} 궤도 · {sub}";
        _stationSub.text = sub;
    }

    public void CloseWindows()
    {
        foreach (StationWindow window in _windows)
            window.Close();
    }

    private void Toggle(StationWindow window)
    {
        bool wasOpen = window.IsOpen;
        CloseWindows();
        if (Map != null && Map.IsOpen)
            Map.Close();

        if (!wasOpen)
            window.Open(_zone);
    }

    private void OpenMap()
    {
        CloseWindows();
        if (Map != null && !Map.IsOpen)
            Map.Open();
    }

    private void RefreshButtons()
    {
        SetOn(_hangarButton, _hangar.IsOpen);
        SetOn(_loungeButton, _lounge.IsOpen);
        SetOn(_missionButton, _missions.IsOpen);
        SetOn(_infoButton, _info.IsOpen);
        SetOn(_mapButton, Map != null && Map.IsOpen);
    }

    private void SetOn(Button button, bool on)
    {
        var image = (Image)button.targetGraphic;
        Sprite sprite = on ? _buttonOn : _buttonOff;
        if (image.sprite != sprite)
            image.sprite = sprite;
    }

    /// <summary>화면 아래 가운데 안내 (예: "F  출항"). null이면 숨긴다.</summary>
    public void ShowPrompt(string text)
    {
        bool show = !string.IsNullOrEmpty(text);
        if (_promptRoot.activeSelf != show)
            _promptRoot.SetActive(show);
        if (show && _prompt.text != text)
            _prompt.text = text;
    }

    /// <summary>0 = 투명, 1 = 화면 전체가 검게</summary>
    public void SetFade(float alpha)
    {
        Color c = _fade.color;
        c.a = alpha;
        _fade.color = c;
        _fade.gameObject.SetActive(alpha > 0.001f);
    }
}
