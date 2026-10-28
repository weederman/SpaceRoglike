using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 안내 창. 대사 줄의 cue로 열고 닫는다.
///   tutorial_show:안내id   Assets/Resources/Story/*.json의 "tutorials"에 정의한 안내를 띄운다
///   tutorial_hide          닫는다
/// 안내는 제목 + (그림) + "이름: 설명" 항목 여러 개로 구성한다 (예: 출항 전 조작 안내).
/// 그림은 안내의 "image"에 Assets/Resources/Story/Tutorials/ 안의 Sprite 이름을 적으면 제목 아래에 뜨고, 창이 그만큼 아래로 늘어난다.
/// 건너뛰기(Esc)로 지나가는 cue는 페이드 없이 바로 처리된다. 시간은 unscaledTime 기준이다.
/// 모양은 Assets/Resources/UI/TutorialTips.prefab (SCI-FI UI Pack Popup 기반).
/// </summary>
public class TutorialTips : MonoBehaviour
{
    private const string ShowPrefix = "tutorial_show:";
    private const string HideCue = "tutorial_hide";
    private const string ImageFolder = "Story/Tutorials/";

    [SerializeField] private CanvasGroup _group;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _body;
    [SerializeField, Min(0f)] private float _fadeSeconds = 0.4f;

    [Header("창 크기 · 그림 (선택)")]
    [Tooltip("창 전체. 위쪽 기준(pivot y = 1)이어야 그림·본문 길이에 맞춰 아래로 늘고 줄어든다.")]
    [SerializeField] private RectTransform _panel;
    [SerializeField] private Image _image;
    [Tooltip("그림 칸의 높이. 그림이 있으면 본문이 이만큼(+간격) 아래로 내려간다.")]
    [SerializeField, Min(0f)] private float _imageHeight = 360f;
    [SerializeField, Min(0f)] private float _imageSpacing = 16f;
    [Tooltip("본문 아래 여백. 창 높이 = 본문 위치 + 본문 글자 높이 + 이 여백")]
    [SerializeField, Min(0f)] private float _bottomPadding = 44f;

    [Tooltip("항목 이름에 입히는 색 (#RRGGBB)")]
    [SerializeField] private string _tipTitleColor = "#FFD166";

    private Vector3 _baseScale;
    private Coroutine _fade;
    // 창 위쪽 가장자리에서 본문 윗변까지의 거리 (그림이 없을 때)
    private float _bodyTop;

    private void Awake()
    {
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            GameFonts.ApplyKorean(text);

        // 본문을 위쪽 기준으로 바꿔 두면 높이만 바꿔도 윗변이 그대로다
        RectTransform body = _body.rectTransform;
        _bodyTop = -(body.anchoredPosition.y + body.sizeDelta.y * (1f - body.pivot.y));
        body.pivot = new Vector2(body.pivot.x, 1f);

        _baseScale = _group.transform.localScale;
        _group.alpha = 0f;
        _group.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        StoryPlayer.OnCue += HandleCue;
    }

    private void OnDisable()
    {
        StoryPlayer.OnCue -= HandleCue;
    }

    private void HandleCue(string cue)
    {
        if (cue.StartsWith(ShowPrefix))
            Show(cue.Substring(ShowPrefix.Length).Trim());
        else if (cue == HideCue)
            Hide();
    }

    public void Show(string tutorialId)
    {
        if (!StoryDatabase.TryGetTutorial(tutorialId, out StoryTutorial tutorial))
        {
            Debug.LogWarning($"[Story] 튜토리얼 안내 '{tutorialId}'를 찾지 못했습니다. (Assets/Resources/Story/*.json의 tutorials)");
            return;
        }

        _title.text = tutorial.title;
        _body.text = BuildBody(tutorial);
        Layout(tutorial.image);
        StartFade(true, StoryPlayer.IsSkipping);
    }

    /// <summary>그림 유무와 본문 길이에 맞춰 본문 위치와 창 높이를 정한다.</summary>
    private void Layout(string imageName)
    {
        Sprite sprite = null;
        if (!string.IsNullOrEmpty(imageName))
        {
            sprite = Resources.Load<Sprite>(ImageFolder + imageName);
            if (sprite == null)
                Debug.LogWarning($"[Story] 튜토리얼 그림 'Assets/Resources/{ImageFolder}{imageName}'을 찾지 못했습니다. (Sprite로 임포트됐는지 확인)");
        }

        bool hasImage = sprite != null && _image != null && _panel != null;
        float extra = hasImage ? _imageHeight + _imageSpacing : 0f;

        if (_image != null)
        {
            _image.gameObject.SetActive(hasImage);
            _image.sprite = sprite;
        }

        RectTransform body = _body.rectTransform;
        float bodyHeight = _body.GetPreferredValues(_body.text, body.rect.width, 0f).y;
        body.anchoredPosition = new Vector2(body.anchoredPosition.x, -(_bodyTop + extra));
        body.sizeDelta = new Vector2(body.sizeDelta.x, bodyHeight);

        if (_panel != null)
            _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, _bodyTop + extra + bodyHeight + _bottomPadding);
    }

    public void Hide()
    {
        if (_group.gameObject.activeSelf)
            StartFade(false, StoryPlayer.IsSkipping);
    }

    private string BuildBody(StoryTutorial tutorial)
    {
        var sb = new StringBuilder();

        foreach (StoryTip tip in tutorial.tips)
        {
            if (sb.Length > 0)
                sb.Append('\n');

            sb.Append("<color=").Append(_tipTitleColor).Append(">").Append(tip.title).Append("</color>: ").Append(tip.text);
        }

        return sb.ToString();
    }

    private void StartFade(bool show, bool instant)
    {
        if (_fade != null)
            StopCoroutine(_fade);

        _fade = StartCoroutine(Fade(show, instant ? 0f : _fadeSeconds));
    }

    private IEnumerator Fade(bool show, float seconds)
    {
        _group.gameObject.SetActive(true);

        float from = _group.alpha;
        float to = show ? 1f : 0f;

        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / seconds);
            _group.alpha = Mathf.Lerp(from, to, k);
            _group.transform.localScale = _baseScale * (show ? Mathf.Lerp(0.92f, 1f, k) : 1f);
            yield return null;
        }

        _group.alpha = to;
        _group.transform.localScale = _baseScale;
        _group.gameObject.SetActive(show);
        _fade = null;
    }
}
