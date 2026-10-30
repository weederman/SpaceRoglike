using System;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 설정(GameSettings)에 쓰는 고정 데이터(단일 출처). 위치: Assets/Resources/Settings/SettingsConfig.asset
///  - 오디오 믹서: 종류별 그룹(Music / SFX / UI)과 그 음량 파라미터(MusicVolume / SfxVolume / UiVolume)
///  - 고를 수 있는 해상도 목록 (화면 비율별). 모니터보다 큰 해상도는 GameSettings가 목록에서 뺀다.
///  - 처음 실행할 때의 기본 음량
/// </summary>
[CreateAssetMenu(fileName = "SettingsConfig", menuName = "SpaceRoglike/Settings Config")]
public class SettingsConfig : ScriptableObject
{
    public const string ResourcePath = "Settings/SettingsConfig";

    [Serializable]
    public class ResolutionPreset
    {
        public int width = 1920;
        public int height = 1080;
        [Tooltip("화면에 보이는 비율 이름 (예: 16:9, 16:10)")]
        public string aspect = "16:9";
        [Tooltip("덧붙이는 이름 (예: Full HD). 비워도 된다")]
        public string note;

        public string Label => string.IsNullOrEmpty(note)
            ? $"{width} × {height} ({aspect})"
            : $"{width} × {height} ({aspect} {note})";
    }

    [Serializable]
    public class ChannelSetting
    {
        public VolumeChannel channel;
        [Tooltip("믹서 그룹 (전체는 비워 둔다: 전체 음량은 AudioListener.volume으로 모든 소리에 건다)")]
        public AudioMixerGroup group;
        [Tooltip("믹서의 음량 파라미터 이름 (Exposed Parameter)")]
        public string parameter;
        [Range(0f, 1f)] public float defaultVolume = 1f;
    }

    [Header("오디오")]
    public AudioMixer mixer;
    public ChannelSetting[] channels = new ChannelSetting[0];

    [Header("그래픽")]
    public ResolutionPreset[] resolutions = new ResolutionPreset[0];
    [Tooltip("처음 실행할 때 전체 화면인지")]
    public bool defaultFullscreen = true;

    private static SettingsConfig _loaded;

    public static SettingsConfig Load()
    {
        if (_loaded == null)
            _loaded = Resources.Load<SettingsConfig>(ResourcePath);
        return _loaded;
    }

    public ChannelSetting Find(VolumeChannel channel)
    {
        foreach (ChannelSetting c in channels)
        {
            if (c != null && c.channel == channel)
                return c;
        }

        return null;
    }
}
