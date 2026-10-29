using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 진행 중인 임무 표시 (화면 위쪽 가운데, SCI-FI UI Pack 기반). 임무 이름과 목표, 상태 줄(남은 시간·목적지 등), 게이지(지킬 대상·표적 체력, 소탕 진행), 남은 적 수,
/// 끝나면 결과(성공/실패, 보수)를 보여준다. 무엇을 보여줄지는 임무 Director(MissionDirector 하위)가 정한다.
/// 프리팹: Assets/Resources/UI/MissionHud.prefab
/// </summary>
public class MissionHud : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _objective;
    [SerializeField] private TMP_Text _timer;
    [SerializeField] private Image _hullFill;
    [SerializeField] private TMP_Text _hullText;
    [SerializeField] private TMP_Text _enemies;
    [SerializeField] private GameObject _statusGroup;
    [SerializeField] private TMP_Text _result;
    [SerializeField] private Color _successColor = new Color(0.45f, 1f, 0.6f);
    [SerializeField] private Color _failColor = new Color(1f, 0.45f, 0.4f);

    // 결과 글이 시작되는 높이 (창 위쪽에서, 목표 문장 바로 아래)
    private const float ResultTop = 78f;

    private float _rootHeight;
    private Canvas _canvas;
    private Coroutine _notice;
    private string _objectiveText;
    private StarMapUI _map;

    private void Awake()
    {
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            GameFonts.ApplyKorean(text);

        _rootHeight = ((RectTransform)_root.transform).sizeDelta.y;
        _canvas = GetComponent<Canvas>();
        _result.enableAutoSizing = false;
        Hide();
    }

    private void LateUpdate()
    {
        // 항성계 지도를 열어 둔 동안은 지도 창에 겹쳐 보이지 않게 숨긴다
        if (_map == null)
            _map = FindObjectOfType<StarMapUI>();

        bool show = _map == null || !_map.IsOpen;
        if (_canvas != null && _canvas.enabled != show)
            _canvas.enabled = show;
    }

    public void Show(string title, string objective)
    {
        if (_notice != null)
            StopCoroutine(_notice);
        _notice = null;
        _objectiveText = null;

        _title.text = title;
        _objective.text = objective;
        _timer.text = "";
        _enemies.text = "";
        HideBar();
        var root = (RectTransform)_root.transform;
        root.sizeDelta = new Vector2(root.sizeDelta.x, _rootHeight);
        _statusGroup.SetActive(true);
        _result.gameObject.SetActive(false);
        _root.SetActive(true);
    }

    public void Hide()
    {
        _root.SetActive(false);
    }

    /// <summary>남은 시간 (초)과 그 앞에 붙는 이름 (예: "워프 준비")</summary>
    public void SetTimer(string label, float seconds)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
        _timer.text = $"{label}  {s / 60:00}:{s % 60:00}";
    }

    /// <summary>시간 대신 상태 문장을 보여준다 (예: "화물선 워프 개시")</summary>
    public void SetStatus(string text)
    {
        _timer.text = text;
    }

    /// <summary>게이지와 그 이름 (예: "화물선 선체" 0.8 → "화물선 선체 80%"). detail이 있으면 % 대신 그 글자.</summary>
    public void SetBar(string label, float ratio, string detail = null)
    {
        ratio = Mathf.Clamp01(ratio);
        _hullFill.transform.parent.gameObject.SetActive(true);
        _hullFill.fillAmount = ratio;
        string value = detail ?? Mathf.RoundToInt(ratio * 100f) + "%";
        _hullText.text = $"{label} {value}";
    }

    /// <summary>게이지를 숨긴다. note가 있으면 게이지 이름 자리에 안내 문장을 보여준다.</summary>
    public void HideBar(string note = "")
    {
        _hullFill.transform.parent.gameObject.SetActive(false);
        _hullText.text = note;
    }

    /// <summary>남은 적 수. 음수면 표시하지 않는다.</summary>
    public void SetEnemies(int count)
    {
        _enemies.text = count < 0 ? "" : count > 0 ? $"적 {count}척" : "적 없음";
    }

    /// <summary>잠깐 목표 문장 자리에 알림(증원 도착, 표적 도주 등)을 강조해 보여준 뒤 목표 문장으로 돌아간다.</summary>
    public void ShowNotice(string text, float seconds = 3f)
    {
        if (_notice != null)
            StopCoroutine(_notice);
        _notice = StartCoroutine(NoticeRoutine(text, seconds));
    }

    private System.Collections.IEnumerator NoticeRoutine(string text, float seconds)
    {
        if (_objectiveText == null)
            _objectiveText = _objective.text;

        _objective.text = $"<color=#FF8A7A>▲ {text}</color>";
        yield return new WaitForSeconds(seconds);
        _objective.text = _objectiveText;
        _objectiveText = null;
        _notice = null;
    }

    /// <summary>진행 표시 대신 결과를 보여준다. body는 여러 줄 가능.</summary>
    public void ShowResult(bool success, string headline, string body)
    {
        _statusGroup.SetActive(false);
        _result.gameObject.SetActive(true);
        _result.color = success ? _successColor : _failColor;
        _result.text = string.IsNullOrEmpty(body) ? headline : $"{headline}\n<size=75%><color=#D8F3F8>{body}</color></size>";

        // 결과 줄 수에 맞춰 창을 아래로 늘린다 (목표 문장 아래부터)
        RectTransform rect = _result.rectTransform;
        float height = _result.GetPreferredValues(_result.text, rect.rect.width, 0f).y;
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -ResultTop - height * 0.5f);
        var root = (RectTransform)_root.transform;
        root.sizeDelta = new Vector2(root.sizeDelta.x, Mathf.Max(_rootHeight, ResultTop + height + 22f));
    }
}
