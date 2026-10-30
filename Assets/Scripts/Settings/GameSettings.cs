using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>소리의 종류. 종류마다 음량을 따로 조절한다 (GameSettings).</summary>
public enum VolumeChannel
{
    Master,     // 전체 (모든 소리)
    Music,      // 배경음악
    Sfx,        // 효과음: 무기·폭발·엔진·워프·채굴 등
    Ui          // 인터페이스: 대사 넘김·퀘스트 알림 등
}

/// <summary>
/// 게임 설정(그래픽·오디오). 저장 파일(SaveSystem)과 별개로 이 컴퓨터에 남는다(PlayerPrefs). 게임을 켤 때 불러와 바로 적용한다.
///  - 오디오: 종류별 음량 0~1 (VolumeChannel). 전체는 AudioListener.volume으로 모든 소리에, 나머지는 오디오 믹서 그룹의 음량으로 건다.
///    소리를 내는 쪽은 AudioSource를 그 종류의 믹서 그룹으로 보낸다(Route). 그룹에 보내지 않은 소리는 전체 음량만 따른다.
///  - 그래픽: 해상도(SettingsConfig의 목록, 모니터보다 큰 것은 뺀다)와 전체 화면/창 모드. 처음 실행하면 지금 화면 그대로 둔다.
/// 데이터(믹서·해상도 목록·기본 음량)는 Assets/Resources/Settings/SettingsConfig.asset.
/// 설정 화면은 값을 바꾸면(Set...) 바로 적용되고 저장된다. 바뀌면 Changed로 알린다.
/// </summary>
public static class GameSettings
{
    private const string VolumeKeyPrefix = "settings.volume.";
    private const string WidthKey = "settings.resolution.width";
    private const string HeightKey = "settings.resolution.height";
    private const string FullscreenKey = "settings.fullscreen";
    private const float MinDecibels = -80f;

    /// <summary>설정이 바뀌었을 때 (설정 화면 갱신용)</summary>
    public static event Action Changed;

    private static readonly Dictionary<VolumeChannel, float> Volumes = new Dictionary<VolumeChannel, float>();
    private static bool _audioApplied;

    // ── 오디오 ─────────────────────────────────────────

    public static float GetVolume(VolumeChannel channel)
    {
        if (Volumes.TryGetValue(channel, out float volume))
            return volume;

        SettingsConfig.ChannelSetting setting = Config != null ? Config.Find(channel) : null;
        volume = PlayerPrefs.GetFloat(VolumeKeyPrefix + channel, setting != null ? setting.defaultVolume : 1f);
        Volumes[channel] = Mathf.Clamp01(volume);
        return Volumes[channel];
    }

    public static void SetVolume(VolumeChannel channel, float volume)
    {
        volume = Mathf.Clamp01(volume);
        Volumes[channel] = volume;
        PlayerPrefs.SetFloat(VolumeKeyPrefix + channel, volume);
        PlayerPrefs.Save();
        ApplyVolume(channel);
        Changed?.Invoke();
    }

    /// <summary>그 종류의 믹서 그룹 (전체이거나 설정이 없으면 null)</summary>
    public static AudioMixerGroup Group(VolumeChannel channel)
    {
        SettingsConfig.ChannelSetting setting = Config != null ? Config.Find(channel) : null;
        return setting != null ? setting.group : null;
    }

    /// <summary>소리를 그 종류의 믹서 그룹으로 보낸다 (그 종류의 음량을 따르게)</summary>
    public static void Route(AudioSource source, VolumeChannel channel)
    {
        if (source == null)
            return;

        AudioMixerGroup group = Group(channel);
        if (group != null)
            source.outputAudioMixerGroup = group;
    }

    private static void ApplyVolume(VolumeChannel channel)
    {
        float volume = GetVolume(channel);
        if (channel == VolumeChannel.Master)
        {
            AudioListener.volume = volume;
            return;
        }

        SettingsConfig.ChannelSetting setting = Config != null ? Config.Find(channel) : null;
        if (setting == null || Config.mixer == null || string.IsNullOrEmpty(setting.parameter))
            return;

        // 믹서 음량은 데시벨: 0 → -80dB(무음), 1 → 0dB
        float db = volume > 0.0001f ? Mathf.Max(MinDecibels, Mathf.Log10(volume) * 20f) : MinDecibels;
        if (!Config.mixer.SetFloat(setting.parameter, db))
            Debug.LogWarning($"[Settings] 오디오 믹서에 '{setting.parameter}' 파라미터가 없습니다. (SettingsConfig 확인)");
    }

