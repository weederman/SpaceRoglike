using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 화면 아래 나레이션바(대사창 + 선택지 목록). 이름표/본문(타자 효과)/"다음" 표시와 선택지 버튼을 그리기만 하고,
/// 무슨 줄을 언제 보여줄지, 어떤 입력이 무슨 뜻인지는 StoryPlayer가 정한다.
/// 모양은 Assets/Resources/UI/NarrationBar.prefab이 정한다(SCI-FI UI Pack Popup012_Cyan 기반 대사창,
/// Btn04Cyan 기반 선택지). 배치와 크기는 프리팹에서 바꾸면 되고, 아래 필드는 프리팹에서 연결해 둔 것이다.
/// 시간은 전부 unscaledTime 기준이라 게임이 일시정지(timeScale 0)여도 동작한다.
/// </summary>
public class NarrationBar : MonoBehaviour
{
    [Header("대사창")]
    [SerializeField] private CanvasGroup _panel;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _bodyText;
    [SerializeField] private Graphic _continueIcon;

    [Header("선택지")]
    [SerializeField] private RectTransform _choiceContainer;
    [SerializeField] private StoryChoiceItem _choiceItemPrefab;

    [Header("기타")]
    [Tooltip("스토리 재생 중 뒤의 UI(상점 등) 클릭을 막는 전체 화면 투명 막")]
    [SerializeField] private GameObject _blocker;
    [SerializeField, Min(1f)] private float _charsPerSecond = 45f;

    private const float FadeSeconds = 0.15f;

    private readonly List<StoryChoiceItem> _choiceItems = new List<StoryChoiceItem>();
    private int _visibleTotal;
    private float _shown;
    private bool _panelVisible;
    private bool _continueVisible;
    private bool _rightSide;
    private RectTransform _panelRect;

    /// <summary>선택지 칸을 클릭했을 때 (칸 번호)</summary>
    public event Action<int> ChoiceClicked;

    /// <summary>선택지 칸에 마우스를 올렸을 때 (칸 번호)</summary>
    public event Action<int> ChoiceHovered;

    public bool IsTyping { get; private set; }
    public bool IsChoosing => _choiceItems.Count > 0;

    private void Awake()
    {
        GameFonts.ApplyKorean(_nameText);
        GameFonts.ApplyKorean(_bodyText);

        _panelRect = (RectTransform)_panel.transform;
        _panel.alpha = 0f;
        _continueIcon.enabled = false;
        _blocker.SetActive(false);
        _choiceContainer.gameObject.SetActive(false);

        EnsureEventSystem();
    }

