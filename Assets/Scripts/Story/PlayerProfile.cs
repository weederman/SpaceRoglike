using UnityEngine;

/// <summary>
/// 플레이어(주인공)의 이름. 새 게임에서 입력받고(NameEntryView) 저장(SaveSystem)에 함께 남는다.
///  - 대사창에서 주인공(화자 id "hero")의 이름 칸에 이 이름을 쓴다.
///  - 대사·선택지 문장의 {player}를 이 이름으로 바꾼다 (예: "{player}, 들리나?").
/// 이름이 없으면(타이틀을 거치지 않은 개발용 시작 등) Speakers.json의 hero 이름을 쓴다.
/// </summary>
public static class PlayerProfile
{
    public const string HeroSpeakerId = "hero";
    public const string NameToken = "{player}";
    public const int MinLength = 2;
    public const int MaxLength = 10;

    /// <summary>입력한 이름 (없으면 null)</summary>
    public static string Name { get; set; }

    public static bool HasName => !string.IsNullOrEmpty(Name);

    /// <summary>화면에 쓰는 이름: 입력한 이름, 없으면 Speakers.json의 hero 이름</summary>
    public static string DisplayName
    {
        get
        {
            if (HasName)
                return Name;
            return StoryDatabase.TryGetSpeaker(HeroSpeakerId, out StorySpeaker hero) && !string.IsNullOrEmpty(hero.name)
                ? hero.name
                : HeroSpeakerId;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Name = null;
    }

    /// <summary>입력한 글을 이름으로 다듬는다: 앞뒤 공백을 지우고, 리치텍스트로 읽히는 &lt; &gt;는 뺀다</summary>
    public static string Clean(string raw)
    {
        return (raw ?? "").Replace("<", "").Replace(">", "").Trim();
    }

    /// <summary>다듬은 이름이 MinLength~MaxLength 글자인지</summary>
    public static bool IsValid(string raw)
    {
        int length = Clean(raw).Length;
        return length >= MinLength && length <= MaxLength;
    }

    /// <summary>문장의 {player}를 이름으로 바꾼다</summary>
    public static string Format(string text)
    {
        return string.IsNullOrEmpty(text) || !text.Contains(NameToken) ? text : text.Replace(NameToken, DisplayName);
    }
}
