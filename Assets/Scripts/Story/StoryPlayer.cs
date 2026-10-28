using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 대사 시퀀스(StoryDatabase)를 한 줄씩 나레이션바(NarrationBar)에 재생한다.
/// 어디서든 StoryPlayer.Play("prologue", 끝났을때할일) 한 줄로 시작하고, 씬에 따로 배치할 필요는 없다(자동 생성).
///
/// 조작: 클릭 / Space / Enter = 타자 효과 건너뛰기 → 다음 줄,  Esc = 건너뛰기(다음 선택지 또는 끝까지 빨리 감기).
/// 조작하며 듣는 시퀀스(control)에서는 대사창 위를 클릭했을 때만 넘어간다 (다른 곳 클릭은 발사).
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

    /// <summary>지금 대사가 기다리고 있는 신호 이름 (waitFor). 기다리는 중이 아니면 null.</summary>
    public static string WaitingFor { get; private set; }

    /// <summary>
    /// "조작하며 듣는" 시퀀스(control: true)를 재생 중인지. 이때는 조작이 잠기지 않고,
    /// 대사는 시간, 신호, 대사창 위 클릭으로 넘어간다 (다른 곳 클릭은 발사 등 게임 입력이라 대사를 넘기지 않는다).
    /// </summary>
    public static bool IsControlMode { get; private set; }

    // control 시퀀스에서 delay가 없는 대사가 화면에 머무는 시간 = 기본 + 글자당 (읽을 시간)
    private const float ControlLineBaseSeconds = 1.6f;
    private const float ControlLineSecondsPerChar = 0.06f;

    private readonly HashSet<string> _signals = new HashSet<string>();

    /// <summary>waitFor로 기다리는 줄에 "됐다"고 알린다. 아직 그 줄에 도달하기 전이어도 기억해 두었다가 도달하면 바로 넘어간다.</summary>
    public static void Signal(string name)
    {
        if (_instance != null && !string.IsNullOrEmpty(name))
            _instance._signals.Add(name);
    }

    private const string BarPrefabPath = "UI/NarrationBar";
    private const string BackdropPrefabPath = "UI/StoryBackdrop";
    private const string QuestPrefabPath = "UI/QuestTracker";
    private const string TipsPrefabPath = "UI/TutorialTips";
    private const string RadarPrefabPath = "UI/RadarPing";
    private const string CueDirectorFolder = "Directors/Cue";

    private static StoryPlayer _instance;

    private NarrationBar _bar;
    private bool _advanceRequested;
    private bool _skipRequested;
    private int _startFrame;
    private StorySequence _pendingAside;

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

    /// <summary>
    /// 조작하며 듣는(control) 시퀀스 도중에 짧은 대사(예: 오르비스의 안내)를 끼워 넣는다.
    /// 본 대사가 흐르는 중이면 끊지 않고, 플레이어의 행동(waitFor)을 기다리는 순간에 보여준 뒤 기다리던 줄로 돌아간다.
    /// control 시퀀스가 아니거나 이미 예약된 것이 있으면 false.
    /// </summary>
    public static bool PlayAside(string sequenceId)
    {
        if (_instance == null || !IsPlaying || !IsControlMode || _instance._pendingAside != null)
            return false;

        if (!StoryDatabase.TryGetSequence(sequenceId, out StorySequence aside))
        {
            Debug.LogWarning($"[Story] '{sequenceId}' 시퀀스를 찾지 못했습니다. (Assets/Resources/Story/*.json)");
            return false;
        }

        _instance._pendingAside = aside;
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
        IsControlMode = false;
        WaitingFor = null;
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
        InstantiateOptional<TutorialTips>(TipsPrefabPath);
        InstantiateOptional<RadarPing>(RadarPrefabPath);

        // 장면별 연출 담당(cue를 받아 화면 연출을 하는 프리팹)은 Resources/Directors/Cue 폴더에 두면 자동으로 생긴다
        foreach (GameObject director in Resources.LoadAll<GameObject>(CueDirectorFolder))
            Instantiate(director, transform);
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
            IsControlMode = false;
        }
    }

    /// <summary>control 시퀀스면 조작 잠금과 화면 클릭 차단을 풀고, 아니면 다시 건다.</summary>
    private void SetControlMode(bool control)
    {
        IsControlMode = control;
        _bar.SetBlocking(!control);
        _pendingAside = null; // 끼워 넣을 대사는 그 control 시퀀스 안에서만 의미가 있다

        if (control)
        {
            GameInputLock.Unlock(this);
            _skipRequested = false;
            _advanceRequested = false;
        }
        else
        {
            GameInputLock.Lock(this);
        }
    }

    private void Begin(StorySequence sequence, Action onComplete)
    {
        IsPlaying = true;
        IsSkipping = false;
        WaitingFor = null;
        _signals.Clear();
        _pendingAside = null;
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

        if (IsControlMode)
        {
            HandleControlModeInput();
            return;
        }

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

    /// <summary>
    /// control 시퀀스: 클릭/Space/Esc는 게임 조작으로 쓰이므로 대사창 위를 클릭했을 때만 넘긴다.
    /// waitFor로 플레이어의 행동을 기다리는 줄은 클릭으로 넘어가지 않는다(문장만 전부 보여준다).
    /// </summary>
    private void HandleControlModeInput()
    {
        if (GameInputLock.IsLockedExcept(this) || Time.frameCount == _startFrame)
            return;

        if (!Input.GetMouseButtonDown(0) || !_bar.IsPointerOverPanel(Input.mousePosition))
            return;

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

        SetControlMode(sequence.control);

        while (i >= 0 && i < lines.Length)
        {
            StoryLine line = lines[i];
            bool hasChoices = line.choices != null && line.choices.Length > 0;
            bool hasText = !string.IsNullOrEmpty(line.text);

            if (hasChoices && sequence.control)
            {
                Debug.LogWarning($"[Story] control 시퀀스 '{sequence.id}'에는 선택지를 쓸 수 없어 무시합니다.");
                hasChoices = false;
            }

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

                if (!string.IsNullOrEmpty(line.waitFor))
                    yield return WaitForSignal(line);

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

                if (!string.IsNullOrEmpty(line.waitFor))
                {
                    // 플레이어가 실제로 뭔가를 해서 신호가 올 때까지 (클릭으로는 넘어가지 않는다)
                    yield return WaitForSignal(line);
                }
                else if (line.delay > 0f)
                {
                    // 자동 진행: delay 뒤에 넘어가고, 그 전에 입력해도 넘어간다
                    yield return WaitSeconds(line.delay, true);
                }
                else if (sequence.control)
                {
                    // 조작 중에는 읽을 시간만큼 보여주고 넘어간다. 그 전에 대사창을 클릭하면 바로 넘어간다.
                    float seconds = ControlLineBaseSeconds + line.text.Length * ControlLineSecondsPerChar;
                    _bar.SetContinueVisible(true);
                    yield return WaitSeconds(seconds, true);
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

        IsControlMode = false;
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

    /// <summary>줄의 waitFor 신호(Signal)가 올 때까지, 또는 건너뛰기가 눌릴 때까지 기다린다. 그동안 예약된 끼워 넣을 대사가 있으면 보여준다.</summary>
    private IEnumerator WaitForSignal(StoryLine line)
    {
        string signal = line.waitFor;
        WaitingFor = signal;
        _bar.SetContinueVisible(false);

        // Remove는 신호가 있었으면 지우면서 true를 돌려준다
        while (!_signals.Remove(signal) && !_skipRequested)
        {
            if (_pendingAside != null)
                yield return RunAside(line);
            else
                yield return null;
        }

        WaitingFor = null;
    }

    /// <summary>예약된 끼워 넣을 대사를 보여주고, 끝나면 기다리던 줄(없으면 숨김)로 돌아간다.</summary>
    private IEnumerator RunAside(StoryLine waitingLine)
    {
        StorySequence aside = _pendingAside;
        _pendingAside = null;

        foreach (StoryLine line in aside.lines ?? new StoryLine[0])
        {
            if (_skipRequested)
                break;

            FireCue(line.cue);
            if (string.IsNullOrEmpty(line.text))
                continue;

            ShowLine(line);
            while (_bar.IsTyping && !_skipRequested)
                yield return null;

            float seconds = line.delay > 0f ? line.delay : ControlLineBaseSeconds + line.text.Length * ControlLineSecondsPerChar;
            _bar.SetContinueVisible(true);
            yield return WaitSeconds(seconds, true);
        }

        if (string.IsNullOrEmpty(waitingLine.text))
        {
            _bar.Hide();
        }
        else
        {
            ShowLine(waitingLine);
            _bar.CompleteTyping();
        }

        _bar.SetContinueVisible(false);
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
