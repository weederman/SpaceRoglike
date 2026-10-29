using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스토리 연출용 전체 화면 배경 이미지. 대사 줄의 cue "bg:이미지이름"으로 그 이미지로 크로스페이드하고,
/// "bg:off"로 배경을 걷어 원래 게임 화면으로 돌아간다.
/// 이미지는 Assets/Resources/Story/Backgrounds/ 폴더에 Sprite로 넣고, 파일 이름이 곧 cue의 이름이다
/// (예: tavern.jpg → "bg:tavern"). 화면 비율이 달라도 이미지가 찌그러지지 않고 화면을 가득 채운다.
/// 건너뛰기(Esc)로 지나가는 cue는 페이드 없이 바로 최종 상태가 된다. 시간은 unscaledTime 기준이다.
/// 모양은 Assets/Resources/UI/StoryBackdrop.prefab.
/// </summary>
public class StoryBackdrop : MonoBehaviour
{
    private const string Folder = "Story/Backgrounds/";
    private const string Prefix = "bg:";

    [Tooltip("교대로 쓰는 이미지 2장 (크로스페이드용)")]
    [SerializeField] private Image[] _layers;
    [Tooltip("배경이 나타나거나 사라지는 시간(초). 배경끼리 바꿀 때는 이전 배경을 그대로 깔아 둔 채 새 배경만 나타나 뒤가 비치지 않는다.")]
    [SerializeField, Min(0.05f)] private float _fadeSeconds = 0.35f;

    private readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();
    private int _front = -1; // 지금 보이는 층 (-1 = 배경 없음)
    private Coroutine _fade;

    private void Awake()
    {
        foreach (Image layer in _layers)
        {
            layer.raycastTarget = false;
            layer.gameObject.SetActive(false);
        }
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
        if (!cue.StartsWith(Prefix))
            return;

        string key = cue.Substring(Prefix.Length).Trim();

        if (key == "off")
        {
            Show(null, key);
            return;
        }

        if (!_cache.TryGetValue(key, out Sprite sprite))
        {
            sprite = Resources.Load<Sprite>(Folder + key);
            if (sprite == null)
            {
                Debug.LogWarning($"[Story] 배경 이미지 '{key}'를 찾지 못했습니다. (Assets/Resources/{Folder}{key}, Sprite로 임포트되어야 합니다)");
                return;
            }

            _cache[key] = sprite;
        }

        Show(sprite, key);
    }

    private void Show(Sprite sprite, string key)
    {
        if (_fade != null)
            StopCoroutine(_fade);

        _fade = StartCoroutine(Crossfade(sprite, StoryPlayer.IsSkipping));
    }

    private IEnumerator Crossfade(Sprite sprite, bool instant)
    {
        int back = _front == 0 ? 1 : 0;
        Image from = _front >= 0 ? _layers[_front] : null;
        Image to = sprite != null ? _layers[back] : null;

        // 지금 보이는 층 말고는 모두 끈다. 앞서 하던 페이드가 끊겨 남은 층이 있다면 여기서 정리된다.
        for (int i = 0; i < _layers.Length; i++)
        {
            if (i != _front)
                _layers[i].gameObject.SetActive(false);
        }

        // 페이드 도중 다음 cue가 와도 "지금 보이는 층"이 맞도록 시작할 때 미리 갱신해 둔다
        _front = to != null ? back : -1;

        if (to != null)
        {
            to.sprite = sprite;
            var fitter = to.GetComponent<AspectRatioFitter>();
            if (fitter != null)
                fitter.aspectRatio = sprite.rect.width / sprite.rect.height;

            SetAlpha(to, 0f);
            to.gameObject.SetActive(true);
            to.transform.SetAsLastSibling();
        }

        float fromAlpha = from != null ? from.color.a : 0f;
        float seconds = instant ? 0.0001f : _fadeSeconds;

        // 배경 → 배경: 이전 배경은 그대로 둔 채 새 배경이 위에 나타난다 (둘 다 반투명해져 뒤의 게임 화면이 비치지 않게)
        // 배경 → 없음: 이전 배경만 사라진다
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / seconds);
            if (to != null) SetAlpha(to, k);
            else if (from != null) SetAlpha(from, fromAlpha * (1f - k));
            yield return null;
        }

        if (to != null) SetAlpha(to, 1f);
        if (from != null) from.gameObject.SetActive(false);

        _fade = null;
    }

    private static void SetAlpha(Image image, float alpha)
    {
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }
}