    private void Update()
    {
        // 패널 페이드
        float targetAlpha = _panelVisible ? 1f : 0f;
        if (!Mathf.Approximately(_panel.alpha, targetAlpha))
            _panel.alpha = Mathf.MoveTowards(_panel.alpha, targetAlpha, Time.unscaledDeltaTime / FadeSeconds);

        // 타자 효과 (TMP는 태그를 뺀 글자 수로 센다)
        if (IsTyping)
        {
            _shown += _charsPerSecond * Time.unscaledDeltaTime;
            int visible = Mathf.FloorToInt(_shown);

            if (visible >= _visibleTotal)
                CompleteTyping();
            else
                _bodyText.maxVisibleCharacters = visible;
        }

        // "다음" 표시 깜빡임
        if (_continueVisible)
        {
            Color c = _continueIcon.color;
            c.a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));
            _continueIcon.color = c;
        }
    }

    // ── StoryPlayer가 부르는 부분 ──────────────────────

    /// <summary>스토리 재생 동안 켜 둔다. 켜져 있으면 바 뒤의 UI(상점 등)가 클릭되지 않는다.</summary>
    public void SetBlocking(bool blocking)
    {
        _blocker.SetActive(blocking);
    }

    /// <summary>
    /// 대사 한 줄을 보여주고 타자 효과를 시작한다. speakerName이 비면 이름표 없는 나레이션.
    /// rightSide가 true면 화면 오른쪽(플레이어), false면 왼쪽(NPC)에 대사창을 띄운다.
    /// </summary>
    public void Show(string speakerName, Color nameColor, string text, bool rightSide = false)
    {
        SetSide(rightSide);

        bool hasName = !string.IsNullOrEmpty(speakerName);
        _nameText.gameObject.SetActive(hasName);
        _nameText.text = speakerName;
        _nameText.color = nameColor;

        _bodyText.text = text ?? "";
        _bodyText.maxVisibleCharacters = 0;
        _bodyText.ForceMeshUpdate();
        _visibleTotal = _bodyText.textInfo.characterCount;

        _shown = 0f;
        IsTyping = _visibleTotal > 0;
        if (!IsTyping)
            CompleteTyping();

        SetContinueVisible(false);
        _panelVisible = true;
    }

    /// <summary>대사창을 왼쪽/오른쪽 아래로 옮긴다. 프리팹에 배치해 둔 왼쪽 여백과 높이를 그대로 좌우 대칭으로 쓴다.</summary>
    private void SetSide(bool rightSide)
    {
        if (rightSide == _rightSide)
            return;

        _rightSide = rightSide;

        float x = _panelRect.anchoredPosition.x;
        float margin = Mathf.Abs(x);
        float side = rightSide ? 1f : 0f;

        _panelRect.anchorMin = new Vector2(side, 0f);
        _panelRect.anchorMax = new Vector2(side, 0f);
        _panelRect.pivot = new Vector2(side, 0f);
        _panelRect.anchoredPosition = new Vector2(rightSide ? -margin : margin, _panelRect.anchoredPosition.y);

        // 옮겨 가는 동안 깜빡이지 않게 새 자리에서 다시 페이드인한다
        if (_panelVisible)
            _panel.alpha = 0f;
    }

    /// <summary>타자 효과를 건너뛰고 문장을 전부 보여준다.</summary>
    public void CompleteTyping()
    {
        IsTyping = false;
        _bodyText.maxVisibleCharacters = int.MaxValue;
    }

    /// <summary>입력을 기다리는 중임을 "다음" 아이콘으로 알린다.</summary>
    public void SetContinueVisible(bool visible)
    {
        _continueVisible = visible;
        _continueIcon.enabled = visible;
    }

    /// <summary>선택지 목록을 띄운다. 첫 칸이 강조된 상태로 시작한다.</summary>
    public void ShowChoices(IReadOnlyList<string> texts)
    {
        HideChoices();
        SetContinueVisible(false);
        _choiceContainer.gameObject.SetActive(true);

        for (int i = 0; i < texts.Count; i++)
        {
            StoryChoiceItem item = Instantiate(_choiceItemPrefab, _choiceContainer);
            item.Bind(i, texts[i]);
            item.Clicked += index => ChoiceClicked?.Invoke(index);
            item.Hovered += index => ChoiceHovered?.Invoke(index);
            _choiceItems.Add(item);
        }

        HighlightChoice(0);
    }

    /// <summary>index번 선택지만 강조한다.</summary>
    public void HighlightChoice(int index)
    {
        for (int i = 0; i < _choiceItems.Count; i++)
            _choiceItems[i].SetHighlighted(i == index);
    }

    public void HideChoices()
    {
        foreach (StoryChoiceItem item in _choiceItems)
            Destroy(item.gameObject);

        _choiceItems.Clear();
        _choiceContainer.gameObject.SetActive(false);

        // 클릭으로 고른 버튼이 선택 상태로 남아 Enter가 한 번 더 눌리는 일이 없도록
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    /// <summary>바를 숨긴다(연출만 있는 구간).</summary>
    public void Hide()
    {
        _panelVisible = false;
        IsTyping = false;
        SetContinueVisible(false);
        HideChoices();
    }

    // 클릭 가능한 UI에는 EventSystem이 있어야 한다. 이 씬에는 이미 있지만, 없는 씬에서도 동작하도록 보장한다.
    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;

        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(go);
    }
}
