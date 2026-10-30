using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 게임 중 일시 정지 메뉴 (Esc): 계속하기 / 설정 / 타이틀로.
///  - 열려 있는 동안 시간을 멈추고(Time.timeScale = 0) 조작을 잠근다. 닫으면 원래대로 돌린다.
///  - 설정: 설정 창(SettingsWindow)을 위에 띄운다. 설정 창을 닫으면 이 메뉴로 돌아온다.
///  - 타이틀로: 확인 창("마지막 저장 이후의 진행은 사라집니다")을 거쳐 타이틀로 돌아간다(GameLaunch.ReturnToTitle).
/// Esc를 다른 곳이 쓰고 있으면 열지 않는다: 스토리 재생(건너뛰기), 항성계 지도, 정거장 창, 설정 창, 조작 잠금(프롤로그·이름 입력·도킹 연출),
/// 워프, 시간이 멈춘 동안(패배 창·개발자 창). 같은 프레임에 그 창이 Esc로 닫히며 이 메뉴까지 열리지 않도록 '지난 프레임'의 상태로 판단한다.
/// 플레이어 함선이 있는 게임 씬(MainScene)을 불러올 때 자동으로 생긴다. 모양은 Assets/Resources/UI/PauseMenu.prefab.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    private const string PrefabPath = "UI/PauseMenu";

    [SerializeField] private GameObject _panel;
    [SerializeField] private Button _resume;
    [SerializeField] private Button _settings;
    [SerializeField] private Button _title;

    [Header("타이틀로 확인 창")]
    [SerializeField] private GameObject _confirm;
    [SerializeField] private Button _confirmYes;
    [SerializeField] private Button _confirmNo;

    /// <summary>일시 정지 메뉴가 열려 있는지</summary>
    public static bool IsOpen => _instance != null && _instance._panel.activeSelf;

    private static PauseMenu _instance;
    private float _savedTimeScale = 1f;
    private bool _blockedLastFrame = true;
    private StarMapUI[] _maps = new StarMapUI[0];
    private StationInteriorView _station;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 플레이어 함선이 있는 게임 씬에서만 (씬과 함께 사라진다)
        if (FindObjectOfType<SpaceshipController>() == null)
            return;

        var prefab = Resources.Load<PauseMenu>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[PauseMenu] Assets/Resources/{PrefabPath}.prefab을 찾지 못해 일시 정지 메뉴 없이 시작합니다.");
            return;
        }

        Instantiate(prefab);
    }

    private void Awake()
    {
        _instance = this;

        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            GameFonts.ApplyKorean(text);

        _resume.onClick.AddListener(Close);
        _settings.onClick.AddListener(() => SettingsWindow.Open());
        _title.onClick.AddListener(() => _confirm.SetActive(true));
        _confirmYes.onClick.AddListener(ReturnToTitle);
        _confirmNo.onClick.AddListener(() => _confirm.SetActive(false));

        _panel.SetActive(false);
        _confirm.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
        if (_panel.activeSelf)
        {
            Time.timeScale = _savedTimeScale;
            GameInputLock.Unlock(this);
        }
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        if (_panel.activeSelf)
        {
            // 위에 뜬 설정 창은 스스로 Esc로 닫힌다. 확인 창이 떠 있으면 그것만 닫는다
            if (SettingsWindow.Current != null || _blockedLastFrame)
                return;
            if (_confirm.activeSelf)
                _confirm.SetActive(false);
            else
                Close();
        }
        else if (!_blockedLastFrame)
        {
            Open();
        }
    }

    private void LateUpdate()
    {
        _blockedLastFrame = _panel.activeSelf ? SettingsWindow.Current != null : IsEscapeBusy();
    }

    /// <summary>Esc를 다른 곳이 쓰고 있거나 지금 멈출 수 없는 상태인지</summary>
    private bool IsEscapeBusy()
    {
        if (StoryPlayer.IsPlaying || GameInputLock.IsLocked || StarSystemWarp.IsWarping
            || SettingsWindow.Current != null || Time.timeScale <= 0f)
            return true;

        if (_maps.Length == 0 || _maps[0] == null)
            _maps = FindObjectsOfType<StarMapUI>(true);
        foreach (StarMapUI map in _maps)
        {
            if (map != null && map.IsOpen)
                return true;
        }

        if (_station == null)
            _station = FindObjectOfType<StationInteriorView>(true);
        return _station != null && _station.AnyWindowOpen;
    }

    public void Open()
    {
        if (_panel.activeSelf)
            return;

        _savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        GameInputLock.Lock(this);
        _confirm.SetActive(false);
        _panel.SetActive(true);
    }

    public void Close()
    {
        if (!_panel.activeSelf)
            return;

        _panel.SetActive(false);
        _confirm.SetActive(false);
        Time.timeScale = _savedTimeScale;
        GameInputLock.Unlock(this);
    }

    private void ReturnToTitle()
    {
        _panel.SetActive(false);
        GameInputLock.Unlock(this);
        GameLaunch.ReturnToTitle();
    }
}
