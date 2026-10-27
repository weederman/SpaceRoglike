using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 대사 시퀀스(StoryDatabase)를 한 줄씩 나레이션바(NarrationBar)에 재생한다.
/// 어디서든 StoryPlayer.Play("prologue", 끝났을때할일) 한 줄로 시작하고, 씬에 따로 배치할 필요는 없다(자동 생성).
///
/// 조작: 클릭 / Space / Enter = 타자 효과 건너뛰기 → 다음 줄,  Esc = 건너뛰기(다음 선택지 또는 끝까지 빨리 감기).
/// 선택지: ↑↓ 또는 W/S로 고르고 Enter/Space, 숫자키 1~9, 마우스 호버+클릭.
/// 재생 중에는 GameInputLock으로 플레이어 조작(이동/발사/줌)과 뒤의 UI 클릭이 막힌다.
///
/// 워프 폭풍, 화이트아웃 같은 연출은 여기서 직접 하지 않는다. JSON의 cue 이름이 OnCue로 전달되므로,
/// 연출 쪽에서 구독해 처리하면 된다. 건너뛰는 동안의 cue는 IsSkipping == true인 상태로 빠르게 전달되니,
/// 연출은 이때 애니메이션 없이 최종 상태로 바로 맞추면 된다.
/// 선택은 OnChoice로 알린다. 선택에 따른 게임 쪽 반응(호감도, 보상 등)은 여기서 구독해 처리한다.
/// </summary>
public class StoryPlayer : MonoBehaviour
{
    /// <summary>줄이 시작될 때 그 줄의 cue 이름</summary>
    public static event Action<string> OnCue;

    /// <summary>플레이어가 선택지를 골랐을 때 (선택지가 있던 줄 id, 고른 선택지 id, 고른 칸 번호 0부터)</summary>
    public static event Action<string, string, int> OnChoice;

    /// <summary>시퀀스가 끝났을 때 (시퀀스 id, 건너뛰었는지)</summary>
    public static event Action<string, bool> OnSequenceEnded;

    public static bool IsPlaying { get; private set; }

    /// <summary>건너뛰기로 줄을 빨리 감는 중인지</summary>
    public static bool IsSkipping { get; private set; }

    private const string BarPrefabPath = "UI/NarrationBar";
    private const string BackdropPrefabPath = "UI/StoryBackdrop";
    private const string QuestPrefabPath = "UI/QuestTracker";

    private static StoryPlayer _instance;

    private NarrationBar _bar;
    private bool _advanceRequested;
    private bool _skipRequested;
    private int _startFrame;

    // 선택지 진행 상태
    private int _choiceCount;
    private int _choiceHighlight;
    private int _choicePicked = -1;

    /// <summary>시퀀스를 재생한다. 이미 재생 중이거나 id가 없으면 false.</summary>
    public static bool Play(string sequenceId, Action onComplete = null)
    {
        if (IsPlaying)
        {
            Debug.LogWarning($"[Story] 이미 재생 중이라 '{sequenceId}'를 시작하지 않았습니다.");
            return false;
        }

        if (!StoryDatabase.TryGetSequence(sequenceId, out StorySequence sequence))
        {
            Debug.LogWarning($"[Story] '{sequenceId}' 시퀀스를 찾지 못했습니다. (Assets/Resources/Story/*.json)");
            return false;
        }

        StoryPlayer player = GetInstance();
        if (player._bar == null)
            return false;

        player.Begin(sequence, onComplete);
        return true;
    }

    /// <summary>재생 중인 시퀀스를 건너뛴다(다음 선택지 또는 끝까지). 재생 중이 아니면 아무 일도 없다.</summary>
    public static void Skip()
    {
        if (IsPlaying && _instance != null)
            _instance._skipRequested = true;
    }

