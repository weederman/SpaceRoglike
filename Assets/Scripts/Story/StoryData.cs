using System;
using System.Collections.Generic;

// Assets/Resources/Story/*.json 한 파일의 구조. JsonUtility로 읽으므로 필드 이름이 곧 JSON 키다.
// 여러 파일로 나눠도 되고(화자 정의 파일 / 프롤로그 / 튜토리얼 ...) 로드할 때 하나로 합쳐진다.

[Serializable]
public class StoryFile
{
    public StorySpeaker[] speakers = new StorySpeaker[0];
    public StorySequence[] sequences = new StorySequence[0];
    public StoryQuest[] quests = new StoryQuest[0];
    public StoryTutorial[] tutorials = new StoryTutorial[0];
}

/// <summary>
/// 튜토리얼 안내 창 하나. cue "tutorial_show:id"로 화면에 띄우고 "tutorial_hide"로 닫는다.
/// 조작법처럼 "이름: 설명" 꼴의 안내를 여러 개 한 번에 보여줄 때 쓴다.
/// </summary>
[Serializable]
public class StoryTutorial
{
    public string id;
    public string title;
    public StoryTip[] tips = new StoryTip[0];
}

/// <summary>튜토리얼 안내의 한 항목 (예: "기본 이동" / "추진기를 이용해 …").</summary>
[Serializable]
public class StoryTip
{
    public string title;
    public string text;
}

/// <summary>퀘스트 정의. 줄의 cue "quest_accept:퀘스트id"로 수락되고 HUD 목표 창에 표시된다.</summary>
[Serializable]
public class StoryQuest
{
    public string id;
    public string title;
    public StoryObjective[] objectives = new StoryObjective[0];
}

/// <summary>퀘스트의 목표 한 줄. cue "quest_done:퀘스트id:목표id"로 완료 표시된다.</summary>
[Serializable]
public class StoryObjective
{
    public string id;
    public string text;
}

/// <summary>대사 앞에 표시되는 화자. 대사의 speaker 값은 여기 id를 가리킨다.</summary>
[Serializable]
public class StorySpeaker
{
    public string id;

    /// <summary>화면에 표시되는 이름</summary>
    public string name;

    /// <summary>이름표 색 (#RRGGBB). 비우면 흰색.</summary>
    public string color;

    /// <summary>대사창이 뜨는 쪽. "right"(플레이어/주인공) 또는 "left"(NPC). 비우면 left.</summary>
    public string side;

    public bool IsRight => string.Equals(side, "right", StringComparison.OrdinalIgnoreCase);
}

/// <summary>처음부터 끝까지 재생되는 대사 묶음 (프롤로그, 튜토리얼 1단계 등). lines를 위에서 아래로 읽고, next로 건너뛸 수 있다.</summary>
[Serializable]
public class StorySequence
{
    /// <summary>next에 쓰면 이 시퀀스를 바로 끝낸다.</summary>
    public const string EndMarker = "$end";

    public string id;
    public StoryLine[] lines = new StoryLine[0];

    /// <summary>
    /// 이 시퀀스가 끝나면 이어서 재생할 시퀀스 id(선택). 사이에 플레이어 조작이 돌아가지 않고 한 흐름으로 이어진다.
    /// 예: 프롤로그 → 튜토리얼 장면 1.
    /// </summary>
    public string then;

    [NonSerialized] private Dictionary<string, int> _indexById;

    /// <summary>줄 id가 가리키는 줄 번호. EndMarker면 lines.Length(= 끝), 없으면 -1.</summary>
    public int IndexOf(string lineId)
    {
        if (lineId == EndMarker)
            return lines.Length;

        if (_indexById == null)
        {
            _indexById = new Dictionary<string, int>();
            for (int i = 0; i < lines.Length; i++)
            {
                string id = lines[i].id;
                if (!string.IsNullOrEmpty(id) && !_indexById.ContainsKey(id))
                    _indexById.Add(id, i);
            }
        }

        return !string.IsNullOrEmpty(lineId) && _indexById.TryGetValue(lineId, out int index) ? index : -1;
    }
}

