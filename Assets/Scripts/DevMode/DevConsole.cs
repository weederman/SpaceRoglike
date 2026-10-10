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
    private EnemySpawner _spawner;

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
        DevTuning.InputBlocked = true;

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
        DevTuning.InputBlocked = false;

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
        if (_spawner == null)
            _spawner = FindObjectOfType<EnemySpawner>();

        var sb = new StringBuilder();

        sb.Append("<b>[적]</b>  ");
        GameObject sample = null;

        if (_spawner == null)
        {
            sb.Append("EnemySpawner 없음");
        }
        else
        {
            sb.Append($"생존 {CountAlive(_spawner, out sample)}/{_spawner.MaxAliveCount} · 리스폰 {DevCommands.F(_spawner.RespawnDelay)}초");
        }

        if (sample != null)
            AppendEnemyDetails(sb, sample);
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

    private static int CountAlive(EnemySpawner spawner, out GameObject first)
    {
        first = null;
        int count = 0;

        foreach (var enemy in spawner.AliveEnemies)
        {
            if (enemy == null)
                continue;

            count++;
            if (first == null)
                first = enemy;
        }

        return count;
    }

    private static void AppendEnemyDetails(StringBuilder sb, GameObject enemy)
    {
        var hull = enemy.GetComponentInChildren<HullHealth>();
        var shield = enemy.GetComponentInChildren<ShieldHealth>();
        var move = enemy.GetComponentInChildren<ShipMovementBase>();
        var orbit = move as EnemyOrbitController;
        var turrets = enemy.GetComponentsInChildren<EnemyTurretController>();

        sb.Append("\n체력 ").Append(hull != null ? DevCommands.F(hull.MaxHp) : "-");
        sb.Append(" · 쉴드 ").Append(shield != null ? DevCommands.F(shield.MaxHp) : "-");
        sb.Append(" · 최고 속도 ").Append(move != null ? DevCommands.F(move.MaxSpeed) : "-");
        sb.Append(" · 가속도 ").Append(move != null ? DevCommands.F(move.Acceleration) : "-");

        sb.Append("\n공전 거리 ").Append(orbit != null ? DevCommands.F(orbit.OrbitDistance) : "-");
        if (turrets.Length > 0)
        {
            sb.Append(" · 공격 간격 ").Append(DevCommands.F(turrets[0].fireInterval)).Append("초");
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