    // 에디터에서 도메인 리로드를 꺼 둬도 플레이마다 깨끗이 시작하도록
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnCue = null;
        OnChoice = null;
        OnSequenceEnded = null;
        IsPlaying = false;
        IsSkipping = false;
        _instance = null;
    }

    private static StoryPlayer GetInstance()
    {
        if (_instance == null)
        {
            var go = new GameObject("StoryPlayer");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<StoryPlayer>();
        }

        return _instance;
    }

    private void Awake()
    {
        var prefab = Resources.Load<NarrationBar>(BarPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[Story] Assets/Resources/{BarPrefabPath}.prefab을 찾지 못해 스토리를 재생할 수 없습니다.");
            return;
        }

        _bar = Instantiate(prefab, transform);
        _bar.ChoiceHovered += HandleChoiceHovered;
        _bar.ChoiceClicked += HandleChoiceClicked;

        // cue("bg:...", "quest_accept:...")를 알아듣는 배경/퀘스트 UI. 없어도 대사 재생은 된다.
        InstantiateOptional<StoryBackdrop>(BackdropPrefabPath);
        InstantiateOptional<QuestTracker>(QuestPrefabPath);
    }

    private void InstantiateOptional<T>(string resourcePath) where T : Component
    {
        var prefab = Resources.Load<T>(resourcePath);
        if (prefab == null)
            Debug.LogWarning($"[Story] Assets/Resources/{resourcePath}.prefab을 찾지 못했습니다. 해당 cue는 무시됩니다.");
        else
            Instantiate(prefab, transform);
    }

    private void OnDestroy()
    {
        GameInputLock.Unlock(this);

        if (_instance == this)
        {
            _instance = null;
            IsPlaying = false;
            IsSkipping = false;
        }
    }

    private void Begin(StorySequence sequence, Action onComplete)
    {
        IsPlaying = true;
        IsSkipping = false;
        _advanceRequested = false;
        _skipRequested = false;
        _startFrame = Time.frameCount; // 재생을 시작시킨 바로 그 클릭/키가 첫 줄을 넘기지 않도록

        _bar.SetBlocking(true);
        GameInputLock.Lock(this);

        StartCoroutine(Run(sequence, onComplete));
    }

    // ── 입력 ──────────────────────────────────────────

    private void Update()
    {
        if (!IsPlaying)
            return;

        // 개발자 모드 창이 열려 있으면 입력을 받지 않는다
        if (GameInputLock.IsLockedExcept(this))
            return;

        if (_bar.IsChoosing)
        {
            HandleChoiceInput();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            _skipRequested = true;
            return;
        }

        if (Time.frameCount == _startFrame)
            return;

        bool pressed = Input.GetMouseButtonDown(0) ||
                       Input.GetKeyDown(KeyCode.Space) ||
                       Input.GetKeyDown(KeyCode.Return) ||
                       Input.GetKeyDown(KeyCode.KeypadEnter);
        if (!pressed)
            return;

        // 문장이 나오는 중이면 먼저 전부 보여주고, 다 나온 뒤에 누르면 다음 줄로
        if (_bar.IsTyping)
            _bar.CompleteTyping();
        else
            _advanceRequested = true;
    }

    private void HandleChoiceInput()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            MoveChoiceHighlight(-1);
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            MoveChoiceHighlight(1);

        // 숫자키 1~9로 바로 선택
        for (int n = 0; n < Mathf.Min(_choiceCount, 9); n++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + n) || Input.GetKeyDown(KeyCode.Keypad1 + n))
            {
                _choicePicked = n;
                return;
            }
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            _choicePicked = _choiceHighlight;
    }

    private void MoveChoiceHighlight(int step)
    {
        _choiceHighlight = (_choiceHighlight + step + _choiceCount) % _choiceCount;
        _bar.HighlightChoice(_choiceHighlight);
    }

    private void HandleChoiceHovered(int index)
    {
        if (index == _choiceHighlight)
            return;

        _choiceHighlight = index;
        _bar.HighlightChoice(index);
    }

    private void HandleChoiceClicked(int index)
    {
        // 개발자 모드 창이 열려 있을 때의 클릭은 무시
        if (!GameInputLock.IsLockedExcept(this))
            _choicePicked = index;
    }

    // ── 재생 ──────────────────────────────────────────

    private IEnumerator Run(StorySequence sequence, Action onComplete)
    {
        StoryLine[] lines = sequence.lines ?? new StoryLine[0];
        bool skippedAny = false;
        int i = 0;

        while (i >= 0 && i < lines.Length)
        {
            StoryLine line = lines[i];
            bool hasChoices = line.choices != null && line.choices.Length > 0;
            bool hasText = !string.IsNullOrEmpty(line.text);

            // 빨리 감기는 선택지 앞에서 멈춘다. 선택은 대신 해 줄 수 없으니까.
            if (_skipRequested && hasChoices)
                _skipRequested = false;

            IsSkipping = _skipRequested;
            skippedAny |= _skipRequested;
            FireCue(line.cue);

            int next = NextIndex(sequence, line, i + 1);

            // 빨리 감는 중: 나머지 줄은 보여주지 않고 cue만 전달하며 지나간다
            if (_skipRequested)
            {
                i = next;
                continue;
            }

            if (!hasText && !hasChoices)
            {
                // 연출 전용 줄: 바를 숨기고 delay만큼 기다린다
                _bar.Hide();
                yield return WaitSeconds(line.delay, false);
                i = next;
                continue;
            }

            if (hasText)
            {
                ShowLine(line);

                while (_bar.IsTyping && !(_skipRequested && !hasChoices))
                    yield return null;

                // 선택지가 없는 줄에서 건너뛰기가 눌렸으면 바로 다음 줄로 (선택지 줄에서는 선택지를 띄운다)
                if (_skipRequested && !hasChoices)
                {
                    i = next;
                    continue;
                }

                // 선택지가 있는 줄에서 눌린 Esc는 선택지 앞에서 멈추는 것으로 이미 처리됐으니 지운다
                _skipRequested = false;
            }
            else
            {
                _bar.Hide();
            }

            if (hasChoices)
            {
                yield return ChooseRoutine(line);

                StoryChoice picked = line.choices[_choicePicked];
                FireChoice(line.id, picked.id, _choicePicked);

                // 선택지의 next가 있으면 그쪽으로, 없으면 줄의 next/바로 아래 줄로
                next = NextIndex(sequence, picked.next, next);
            }
            else
            {
                _advanceRequested = false;

                if (line.delay > 0f)
                {
                    // 자동 진행: delay 뒤에 넘어가고, 그 전에 입력해도 넘어간다
                    yield return WaitSeconds(line.delay, true);
                }
                else
                {
                    _bar.SetContinueVisible(true);
                    while (!_advanceRequested && !_skipRequested)
                        yield return null;
                }
            }

            i = next;
        }

        IsSkipping = false;

        // 이어질 시퀀스(then)가 있으면 재생 상태를 유지한 채 곧바로 이어 간다 (사이에 조작이 돌아가지 않는다)
        StorySequence follow = null;
        if (!string.IsNullOrEmpty(sequence.then))
            StoryDatabase.TryGetSequence(sequence.then, out follow);

        OnSequenceEnded?.Invoke(sequence.id, skippedAny);

        if (follow != null)
        {
            _advanceRequested = false;
            _skipRequested = false;
            _startFrame = Time.frameCount;
            yield return Run(follow, onComplete);
            yield break;
        }

        _bar.Hide();
        _bar.SetBlocking(false);
        GameInputLock.Unlock(this);

        IsPlaying = false;
        onComplete?.Invoke();
    }

    /// <summary>선택지를 띄우고 플레이어가 고를 때까지 기다린다. 결과는 _choicePicked.</summary>
    private IEnumerator ChooseRoutine(StoryLine line)
    {
        var texts = new List<string>(line.choices.Length);
        foreach (StoryChoice choice in line.choices)
            texts.Add(choice.text ?? "");

        _choiceCount = texts.Count;
        _choiceHighlight = 0;
        _choicePicked = -1;
        _bar.ShowChoices(texts);

        while (_choicePicked < 0)
            yield return null;

        _bar.HideChoices();
        _choiceCount = 0;
    }

    private IEnumerator WaitSeconds(float seconds, bool endOnAdvance)
    {
        if (seconds <= 0f)
            yield break;

        _advanceRequested = false;
        float end = Time.unscaledTime + seconds;

        while (Time.unscaledTime < end && !_skipRequested && !(endOnAdvance && _advanceRequested))
            yield return null;
    }

    /// <summary>줄/선택지의 next가 가리키는 줄 번호. 비어 있거나 찾지 못하면 fallback.</summary>
    private static int NextIndex(StorySequence sequence, string nextId, int fallback)
    {
        if (string.IsNullOrEmpty(nextId))
            return fallback;

        int index = sequence.IndexOf(nextId);
        return index >= 0 ? index : fallback;
    }

    private static int NextIndex(StorySequence sequence, StoryLine line, int fallback)
    {
        return NextIndex(sequence, line.next, fallback);
    }

    private void ShowLine(StoryLine line)
    {
        string speakerName = "";
        Color color = Color.white;
        bool rightSide = false;

        if (!string.IsNullOrEmpty(line.speaker))
        {
            if (StoryDatabase.TryGetSpeaker(line.speaker, out StorySpeaker speaker))
            {
                speakerName = string.IsNullOrEmpty(speaker.name) ? speaker.id : speaker.name;
                rightSide = speaker.IsRight;

                if (!string.IsNullOrEmpty(speaker.color) && !ColorUtility.TryParseHtmlString(speaker.color, out color))
                    color = Color.white;
            }
            else
            {
                Debug.LogWarning($"[Story] 정의되지 않은 화자 id '{line.speaker}' (Speakers.json 확인). id를 그대로 이름으로 표시합니다.");
                speakerName = line.speaker;
            }
        }

        _bar.Show(speakerName, color, line.text, rightSide);
    }

    private static void FireChoice(string lineId, string choiceId, int index)
    {
        if (OnChoice == null)
            return;

        foreach (Action<string, string, int> handler in OnChoice.GetInvocationList())
        {
            try
            {
                handler(lineId, choiceId, index);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    // 연출 쪽 코드의 예외가 재생(과 입력 잠금)을 멈추지 않도록 감싼다
    private static void FireCue(string cue)
    {
        if (string.IsNullOrEmpty(cue) || OnCue == null)
            return;

        // 한 줄에 cue를 세미콜론으로 여러 개 쓸 수 있다
        foreach (string single in cue.Split(';'))
        {
            string name = single.Trim();
            if (name.Length == 0)
                continue;

            foreach (Action<string> handler in OnCue.GetInvocationList())
            {
                try
                {
                    handler(name);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}
