using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면(TitleScene)의 메뉴: 새 게임 / 이어 하기 / 설정 / 게임 종료.
///  - 새 게임: 저장이 있으면 "기존 저장이 지워집니다" 확인 창을 띄운 뒤 메인 씬으로 간다(GameLaunch.StartNewGame).
///    저장은 메인 씬에서 이름을 정할 때 지운다.
///  - 이어 하기: 저장이 없으면 꺼져 있다. 누르면 저장한 정거장 안에서 시작한다(GameLaunch.ContinueGame).
///  - 설정: 설정 창(SettingsWindow)을 띄운다.
///  - 게임 종료: 게임을 끈다(에디터에서는 Play를 멈춘다).
/// 확인 창이 떠 있으면 Esc로 닫는다. UI는 TitleScene의 Canvas에 놓인 SCI-FI UI Pack 버튼·팝업이다.
/// </summary>
public class TitleMenu : MonoBehaviour
{
    [Header("메뉴")]
    [SerializeField] private Button _newGame;
    [SerializeField] private Button _continue;
    [SerializeField] private Button _settings;
    [SerializeField] private Button _quit;

    [Header("새 게임 확인 창 (저장이 있을 때)")]
    [SerializeField] private GameObject _confirmPopup;
    [SerializeField] private Button _confirmYes;
    [SerializeField] private Button _confirmNo;

    private void Awake()
    {
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            GameFonts.ApplyKorean(text);

        _newGame.onClick.AddListener(HandleNewGame);
        _continue.onClick.AddListener(GameLaunch.ContinueGame);
        _settings.onClick.AddListener(() => SettingsWindow.Open());
        _quit.onClick.AddListener(GameLaunch.Quit);
        _confirmYes.onClick.AddListener(GameLaunch.StartNewGame);
        _confirmNo.onClick.AddListener(() => _confirmPopup.SetActive(false));

        SetAvailable(_continue, SaveSystem.HasSave);
        _confirmPopup.SetActive(false);
    }

    /// <summary>버튼을 켜고 끈다. UI 팩 버튼은 꺼진 모습(스프라이트)이 따로 없으므로 꺼지면 흐리게 한다.</summary>
    private static void SetAvailable(Button button, bool available)
    {
        button.interactable = available;
        CanvasGroup group = button.GetComponent<CanvasGroup>();
        if (group == null)
            group = button.gameObject.AddComponent<CanvasGroup>();
        group.alpha = available ? 1f : 0.4f;
    }

    private void Update()
    {
        if (_confirmPopup.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            _confirmPopup.SetActive(false);
    }

    private void HandleNewGame()
    {
        if (SaveSystem.HasSave)
            _confirmPopup.SetActive(true);
        else
            GameLaunch.StartNewGame();
    }
}
