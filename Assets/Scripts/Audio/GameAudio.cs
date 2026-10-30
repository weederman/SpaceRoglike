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
/// 음량 설정(GameSettings): 배경음악은 음악, 소리는 SoundTable의 종류(효과음/인터페이스)대로 믹서 그룹에 보낸다.
/// 처음 쓸 때 자동으로 생긴다(씬에 둘 필요 없음).
/// </summary>
public class GameAudio : MonoBehaviour
{
    private const float DefaultMusicFade = 1.2f;

    private static GameAudio _instance;

    private SoundTable _table;
    private AudioSource _sfx;
    private AudioSource _ui;
    private AudioSource _music;
    private readonly Dictionary<object, AudioSource> _loops = new Dictionary<object, AudioSource>();
    private readonly Dictionary<SoundTable.Entry, AudioClip> _boosted = new Dictionary<SoundTable.Entry, AudioClip>();
    private Coroutine _musicFade;
    private string _musicName;
    private float _musicVolume = 1f;

    // ── 공개 API ──────────────────────────────────────

    /// <summary>스토리 배경음악(bgm: cue, 예: 술집)이 나오는 중인지. 이때는 상황 음악(MusicDirector)이 물러난다.</summary>
    public static bool IsStoryMusicPlaying => _instance != null && _instance._musicName != null;

    public static void Play(string soundName)
    {
        GameAudio a = Instance;
        SoundTable.Entry e = a != null ? a.Find(soundName) : null;
        if (e != null)
            (e.channel == VolumeChannel.Ui ? a._ui : a._sfx).PlayOneShot(a.ClipFor(e), Mathf.Min(1f, e.volume));
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

        GameSettings.Route(source, e.channel == VolumeChannel.Ui ? VolumeChannel.Ui : VolumeChannel.Sfx);

        AudioClip clip = a.ClipFor(e);
        if (source.isPlaying && source.clip == clip)
            return;

        source.clip = clip;
        source.volume = Mathf.Min(1f, e.volume);
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

    /// <summary>반복음과 스토리 배경음악을 모두 멈춘다 (타이틀로 돌아갈 때: 주인이 사라져도 반복음이 남지 않게)</summary>
    public static void StopAll()
    {
        if (_instance == null)
            return;

        foreach (AudioSource source in _instance._loops.Values)
        {
            if (source != null)
                Destroy(source);
        }

        _instance._loops.Clear();
        StopMusic(0.2f);
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
        a.FadeMusic(a.ClipFor(e), Mathf.Min(1f, e.volume), fadeSeconds);
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
        _ui = CreateSource("Ui", false);
        _music = CreateSource("Music", true);

        // 음량 설정의 종류별로 믹서 그룹에 보낸다 (GameSettings)
        GameSettings.Route(_sfx, VolumeChannel.Sfx);
        GameSettings.Route(_ui, VolumeChannel.Ui);
        GameSettings.Route(_music, VolumeChannel.Music);

        // 소리 파일이 "미리 불러오기" 꺼짐으로 되어 있어 처음 재생이 빠질 수 있으므로 시작할 때 불러 둔다
        if (_table != null)
        {
            foreach (SoundTable.Entry e in _table.Entries)
            {
                if (e != null && e.clip != null)
                    e.clip.LoadAudioData();
            }
        }

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

    /// <summary>음량 배율이 1보다 크면 소리를 키운 복사본(한 번 만들어 둔다), 아니면 원래 소리</summary>
    private AudioClip ClipFor(SoundTable.Entry e)
    {
        if (e.volume <= 1f)
            return e.clip;

        if (_boosted.TryGetValue(e, out AudioClip boosted))
            return boosted;

        boosted = e.clip;
        if (e.clip.loadType == AudioClipLoadType.DecompressOnLoad && e.clip.LoadAudioData())
        {
            var data = new float[e.clip.samples * e.clip.channels];
            if (e.clip.GetData(data, 0))
            {
                for (int i = 0; i < data.Length; i++)
                    data[i] = Mathf.Clamp(data[i] * e.volume, -1f, 1f);

                boosted = AudioClip.Create(e.clip.name + "_boost", e.clip.samples, e.clip.channels, e.clip.frequency, false);
                boosted.SetData(data, 0);
            }
        }

        _boosted[e] = boosted;
        return boosted;
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