    private static void ApplyAllVolumes()
    {
        foreach (VolumeChannel channel in (VolumeChannel[])Enum.GetValues(typeof(VolumeChannel)))
            ApplyVolume(channel);
        _audioApplied = true;
    }

    // ── 그래픽 ─────────────────────────────────────────

    /// <summary>고를 수 있는 해상도 (설정 목록 중 모니터보다 크지 않은 것)</summary>
    public static IReadOnlyList<SettingsConfig.ResolutionPreset> Resolutions
    {
        get
        {
            var list = new List<SettingsConfig.ResolutionPreset>();
            if (Config == null)
                return list;

            // 모니터의 바탕화면 해상도 (에디터에서도 Game 창이 아니라 모니터 크기)
            int maxW = Screen.currentResolution.width;
            int maxH = Screen.currentResolution.height;
            foreach (SettingsConfig.ResolutionPreset r in Config.resolutions)
            {
                if (r != null && r.width <= maxW && r.height <= maxH)
                    list.Add(r);
            }

            return list;
        }
    }

    /// <summary>지금 해상도 (가로, 세로). 고른 적이 없으면 지금 화면 크기 (에디터에서는 Game 창 대신 모니터 크기)</summary>
    public static Vector2Int Resolution => new Vector2Int(
        PlayerPrefs.GetInt(WidthKey, Application.isEditor ? Screen.currentResolution.width : Screen.width),
        PlayerPrefs.GetInt(HeightKey, Application.isEditor ? Screen.currentResolution.height : Screen.height));

    public static bool Fullscreen => PlayerPrefs.GetInt(FullscreenKey, Config == null || Config.defaultFullscreen ? 1 : 0) == 1;

    /// <summary>목록에서 지금 해상도의 번호 (목록에 없으면 -1)</summary>
    public static int ResolutionIndex
    {
        get
        {
            Vector2Int current = Resolution;
            IReadOnlyList<SettingsConfig.ResolutionPreset> list = Resolutions;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].width == current.x && list[i].height == current.y)
                    return i;
            }

            return -1;
        }
    }

    public static void SetResolution(int width, int height)
    {
        PlayerPrefs.SetInt(WidthKey, width);
        PlayerPrefs.SetInt(HeightKey, height);
        PlayerPrefs.Save();
        ApplyScreen();
        Changed?.Invoke();
    }

    public static void SetFullscreen(bool fullscreen)
    {
        PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
        PlayerPrefs.Save();
        ApplyScreen();
        Changed?.Invoke();
    }

    private static void ApplyScreen()
    {
        // 에디터의 Game 창 크기는 바꿀 수 없다 (빌드에서만 적용)
        Vector2Int r = Resolution;
        Screen.SetResolution(r.x, r.y, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }

    // ── 시작 ──────────────────────────────────────────

    /// <summary>게임을 켤 때 저장된 음량을 Start에서 한 번 적용하고 사라진다</summary>
    private class Applier : MonoBehaviour
    {
        private void Start()
        {
            ApplyAllVolumes();
            Destroy(gameObject);
        }
    }

    private static SettingsConfig Config => SettingsConfig.Load();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Volumes.Clear();
        Changed = null;
        _audioApplied = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyOnStart()
    {
        if (Config == null)
        {
            Debug.LogWarning($"[Settings] Assets/Resources/{SettingsConfig.ResourcePath}.asset을 찾지 못해 기본 설정으로 실행합니다.");
            return;
        }

        // 믹서 값은 Start 전에 넣으면 무시되므로, 첫 씬의 Start에서 적용한다 (Applier)
        if (!_audioApplied)
        {
            var go = new GameObject("GameSettingsApplier");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Applier>();
        }

        // 해상도는 고른 적이 있을 때만 (처음 실행이면 지금 화면 그대로)
        if (PlayerPrefs.HasKey(WidthKey) || PlayerPrefs.HasKey(FullscreenKey))
            ApplyScreen();
    }
}
