using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정 창 (탭 두 개). 값을 바꾸면 바로 적용·저장된다(GameSettings).
///  - 그래픽: 해상도(◀ ▶로 목록을 넘긴다, 목록은 SettingsConfig), 전체 화면/창 모드 스위치
///  - 오디오: 전체 / 음악 / 효과음 / 인터페이스 음량 슬라이더 (0~100%)
/// Open()으로 어디서든 띄운다(타이틀의 '설정' 버튼 등). 자기 캔버스를 갖고 다른 UI 위에 뜬다. 떠 있는 동안 조작을 잠근다.
/// 닫기 버튼이나 Esc로 닫는다. 모양은 Assets/Resources/UI/SettingsWindow.prefab (정거장 창과 같은 SCI-FI UI Pack 구성).
/// </summary>
public class SettingsWindow : MonoBehaviour
{
    private const string PrefabPath = "UI/SettingsWindow";

    [Serializable]
    private class VolumeRow
    {
        public VolumeChannel channel;
        public Slider slider;
        public TMP_Text value;
    }

    [Header("창")]
    [SerializeField] private Button _close;
    [SerializeField] private Button _graphicsTab;
    [SerializeField] private Button _audioTab;
    [SerializeField] private Sprite _tabOn;
    [SerializeField] private Sprite _tabOff;
    [SerializeField] private GameObject _graphicsPage;
    [SerializeField] private GameObject _audioPage;

    [Header("그래픽")]
    [SerializeField] private TMP_Text _resolutionLabel;
    [SerializeField] private Button _previousResolution;
    [SerializeField] private Button _nextResolution;
    [SerializeField] private Toggle _fullscreen;
    [SerializeField] private TMP_Text _fullscreenLabel;
    [SerializeField] private string _fullscreenText = "전체 화면";
    [SerializeField] private string _windowedText = "창 모드";
    [Tooltip("목록에 없는 지금 해상도를 보일 때. {0} 가로, {1} 세로")]
    [SerializeField] private string _customResolutionFormat = "{0} × {1}";

    [Header("오디오")]
    [SerializeField] private VolumeRow[] _volumes = new VolumeRow[0];

    /// <summary>지금 떠 있는 설정 창 (없으면 null)</summary>
    public static SettingsWindow Current { get; private set; }

    /// <summary>창이 닫혔을 때</summary>
    public event Action Closed;

    private bool _refreshing;

    /// <summary>설정 창을 띄운다 (이미 떠 있으면 그것을 돌려준다)</summary>
    public static SettingsWindow Open()
    {
        if (Current != null)
            return Current;

        var prefab = Resources.Load<SettingsWindow>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[Settings] Assets/Resources/{PrefabPath}.prefab을 찾지 못했습니다.");
            return null;
        }

        return Instantiate(prefab);
    }

    private void Awake()
    {
        Current = this;
        GameInputLock.Lock(this);

        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            GameFonts.ApplyKorean(text);

        _close.onClick.AddListener(Close);
        _graphicsTab.onClick.AddListener(() => ShowPage(true));
        _audioTab.onClick.AddListener(() => ShowPage(false));
        _previousResolution.onClick.AddListener(() => StepResolution(-1));
        _nextResolution.onClick.AddListener(() => StepResolution(1));
        _fullscreen.onValueChanged.AddListener(HandleFullscreen);

        foreach (VolumeRow row in _volumes)
        {
            VolumeRow r = row;
            r.slider.minValue = 0f;
            r.slider.maxValue = 1f;
            r.slider.onValueChanged.AddListener(v => HandleVolume(r, v));
        }

        ShowPage(true);
        Refresh();
    }

    private void OnDestroy()
    {
        GameInputLock.Unlock(this);
        if (Current == this)
            Current = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    public void Close()
    {
        Closed?.Invoke();
        Destroy(gameObject);
    }

    private void ShowPage(bool graphics)
    {
        _graphicsPage.SetActive(graphics);
        _audioPage.SetActive(!graphics);
        ((Image)_graphicsTab.targetGraphic).sprite = graphics ? _tabOn : _tabOff;
        ((Image)_audioTab.targetGraphic).sprite = graphics ? _tabOff : _tabOn;
    }

    /// <summary>지금 설정 값을 화면에 옮긴다 (값을 바꾸는 이벤트는 내지 않는다)</summary>
    private void Refresh()
    {
        _refreshing = true;

        var list = GameSettings.Resolutions;
        int index = GameSettings.ResolutionIndex;
        Vector2Int current = GameSettings.Resolution;
        _resolutionLabel.text = index >= 0 ? list[index].Label : string.Format(_customResolutionFormat, current.x, current.y);
        _previousResolution.interactable = list.Count > 0;
        _nextResolution.interactable = list.Count > 0;

        _fullscreen.isOn = GameSettings.Fullscreen;
        _fullscreenLabel.text = GameSettings.Fullscreen ? _fullscreenText : _windowedText;

        foreach (VolumeRow row in _volumes)
        {
            float volume = GameSettings.GetVolume(row.channel);
            row.slider.value = volume;
            row.value.text = Percent(volume);
        }

        _refreshing = false;
    }

    private void StepResolution(int step)
    {
        var list = GameSettings.Resolutions;
        if (list.Count == 0)
            return;

        // 목록에 없는 해상도였으면 ▶는 첫 항목, ◀는 마지막 항목부터
        int index = GameSettings.ResolutionIndex;
        index = index < 0 ? (step > 0 ? 0 : list.Count - 1) : (index + step + list.Count) % list.Count;
        GameSettings.SetResolution(list[index].width, list[index].height);
        Refresh();
    }

    private void HandleFullscreen(bool on)
    {
        if (_refreshing)
            return;

        GameSettings.SetFullscreen(on);
        Refresh();
    }

    private void HandleVolume(VolumeRow row, float volume)
    {
        row.value.text = Percent(volume);
        if (!_refreshing)
            GameSettings.SetVolume(row.channel, volume);
    }

    private static string Percent(float volume)
    {
        return $"{Mathf.RoundToInt(volume * 100f)}%";
    }
}
