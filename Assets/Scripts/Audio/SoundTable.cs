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
        [Tooltip("음량 배율. 1보다 크면 소리를 키운 복사본을 만들어 쓴다 (원래 아주 작게 녹음된 소리용, 압축 없이 불러오는 소리만).")]
        [Range(0f, 8f)] public float volume = 1f;
    }

    [SerializeField] private Entry[] _entries = new Entry[0];

    public Entry[] Entries => _entries;

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
