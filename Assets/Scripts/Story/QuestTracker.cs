using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// 임무 수락 연출 + HUD 목표 창. 스토리 줄의 cue로 움직인다.
///   quest_accept:퀘스트id          화면 가운데에 "임무 수락" 창이 뜬 뒤, 왼쪽 위 HUD 구석으로 날아가 목표 창이 된다
///   quest_done:퀘스트id:목표id     그 목표를 완료 표시(취소선)한다
/// 퀘스트의 제목과 목표 문장은 Assets/Resources/Story/*.json의 "quests"에 둔다.
/// 건너뛰기(Esc)로 지나가는 cue는 연출 없이 바로 구석의 목표 창이 된다.
/// 모양은 Assets/Resources/UI/QuestTracker.prefab (SCI-FI UI Pack Popup 기반).
/// </summary>
public class QuestTracker : MonoBehaviour
{
    private const string AcceptPrefix = "quest_accept:";
    private const string DonePrefix = "quest_done:";

    [SerializeField] private RectTransform _panel;
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _body;

    [Header("임무 수락 연출 (가운데 → 왼쪽 위 구석)")]
    [SerializeField] private Vector2 _centerPosition = new Vector2(0f, 60f);
    [SerializeField] private float _centerScale = 1f;
    [SerializeField] private Vector2 _cornerPosition = new Vector2(30f, -110f);
    [SerializeField] private float _cornerScale = 0.55f;
    [SerializeField, Min(0f)] private float _fadeInSeconds = 0.5f;
    [SerializeField, Min(0f)] private float _holdSeconds = 3.2f;
    [SerializeField, Min(0f)] private float _moveSeconds = 0.8f;

    private StoryQuest _quest;
    private readonly HashSet<string> _done = new HashSet<string>();
    private Coroutine _routine;

    private void Awake()
    {
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            GameFonts.ApplyKorean(text);

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
        if (cue.StartsWith(AcceptPrefix))
        {
            Accept(cue.Substring(AcceptPrefix.Length).Trim());
        }
        else if (cue.StartsWith(DonePrefix))
        {
            string[] parts = cue.Substring(DonePrefix.Length).Split(':');
            if (parts.Length == 2)
                Complete(parts[0].Trim(), parts[1].Trim());
        }
    }

    public void Accept(string questId)
    {
        if (!StoryDatabase.TryGetQuest(questId, out StoryQuest quest))
        {
            Debug.LogWarning($"[Story] 퀘스트 '{questId}'를 찾지 못했습니다. (Assets/Resources/Story/*.json의 quests)");
            return;
        }

        _quest = quest;
        _done.Clear();
        RefreshBody();

        if (_routine != null)
            StopCoroutine(_routine);

        _group.gameObject.SetActive(true);

        if (StoryPlayer.IsSkipping)
        {
            _title.text = quest.title;
            ApplyLayout(1f);
            _group.alpha = 1f;
            _routine = null;
        }
        else
        {
            _routine = StartCoroutine(AcceptRoutine());
        }
    }

    public void Complete(string questId, string objectiveId)
    {
        if (_quest == null || _quest.id != questId)
            return;

        _done.Add(objectiveId);
        RefreshBody();
    }

    private IEnumerator AcceptRoutine()
    {
        // 1) 화면 가운데에 "임무 수락"으로 나타난다
        _title.text = $"임무 수락: {_quest.title}";
        ApplyLayout(0f);

        for (float t = 0f; t < _fadeInSeconds; t += Time.unscaledDeltaTime)
        {
            _group.alpha = t / _fadeInSeconds;
            yield return null;
        }

        _group.alpha = 1f;
        yield return new WaitForSecondsRealtime(_holdSeconds);

        // 2) 왼쪽 위 HUD 구석으로 날아가 목표 창이 된다
        _title.text = _quest.title;

        for (float t = 0f; t < _moveSeconds; t += Time.unscaledDeltaTime)
        {
            ApplyLayout(Mathf.SmoothStep(0f, 1f, t / _moveSeconds));
            yield return null;
        }

        ApplyLayout(1f);
        _routine = null;
    }

    /// <summary>k = 0: 화면 가운데, k = 1: 왼쪽 위 구석</summary>
    private void ApplyLayout(float k)
    {
        Vector2 anchor = Vector2.Lerp(new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), k);
        _panel.anchorMin = anchor;
        _panel.anchorMax = anchor;
        _panel.pivot = anchor;
        _panel.anchoredPosition = Vector2.Lerp(_centerPosition, _cornerPosition, k);
        _panel.localScale = Vector3.one * Mathf.Lerp(_centerScale, _cornerScale, k);
    }

    private void RefreshBody()
    {
        var sb = new StringBuilder();

        foreach (StoryObjective objective in _quest.objectives)
        {
            if (sb.Length > 0)
                sb.Append('\n');

            if (_done.Contains(objective.id))
                sb.Append("<color=#7B8FA1><s>").Append(objective.text).Append("</s></color>");
            else
                sb.Append("▶ ").Append(objective.text);
        }

        _body.text = sb.ToString();
    }
}
