using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Assets/Resources/Story 폴더의 JSON 파일을 모두 읽어 하나로 합친 대사 데이터 창구.
/// 대사를 고치려면 JSON만 수정하면 되고 코드는 건드리지 않는다. 처음 조회할 때 한 번 읽으며,
/// 플레이 중에 JSON을 고쳤다면 Reload()로 다시 읽는다(개발자 모드의 "story reload").
/// </summary>
public static class StoryDatabase
{
    private const string ResourceFolder = "Story";

    private static readonly Dictionary<string, StorySequence> _sequences = new Dictionary<string, StorySequence>();
    private static readonly Dictionary<string, StorySpeaker> _speakers = new Dictionary<string, StorySpeaker>();
    private static readonly Dictionary<string, StoryQuest> _quests = new Dictionary<string, StoryQuest>();
    private static readonly Dictionary<string, StoryTutorial> _tutorials = new Dictionary<string, StoryTutorial>();
    private static bool _loaded;

    public static IEnumerable<string> SequenceIds
    {
        get
        {
            EnsureLoaded();
            return _sequences.Keys;
        }
    }

    public static bool TryGetSequence(string id, out StorySequence sequence)
    {
        EnsureLoaded();
        return _sequences.TryGetValue(id ?? "", out sequence);
    }

    public static bool TryGetSpeaker(string id, out StorySpeaker speaker)
    {
        EnsureLoaded();
        return _speakers.TryGetValue(id ?? "", out speaker);
    }

    public static bool TryGetQuest(string id, out StoryQuest quest)
    {
        EnsureLoaded();
        return _quests.TryGetValue(id ?? "", out quest);
    }

    public static bool TryGetTutorial(string id, out StoryTutorial tutorial)
    {
        EnsureLoaded();
        return _tutorials.TryGetValue(id ?? "", out tutorial);
    }

    /// <summary>JSON을 처음부터 다시 읽는다.</summary>
    public static void Reload()
    {
        _loaded = false;
        EnsureLoaded();
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
            return;

        _loaded = true;
        _sequences.Clear();
        _speakers.Clear();
        _quests.Clear();
        _tutorials.Clear();

        var files = Resources.LoadAll<TextAsset>(ResourceFolder);
        if (files.Length == 0)
            Debug.LogWarning($"[Story] Assets/Resources/{ResourceFolder} 폴더에 JSON 파일이 없습니다.");

        foreach (var file in files)
            Merge(file);

        // 파일이 나뉘어 있을 수 있으므로, 다른 시퀀스를 가리키는 then은 전부 읽은 뒤에 확인한다
        foreach (var sequence in _sequences.Values)
        {
            if (!string.IsNullOrEmpty(sequence.then) && !_sequences.ContainsKey(sequence.then))
                Debug.LogWarning($"[Story] 시퀀스 '{sequence.id}'의 then '{sequence.then}'에 해당하는 시퀀스가 없습니다.");
        }
    }

    private static void Merge(TextAsset file)
    {
        StoryFile data;
        try
        {
            data = JsonUtility.FromJson<StoryFile>(file.text);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Story] {file.name}.json을 읽지 못했습니다. JSON 문법(쉼표, 따옴표, 괄호)을 확인하세요.\n{e.Message}");
            return;
        }

        if (data == null)
            return;

        foreach (var speaker in data.speakers)
        {
            if (string.IsNullOrEmpty(speaker.id))
            {
                Debug.LogWarning($"[Story] {file.name}.json: id가 없는 화자가 있어 무시합니다.");
                continue;
            }

            if (_speakers.ContainsKey(speaker.id))
                Debug.LogWarning($"[Story] 화자 id '{speaker.id}'가 중복되어 {file.name}.json의 것으로 덮어씁니다.");

            _speakers[speaker.id] = speaker;
        }

        foreach (var quest in data.quests)
        {
            if (string.IsNullOrEmpty(quest.id))
            {
                Debug.LogWarning($"[Story] {file.name}.json: id가 없는 퀘스트가 있어 무시합니다.");
                continue;
            }

            _quests[quest.id] = quest;
        }

        foreach (var tutorial in data.tutorials)
        {
            if (string.IsNullOrEmpty(tutorial.id))
            {
                Debug.LogWarning($"[Story] {file.name}.json: id가 없는 튜토리얼 안내가 있어 무시합니다.");
                continue;
            }

            _tutorials[tutorial.id] = tutorial;
        }

        foreach (var sequence in data.sequences)
        {
            if (string.IsNullOrEmpty(sequence.id))
            {
                Debug.LogWarning($"[Story] {file.name}.json: id가 없는 시퀀스가 있어 무시합니다.");
                continue;
            }

            if (_sequences.ContainsKey(sequence.id))
                Debug.LogWarning($"[Story] 시퀀스 id '{sequence.id}'가 중복되어 {file.name}.json의 것으로 덮어씁니다.");

            _sequences[sequence.id] = sequence;
            Validate(sequence, file.name);
        }
    }

    /// <summary>JSON 작성 실수(id 중복, 없는 줄로 점프)를 재생 중이 아니라 로드할 때 바로 알려 준다.</summary>
    private static void Validate(StorySequence sequence, string fileName)
    {
        var seen = new HashSet<string>();

        for (int i = 0; i < sequence.lines.Length; i++)
        {
            StoryLine line = sequence.lines[i];
            string where = $"[Story] {fileName}.json '{sequence.id}' {i + 1}번째 줄";

            if (!string.IsNullOrEmpty(line.id) && !seen.Add(line.id))
                Debug.LogWarning($"{where}: id '{line.id}'가 시퀀스 안에서 중복됩니다. 점프는 먼저 나온 줄로 갑니다.");

            CheckTarget(sequence, line.next, where);

            if (line.choices == null)
                continue;

            for (int c = 0; c < line.choices.Length; c++)
                CheckTarget(sequence, line.choices[c].next, $"{where} 선택지 {c + 1}");
        }
    }

    private static void CheckTarget(StorySequence sequence, string target, string where)
    {
        if (!string.IsNullOrEmpty(target) && sequence.IndexOf(target) < 0)
            Debug.LogWarning($"{where}: next '{target}'에 해당하는 줄 id가 없습니다. 바로 아래 줄로 이어집니다.");
    }
}
