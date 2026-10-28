using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레이더 연출. 화면 오른쪽 위에 둥근 레이더(SCI-FI UI Pack CircleMap)가 뜨고, 붉은 신호가 점멸하며 중심으로 다가온다.
///   radar_show   레이더를 띄운다 (신호 위치는 아래 인스펙터 값)
///   radar_hide   레이더를 닫는다
/// 실제 적 위치와 연결된 레이더가 아니라 스토리용 연출이다. 건너뛰기(Esc)로 지나가는 cue는 페이드 없이 바로 처리한다.
/// 모양은 Assets/Resources/UI/RadarPing.prefab.
/// </summary>
public class RadarPing : MonoBehaviour
{
    private const string ShowCue = "radar_show";
    private const string HideCue = "radar_hide";

    [SerializeField] private CanvasGroup _group;
    [SerializeField] private RectTransform[] _blips;
    [SerializeField] private Image[] _blipImages;

    [Header("신호 위치 (각도: 위쪽이 0도, 시계 방향 / 거리: 0=중심, 1=레이더 가장자리)")]
    [SerializeField] private float[] _angles = { 20f, 75f, -40f };
    [SerializeField] private float[] _startDistances = { 0.92f, 0.95f, 0.88f };
    [SerializeField, Range(0f, 1f)] private float _endDistance = 0.6f;
    [Tooltip("신호가 가장자리에서 끝 위치까지 다가오는 데 걸리는 시간(초)")]
    [SerializeField, Min(0.1f)] private float _approachSeconds = 14f;
    [Tooltip("신호가 놓이는 원의 반지름(레이더 이미지 기준 픽셀)")]
    [SerializeField] private float _radarRadius = 135f;
    [Tooltip("레이더 중심 위치 (이미지 중앙에서의 보정)")]
    [SerializeField] private Vector2 _center = new Vector2(0f, -7f);

    private float _shownAt;
    private bool _visible;
    private Coroutine _fade;

    private void Awake()
    {
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
        if (cue == ShowCue)
            SetVisible(true, StoryPlayer.IsSkipping);
        else if (cue == HideCue)
            SetVisible(false, StoryPlayer.IsSkipping);
    }

    private void SetVisible(bool show, bool instant)
    {
        if (show == _visible)
            return;

        _visible = show;
        if (show)
            _shownAt = Time.unscaledTime;

        if (_fade != null)
            StopCoroutine(_fade);

        _fade = StartCoroutine(Fade(show, instant ? 0f : 0.5f));
    }

    private void Update()
    {
        if (!_visible)
            return;

        float t = Mathf.Clamp01((Time.unscaledTime - _shownAt) / _approachSeconds);
        float blink = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f));

        for (int i = 0; i < _blips.Length && i < _angles.Length; i++)
        {
            float distance = Mathf.Lerp(_startDistances[i], _endDistance, Mathf.SmoothStep(0f, 1f, t)) * _radarRadius;
            float rad = _angles[i] * Mathf.Deg2Rad;

            // 위쪽이 0도, 시계 방향
            _blips[i].anchoredPosition = _center + new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * distance;

            Color c = _blipImages[i].color;
            c.a = blink;
            _blipImages[i].color = c;
        }
    }

    private IEnumerator Fade(bool show, float seconds)
    {
        _group.gameObject.SetActive(true);

        float from = _group.alpha;
        float to = show ? 1f : 0f;

        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            _group.alpha = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }

        _group.alpha = to;
        _group.gameObject.SetActive(show);
        _fade = null;
    }
}
