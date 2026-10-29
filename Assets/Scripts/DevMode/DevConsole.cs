#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using FORGE3D;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// 개발자 모드 창. 게임 중 Shift+F로 열고 닫는다. 현재 적/플레이어 설정을 보여주고,
/// 아래 채팅 입력창에 명령(예: "enemy hp 3000")을 쳐서 값을 바꾼다. 명령 목록은 "help".
/// 씬에 배치할 필요 없이 게임 시작 시 스스로 생성되며(DontDestroyOnLoad), 에디터/개발 빌드에서만 존재한다.
/// 창이 열려 있는 동안 게임 입력(이동/발사/줌/UI 클릭)을 막고, 기본으로 게임을 일시정지한다.
/// </summary>
public class DevConsole : MonoBehaviour
{
    private const string InputControl = "DevConsoleInput";
    private const int WindowId = 7341;
    private const int MaxLogLines = 200;
    private const float StatusRefreshInterval = 0.25f;

    private static DevConsole _instance;

    private bool _isOpen;
    private bool _pauseWhileOpen = true;
    private bool _isPaused;
    private float _savedTimeScale = 1f;
    private EventSystem _disabledEventSystem;
    private int _toggleFrame = -10;

    // 좌상단 크레딧 HUD를 가리지 않도록 조금 아래에서 시작
    private Rect _windowRect = new Rect(20f, 90f, 560f, 640f);
    private Vector2 _logScroll;
    private string _input = "";
    private bool _focusInput;
    private bool _scrollToBottom;
    private readonly List<string> _log = new List<string>();
    private readonly List<string> _history = new List<string>();
    private int _historyIndex;

    private string _status = "";
    private float _nextStatusTime;

