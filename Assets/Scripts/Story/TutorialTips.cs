using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// 튜토리얼 안내 창. 대사 줄의 cue로 열고 닫는다.
///   tutorial_show:안내id   Assets/Resources/Story/*.json의 "tutorials"에 정의한 안내를 띄운다
///   tutorial_hide          닫는다
/// 안내는 제목 + "이름: 설명" 항목 여러 개로 구성한다 (예: 출항 전 조작 안내).
/// 건너뛰기(Esc)로 지나가는 cue는 페이드 없이 바로 처리된다. 시간은 unscaledTime 기준이다.
/// 모양은 Assets/Resources/UI/TutorialTips.prefab (SCI-FI UI Pack Popup 기반).
/// </summary>
public class TutorialTips : MonoBehaviour
{
    private const string ShowPrefix = "tutorial_show:";
    private const string HideCue = "tutorial_hide";

    [SerializeField] private CanvasGroup _group;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _body;
    [SerializeField, Min(0f)] private float _fadeSeconds = 0.4f;

    [Tooltip("항목 이름에 입히는 색 (#RRGGBB)")]
    [SerializeField] private string _tipTitleColor = "#FFD166";

    private Vector3 _baseScale;
    private Coroutine _fade;

    private void Awake()
    {
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            GameFonts.ApplyKorean(text);

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
        StartFade(true, StoryPlayer.IsSkipping);
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