/// <summary>
/// 시퀀스의 한 줄. 이렇게 쓴다.
///  · 대사:   speaker + text. 화자를 비우면 화자 없는 나레이션.
///  · 연출:   cue만 있고 text가 없음. 이름을 StoryPlayer.OnCue로 알려 주고 delay초 뒤 다음 줄로 넘어간다.
///  · 선택지: text(질문/상대 대사) + choices. 대사가 다 나오면 선택지가 뜨고, 고른 선택지의 next로 이어진다.
///  · 분기 합류: 줄에 id를 붙여 두고, 다른 줄/선택지의 next에 그 id를 쓰면 거기로 점프한다.
/// 이 중 여럿을 한 줄에 섞어도 된다(대사 + cue 등).
/// </summary>
[Serializable]
public class StoryLine
{
    /// <summary>이 줄의 이름(선택). 다른 줄의 next가 점프해 올 목적지가 되거나, 선택 이벤트에서 어떤 선택지였는지 구분할 때 쓴다. 시퀀스 안에서 겹치면 안 된다.</summary>
    public string id;

    /// <summary>화자 id (StorySpeaker.id). 비우면 나레이션.</summary>
    public string speaker;

    /// <summary>출력할 문장. 비우면 연출 전용 줄. &lt;b&gt;, &lt;i&gt;, &lt;color=#ff0000&gt; 같은 리치텍스트 사용 가능.</summary>
    public string text;

    /// <summary>
    /// 이 줄이 시작될 때 StoryPlayer.OnCue로 전달되는 연출 이름 (예: "warp_storm"). 연출 쪽에서 이 이름을 구독해 처리한다.
    /// 세미콜론(;)으로 여러 개를 한 줄에 쓸 수 있다 (예: "bg:tavern_talk;some_other_cue").
    /// 기본 제공 cue:
    ///   bg:이미지이름   배경 이미지로 크로스페이드 (Resources/Story/Backgrounds/이미지이름). bg:off는 배경을 걷는다.
    ///   quest_accept:퀘스트id   임무 수락 연출 + HUD 목표 창
    ///   quest_done:퀘스트id:목표id   목표 완료 표시
    ///   tutorial_show:안내id / tutorial_hide   튜토리얼 안내 창 열기/닫기
    /// </summary>
    public string cue;

    /// <summary>
    /// 대사 줄: 문장이 다 나온 뒤 이 시간(초)이 지나면 입력 없이 자동으로 다음 줄로. 0이면 입력(클릭/Space/Enter)을 기다린다.
    /// 연출 줄: 다음 줄로 넘어가기 전 대기 시간(초). 0이면 바로 넘어간다. 선택지가 있는 줄에서는 쓰지 않는다.
    /// </summary>
    public float delay;

    /// <summary>
    /// 이 줄 다음에 갈 줄 id. 비우면 바로 아래 줄. 분기 하나가 끝나고 공통 장면으로 합류할 때 쓴다.
    /// 시퀀스를 여기서 끝내려면 "$end". 선택지가 있는 줄에서는 선택지의 next가 우선한다.
    /// </summary>
    public string next;

    /// <summary>
    /// 이 이름의 신호(StoryPlayer.Signal)가 올 때까지 다음 줄로 넘어가지 않는다(선택).
    /// "비콘을 클릭하세요"처럼 플레이어가 실제로 뭔가를 해야 넘어가는 튜토리얼 줄에 쓴다.
    /// 신호를 보내는 쪽(예: 비콘)은 코드에서 StoryPlayer.Signal("이름")을 부른다. 건너뛰기(Esc)를 누르면 신호 없이 넘어간다.
    /// </summary>
    public string waitFor;

    /// <summary>플레이어가 고르는 선택지. 비어 있으면 일반 줄.</summary>
    public StoryChoice[] choices;
}

/// <summary>선택지 하나.</summary>
[Serializable]
public class StoryChoice
{
    /// <summary>게임 쪽에서 어떤 선택이었는지 알아볼 이름(선택). StoryPlayer.OnChoice로 전달된다.</summary>
    public string id;

    /// <summary>선택지에 표시되는 문장</summary>
    public string text;

    /// <summary>고른 뒤 이어질 줄 id. 비우면 선택지가 있는 줄의 바로 아래 줄(또는 그 줄의 next).</summary>
    public string next;
}