    private GUIStyle _statusStyle;
    private GUIStyle _logStyle;
    private GUIStyle _inputStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null)
            return;

        var go = new GameObject("DevConsole");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<DevConsole>();
    }

    private void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Print("<b>개발자 모드</b>  Shift+F로 열고 닫기 · 명령어 목록은 <b>help</b>");
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (_isOpen)
            Close();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 열려 있던 채로 씬이 바뀌면 일시정지/입력 차단을 풀어 둔다
        if (_isOpen)
            Close();

        // 재시작해도 바꿔 둔 플레이어 속도가 유지되도록 새 함선에 다시 적용한다
        DevTuning.ApplyToPlayer();
    }

    private void Update()
    {
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (shift && Input.GetKeyDown(KeyCode.F))
        {
            _toggleFrame = Time.frameCount;

            if (_isOpen)
                Close();
            else
                Open();
        }
    }

    // ── 열기 / 닫기 ────────────────────────────────────

    private void Open()
    {
        _isOpen = true;
        _focusInput = true;
        _scrollToBottom = true;
        _nextStatusTime = 0f;
        GameInputLock.Lock(this);

        SetPaused(_pauseWhileOpen);

        // 창 뒤의 상점 버튼 등이 클릭되지 않도록 UI 이벤트를 잠시 끈다
        if (EventSystem.current != null && EventSystem.current.enabled)
        {
            _disabledEventSystem = EventSystem.current;
            _disabledEventSystem.enabled = false;
        }
    }

    private void Close()
    {
        _isOpen = false;
        GameInputLock.Unlock(this);

        SetPaused(false);

        if (_disabledEventSystem != null)
            _disabledEventSystem.enabled = true;
        _disabledEventSystem = null;
    }

    private void SetPaused(bool paused)
    {
        if (paused == _isPaused)
            return;

        _isPaused = paused;

        // 우리가 멈춘 경우에만 원래 배속으로 되돌린다 (패배 화면 등이 걸어 둔 정지는 건드리지 않음)
        if (paused)
        {
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = _savedTimeScale;
        }
    }

    // ── 채팅 ──────────────────────────────────────────

    private void Print(string line)
    {
        _log.Add(line);
        if (_log.Count > MaxLogLines)
            _log.RemoveAt(0);

        _scrollToBottom = true;
    }

    private void Submit()
    {
        string line = _input.Trim();
        _input = "";
        _focusInput = true;

        if (line.Length == 0)
            return;

        Print($"<color=#9ECBFF>> {line}</color>");
        _history.Add(line);
        _historyIndex = _history.Count;

        string reply;
        string[] words = line.ToLowerInvariant().Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);

        switch (words[0])
        {
            case "pause":
                reply = HandlePause(words);
                break;
            case "reset":
                reply = HandleReset();
                break;
            case "story":
                reply = HandleStory(words);
                break;
            case "prologue":
                reply = HandlePrologue(words);
                break;
            case "save":
                reply = HandleSave(words);
                break;
            case "mission":
                reply = HandleMission(words);
                break;
            case "mainquest":
                reply = HandleMainQuest(words);
                break;
            case "ship":
                reply = HandleShip(words);
                break;
            case "clear":
                _log.Clear();
                reply = null;
                break;
            default:
                reply = DevCommands.Execute(line);
                break;
        }

        if (reply != null)
            Print(reply);
    }

    private string HandlePause(string[] words)
    {
        if (words.Length > 1 && words[1] != "on" && words[1] != "off")
            return DevCommands.Err("사용법: pause [on|off]");

        _pauseWhileOpen = words.Length > 1 ? words[1] == "on" : !_pauseWhileOpen;
        SetPaused(_pauseWhileOpen);

        return DevCommands.Ok(_pauseWhileOpen
            ? "창이 열려 있는 동안 게임을 멈춥니다."
            : "게임을 멈추지 않습니다. (입력은 계속 막힘)");
    }

    /// <summary>자동 저장 파일: save = 지금 상태 저장, save clear = 지우기(다음 플레이는 처음부터)</summary>
    private string HandleSave(string[] words)
    {
        if (words.Length > 1 && words[1] == "clear")
        {
            SaveSystem.Clear();
            return DevCommands.Ok("저장을 지웠습니다. 다음 플레이는 프롤로그부터 시작합니다.");
        }

        if (words.Length > 1)
            return DevCommands.Err("사용법: save / save clear");

        SaveSystem.Save(null);
        return DevCommands.Ok($"지금 상태를 저장했습니다. ({SaveSystem.FilePath})");
    }

    /// <summary>임무: mission list / mission start 번호 / mission stop</summary>
    private string HandleMainQuest(string[] words)
    {
        if (words.Length > 2 && words[1] == "stage" && int.TryParse(words[2], out int stage)
            && stage >= 0 && stage <= (int)FirstShipQuest.Stage.Done)
        {
            FirstShipQuest.DevSetStage((FirstShipQuest.Stage)stage);
            return DevCommands.Ok($"첫 번째 함선 퀘스트를 {(FirstShipQuest.Stage)stage} 단계로 옮겼습니다. (라운지의 라스 항목)");
        }

        if (words.Length > 1 && words[1] != "status")
            return DevCommands.Err("사용법: mainquest [status] / mainquest stage <0~6>  (0 연락 전, 1 제안, 2 광물, 3 합금, 4 코어, 5 조립, 6 완료)");

        FirstShipQuestData data = FirstShipQuest.Data;
        var sb = new StringBuilder($"첫 번째 함선 퀘스트: {FirstShipQuest.Current}");
        if (data != null)
        {
            foreach (FirstShipQuestData.Material m in data.materials)
                sb.Append($"\n  {OreCatalog.NameOf(m.oreId)} {FirstShipQuest.DeliveredOf(m.oreId)} / {m.amount}");
            sb.Append($"\n  {data.alloyName} {FirstShipQuest.Alloys} / {data.alloyRequired} · {data.coreName} {(FirstShipQuest.HasCore ? 1 : 0)} / 1 · 통신 기록 {(FirstShipQuest.HasCommsLog ? "있음" : "없음")}");
        }

        return sb.ToString();
    }

    private string HandleShip(string[] words)
    {
        if (words.Length > 2 && (words[1] == "grant" || words[1] == "switch"))
        {
            bool ok = words[1] == "grant" ? PlayerShips.Grant(words[2]) : PlayerShips.Switch(words[2]);
            return ok ? DevCommands.Ok($"함선: {PlayerShips.CurrentId}") : DevCommands.Err($"'{words[2]}' 함선을 주거나 탈 수 없습니다.");
        }

        return $"함선: {PlayerShips.CurrentId} (보유: {string.Join(", ", PlayerShips.OwnedIds)})\n사용법: ship grant <id> / ship switch <id>";
    }

    private string HandleMission(string[] words)
    {
        string sub = words.Length > 1 ? words[1] : "list";

        if (sub == "list")
        {
            var sb = new StringBuilder("임무:");
            foreach (MissionData m in MissionDirector.AllMissions())
                sb.Append($"\n  {m.id}  {m.title} ({m.tier}) · {m.Summary} · {m.reward:N0} CR");
            if (MissionDirector.IsRunning)
                sb.Append("\n(진행 중인 임무가 있습니다 — mission stop)");
            return sb.ToString();
        }

        if (sub == "stop")
        {
            if (!MissionDirector.IsRunning)
                return DevCommands.Err("진행 중인 임무가 없습니다.");

            MissionDirector.Abort();
            return DevCommands.Ok("임무를 중단했습니다. (임무 함선들을 치움)");
        }

        if (sub == "start" && words.Length > 2)
        {
            // 창이 열려 있으면 수락 대사가 가려지고 입력도 막히니, 닫고 시작한다
            if (!MissionDirector.TryStart(words[2], out string error))
                return DevCommands.Err(error);

            Close();
            return null;
        }

        return DevCommands.Err("사용법: mission list / mission start <번호> (예: 1-1) / mission stop");
    }

    /// <summary>다음 플레이부터 게임 시작 프롤로그를 재생할지 정한다 (PlayerPrefs에 저장되어 에디터를 껐다 켜도 유지).</summary>
    private string HandlePrologue(string[] words)
    {
        bool skipNow = PlayerPrefs.GetInt(PrologueDirector.SkipPrefKey, 0) == 1;

        if (words.Length < 2)
            return DevCommands.Ok($"프롤로그는 현재 {(skipNow ? "꺼짐(건너뜀)" : "켜짐")}입니다. 바꾸려면: prologue on / prologue off");

        if (words[1] != "on" && words[1] != "off")
            return DevCommands.Err("사용법: prologue on|off");

        bool play = words[1] == "on";
        PlayerPrefs.SetInt(PrologueDirector.SkipPrefKey, play ? 0 : 1);
        PlayerPrefs.Save();

        return DevCommands.Ok(play
            ? "다음 플레이부터 게임 시작 때 프롤로그를 재생합니다."
            : "다음 플레이부터 프롤로그를 건너뛰고 바로 게임을 시작합니다.");
    }

    private string HandleStory(string[] words)
    {
        string sub = words.Length > 1 ? words[1] : "";

        if (sub == "list")
            return "스토리 시퀀스: " + string.Join(", ", StoryDatabase.SequenceIds);

        if (sub == "reload")
        {
            StoryDatabase.Reload();
            return DevCommands.Ok("스토리 JSON을 다시 읽었습니다. 시퀀스: " + string.Join(", ", StoryDatabase.SequenceIds));
        }

        // story play <id>  /  story <id>
        string id = sub == "play" ? (words.Length > 2 ? words[2] : "") : sub;
        if (id.Length == 0)
            return DevCommands.Err("사용법: story play <id>  /  story list  /  story reload");

        if (!StoryDatabase.TryGetSequence(id, out _))
            return DevCommands.Err($"'{id}' 시퀀스가 없습니다. (story list)");

        // 창이 열려 있으면 가려지고 입력도 막히니, 닫고 재생한다
        Close();
        StoryPlayer.Play(id);
        return null;
    }

    private string HandleReset()
    {
        DevTuning.Clear();
        Print(DevCommands.Ok("바꾼 값을 모두 되돌리고 씬을 다시 시작합니다."));

        Close();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        return null;
    }

    private void MoveHistory(int step)
    {
        if (_history.Count == 0)
            return;

        _historyIndex = Mathf.Clamp(_historyIndex + step, 0, _history.Count);
        _input = _historyIndex < _history.Count ? _history[_historyIndex] : "";
    }

    // ── 화면 ──────────────────────────────────────────

    private void OnGUI()
    {
        Event e = Event.current;

        // Shift+F는 키 이벤트와 별도로 문자 이벤트('F')도 오기 때문에, 방금 토글한 직후 것은 삼켜서 입력창에 F가 찍히지 않게 한다
        if (e.type == EventType.KeyDown && e.character == 'F' && Time.frameCount - _toggleFrame <= 1)
            e.Use();

        if (!_isOpen)
            return;

        EnsureStyles();

        // 레이아웃이 바뀌지 않도록 상태 문구는 Layout 이벤트에서만 갱신한다
        if (e.type == EventType.Layout && Time.unscaledTime >= _nextStatusTime)
        {
            _status = BuildStatus();
            _nextStatusTime = Time.unscaledTime + StatusRefreshInterval;
        }

        // 고해상도에서도 글자가 작아지지 않도록 화면 높이에 비례해 키운다
        float scale = Mathf.Max(1f, Screen.height / 900f);
        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        _windowRect.width = Mathf.Min(_windowRect.width, Screen.width / scale);
        _windowRect.height = Mathf.Min(_windowRect.height, Screen.height / scale);
        _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, Screen.width / scale - _windowRect.width);
        _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, Screen.height / scale - _windowRect.height);

        _windowRect = GUI.Window(WindowId, _windowRect, DrawWindow, "개발자 모드   (Shift+F: 닫기)");

        GUI.matrix = oldMatrix;
    }

    private void DrawWindow(int id)
    {
        Event e = Event.current;

        GUILayout.Label(_status, _statusStyle);

        // 채팅 로그
        if (_scrollToBottom)
        {
            _logScroll.y = float.MaxValue;
            if (e.type == EventType.Repaint)
                _scrollToBottom = false;
        }

        _logScroll = GUILayout.BeginScrollView(_logScroll, GUI.skin.box, GUILayout.ExpandHeight(true));
        foreach (string line in _log)
            GUILayout.Label(line, _logStyle);
        GUILayout.EndScrollView();

        // Enter / ↑ / ↓ 는 TextField가 먹기 전에 먼저 처리한다
        if (e.type == EventType.KeyDown && GUI.GetNameOfFocusedControl() == InputControl)
        {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                Submit();
                e.Use();
            }
            else if (e.keyCode == KeyCode.UpArrow)
            {
                MoveHistory(-1);
                e.Use();
            }
            else if (e.keyCode == KeyCode.DownArrow)
            {
                MoveHistory(1);
                e.Use();
            }
        }

        GUILayout.BeginHorizontal();
        GUI.SetNextControlName(InputControl);
        _input = GUILayout.TextField(_input, 200, _inputStyle, GUILayout.Height(28f));
        if (GUILayout.Button("전송", GUILayout.Width(70f), GUILayout.Height(28f)))
            Submit();
        GUILayout.EndHorizontal();

        if (_focusInput && e.type == EventType.Repaint)
        {
            GUI.FocusControl(InputControl);
            _focusInput = false;
        }

        GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
    }

    private void EnsureStyles()
    {
        if (_statusStyle != null)
            return;

        _statusStyle = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, fontSize = 13 };
        _logStyle = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, fontSize = 13 };
        _inputStyle = new GUIStyle(GUI.skin.textField) { fontSize = 14, alignment = TextAnchor.MiddleLeft };
    }

    // ── 현재 설정 표시 ─────────────────────────────────

    private string BuildStatus()
    {
        var sb = new StringBuilder();

        PirateShip[] enemies = FindObjectsOfType<PirateShip>();
        sb.Append("<b>[적]</b>  ").Append($"살아있는 해적선 {enemies.Length}기");

        if (enemies.Length > 0)
            AppendEnemyDetails(sb, enemies[0].gameObject);
        else
            sb.Append("\n(살아있는 적이 없어 상세 값을 표시할 수 없습니다)");

        var player = FindObjectOfType<SpaceshipController>();
        sb.Append("\n<b>[플레이어]</b>  ");
        if (player != null)
        {
            var hull = player.GetComponentInChildren<HullHealth>();
            var shield = player.GetComponentInChildren<ShieldHealth>();

            sb.Append($"최고 속도 {DevCommands.F(player.MaxSpeed)} · 가속도 {DevCommands.F(player.Acceleration)}");
            if (hull != null)
                sb.Append($"\n체력 {DevCommands.F(hull.CurrentHp)}/{DevCommands.F(hull.MaxHp)}");
            if (shield != null)
                sb.Append($" · 쉴드 {DevCommands.F(shield.CurrentHp)}/{DevCommands.F(shield.MaxHp)}");
            sb.Append($" · 무적 {(DevTuning.PlayerGod ? "켜짐" : "꺼짐")}");
        }
        else
        {
            sb.Append("함선 없음");
        }

        if (CreditWallet.Instance != null)
            sb.Append($"\n<b>[크레딧]</b>  {CreditWallet.Instance.Amount}");

        sb.Append($"\n<b>[창]</b>  일시정지 {(_pauseWhileOpen ? "켜짐" : "꺼짐")} (pause 명령으로 전환)");
        return sb.ToString();
    }

    private static void AppendEnemyDetails(StringBuilder sb, GameObject enemy)
    {
        var hull = enemy.GetComponentInChildren<HullHealth>();
        var shield = enemy.GetComponentInChildren<ShieldHealth>();
        var turrets = enemy.GetComponentsInChildren<EnemyTurretController>();

        sb.Append("\n체력 ").Append(hull != null ? DevCommands.F(hull.MaxHp) : "-");
        sb.Append(" · 쉴드 ").Append(shield != null ? DevCommands.F(shield.MaxHp) : "-");
        if (turrets.Length > 0)
        {
            sb.Append("\n공격 간격 ").Append(DevCommands.F(turrets[0].fireInterval)).Append("초");
            sb.Append(" · 사거리 ").Append(DevCommands.F(turrets[0].fireRange));
        }

        // 적이 실제로 쓰는 무기별 공격력 (개발자 모드로 덮어쓴 값 반영)
        var seen = new HashSet<F3DFXType>();
        var weapons = new StringBuilder();
        foreach (var turret in turrets)
        {
            if (turret.fxController == null || !seen.Add(turret.fxController.DefaultFXType))
                continue;

            F3DFXType type = turret.fxController.DefaultFXType;
            if (weapons.Length > 0)
                weapons.Append(", ");
            weapons.Append($"{type} {DevCommands.F(WeaponStatsTable.GetDamage(type, WeaponOwner.Enemy))}");
        }

        if (weapons.Length > 0)
            sb.Append("\n무기 공격력: ").Append(weapons);
    }
}
#endif
