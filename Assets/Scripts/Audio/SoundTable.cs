using System;
using UnityEngine;

/// <summary>
/// 게임 사운드 목록(단일 출처). 코드와 스토리 JSON은 소리를 파일이 아니라 이름으로 부르고, 이름 → 파일·음량은 여기서만 정한다.
/// 위치: Assets/Resources/Audio/SoundTable.asset
/// </summary>
[CreateAssetMenu(fileName = "SoundTable", menuName = "SpaceRoglike/Sound Table")]
public class SoundTable : ScriptableObject
{
    public const string ResourcePath = "Audio/SoundTable";

    [Serializable]
    public class Entry
    {
        public string name;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [SerializeField] private Entry[] _entries = new Entry[0];

    public Entry Find(string soundName)
    {
        foreach (Entry e in _entries)
        {
            if (e != null && e.name == soundName)
                return e;
        }

        return null;
    }
}
