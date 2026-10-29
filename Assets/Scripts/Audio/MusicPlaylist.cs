using UnityEngine;

/// <summary>
/// 상황에 따라 바뀌는 배경음악 목록 (MusicDirector가 쓴다). 위치: Assets/Resources/Audio/MusicPlaylist.asset
///  - calm  : 그리드(플레이어 주변)에 적이 없을 때 (Ultimate_Sci-Fi/Common_Sound)
///  - combat: 그리드에 적이 나타났을 때 (Ultimate_Sci-Fi/Combat_Sound)
/// </summary>
[CreateAssetMenu(fileName = "MusicPlaylist", menuName = "SpaceRoglike/Music Playlist")]
public class MusicPlaylist : ScriptableObject
{
    public const string ResourcePath = "Audio/MusicPlaylist";

    public AudioClip[] calm = new AudioClip[0];
    public AudioClip[] combat = new AudioClip[0];
    [Range(0f, 1f)] public float calmVolume = 0.35f;
    [Range(0f, 1f)] public float combatVolume = 0.4f;
}
