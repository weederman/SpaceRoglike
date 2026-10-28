using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 사운드 재생 (2D). 소리는 SoundTable의 이름으로 부른다.
///  - Play(이름): 한 번 재생 (효과음)
///  - StartLoop(이름, 주인) / StopLoop(주인): 주인이 끌 때까지 반복 (경보, 워프 엔진음 등)
///  - PlayMusic(이름) / StopMusic(): 배경음악 하나를 서서히 바꿔 튼다
/// 스토리 JSON의 cue로도 쓸 수 있다:  sfx:이름 / bgm:이름 / bgm_stop
/// (건너뛰기 중에는 효과음은 내지 않고, 배경음악은 최종 상태대로 맞춘다)
/// 처음 쓸 때 자동으로 생긴다(씬에 둘 필요 없음).
/// </summary>
public class GameAudio : MonoBehaviour
{
    private const float DefaultMusicFade = 1.2f;

    private static GameAudio _instance;

    private SoundTable _table;
    private AudioSource _sfx;
    private AudioSource _music;
    private readonly Dictionary<object, AudioSource> _loops = new Dictionary<object, AudioSource>();
    private Coroutine _musicFade;
    private string _musicName;
    private float _musicVolume = 1f;

    // ── 공개 API ──────────────────────────────────────

    public static void Play(string soundName)
    {
        GameAudio a = Instance;
        SoundTable.Entry e = a != null ? a.Find(soundName) : null;
        if (e != null)
            a._sfx.PlayOneShot(e.clip, e.volume);
    }

    public static void StartLoop(string soundName, object owner)
    {
        GameAudio a = Instance;
        SoundTable.Entry e = a != null ? a.Find(soundName) : null;
        if (e == null || owner == null)
            return;

        if (!a._loops.TryGetValue(owner, out AudioSource source) || source == null)
        {
            source = a.CreateSource("Loop", true);
            a._loops[owner] = source;
        }

        if (source.isPlaying && source.clip == e.clip)
            return;

        source.clip = e.clip;
        source.volume = e.volume;
        source.Play();
    }

    public static void StopLoop(object owner)
    {
        if (_instance == null || owner == null || !_instance._loops.TryGetValue(owner, out AudioSource source))
            return;

        _instance._loops.Remove(owner);
        if (source != null)
            Destroy(source);
    }

    public static void PlayMusic(string musicName, float fadeSeconds = DefaultMusicFade)
    {
        GameAudio a = Instance;
        if (a == null || a._musicName == musicName)
            return;

        SoundTable.Entry e = a.Find(musicName);
        if (e == null)
            return;

        a._musicName = musicName;
        a.FadeMusic(e.clip, e.volume, fadeSeconds);
    }

    public static void StopMusic(float fadeSeconds = DefaultMusicFade)
    {
        if (_instance == null || _instance._musicName == null)
            return;

        _instance._musicName = null;
        _instance.FadeMusic(null, 0f, fadeSeconds);
    }

    // ── 내부 ──────────────────────────────────────────

    private static GameAudio Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("GameAudio");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<GameAudio>();
            }

            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
    }

    // 스토리 cue를 듣도록 게임 시작 때 만들어 둔다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        _ = Instance;
    }

    private void Awake()
    {
        _table = Resources.Load<SoundTable>(SoundTable.ResourcePath);
        if (_table == null)
            Debug.LogWarning($"[GameAudio] Assets/Resources/{SoundTable.ResourcePath}.asset을 찾지 못해 소리를 내지 않습니다.");

        _sfx = CreateSource("Sfx", false);
        _music = CreateSource("Music", true);

        StoryPlayer.OnCue += HandleCue;
    }

    private void OnDestroy()
    {
        StoryPlayer.OnCue -= HandleCue;
        if (_instance == this)
            _instance = null;
    }

    private AudioSource CreateSource(string label, bool loop)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f; // 2D
        return source;
    }

    private SoundTable.Entry Find(string soundName)
    {
        if (_table == null || string.IsNullOrEmpty(soundName))
            return null;

        SoundTable.Entry e = _table.Find(soundName);
        if (e == null || e.clip == null)
        {
            Debug.LogWarning($"[GameAudio] 사운드 '{soundName}'이(가) SoundTable에 없습니다.");
            return null;
        }

        return e;
    }

    private void FadeMusic(AudioClip clip, float volume, float seconds)
    {
        if (_musicFade != null)
            StopCoroutine(_musicFade);

        _musicFade = StartCoroutine(FadeMusicRoutine(clip, volume, seconds));
    }

    private IEnumerator FadeMusicRoutine(AudioClip clip, float volume, float seconds)
    {
        // 지금 곡을 줄이고 → 새 곡을 키운다 (시간은 대사 일시정지와 무관하게 실제 시간)
        float half = Mathf.Max(0.01f, seconds * 0.5f);
        float from = _music.volume;
        for (float t = 0f; _music.isPlaying && t < half; t += Time.unscaledDeltaTime)
        {
            _music.volume = Mathf.Lerp(from, 0f, t / half);
            yield return null;
        }

        _music.Stop();
        _musicVolume = volume;
        if (clip == null)
        {
            _musicFade = null;
            yield break;
        }

        _music.clip = clip;
        _music.volume = 0f;
        _music.Play();
        for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
        {
            _music.volume = Mathf.Lerp(0f, _musicVolume, t / half);
            yield return null;
        }

        _music.volume = _musicVolume;
        _musicFade = null;
    }

    private void HandleCue(string cue)
    {
        if (cue.StartsWith("bgm:"))
            PlayMusic(cue.Substring(4), StoryPlayer.IsSkipping ? 0.2f : DefaultMusicFade);
        else if (cue == "bgm_stop")
            StopMusic(StoryPlayer.IsSkipping ? 0.2f : DefaultMusicFade);
        else if (cue.StartsWith("sfx:") && !StoryPlayer.IsSkipping)
            Play(cue.Substring(4));
    }
}
