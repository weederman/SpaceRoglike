using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 임시 엔딩 연출 담당. 아이언월 조립 대사(first_ship_assembly) → 라스의 통신(remaining_doubts_call, 퀘스트 「남겨진 의문」)에
/// then으로 이어지는 "ending_temp" 시퀀스의 cue를 받는다. 그 첫 줄에서 통신창(대사 바)이 천천히 사라진 뒤:
///
///  ending_fly       화면을 잠깐 어둡게 바꾸고, 함선(아이언월)이 푸른 엔진 빛을 뿜으며 별이 가득한 우주를 옆으로 가로지르는 장면을 보여준다.
///                   정거장 화면·HUD는 숨긴다. 함선은 이 뒤로도 계속 앞으로 나아간다.
///  ending_pullback  카메라가 함선에서 멀어지며 위로 올라가 우주 배경을 보여준다
///  ending_black     화면이 서서히 어두워진다
///  ending_title     가운데에 로고와 엔딩 문구가 하나씩 떠오른다. 아무 키나 누르면 정거장 화면으로 돌아온다 (함선은 도킹한 자리로).
///
/// 엔딩 문구는 Resources/Story/Ending.json의 "ending_temp_text" 시퀀스에서 읽는다 (줄 id: title / body1~3 / footer / hint).
/// 각 단계가 끝나면 스토리에 신호(ending_fly_done 등, JSON의 waitFor)를 보낸다. cue는 큐에 쌓아 한 번에 하나씩 실행한다.
/// Esc로 건너뛰면 남은 연출을 생략하고 마지막 상태(로고와 엔딩 문구 화면)로 바로 가며, 아무 키나 누르면 돌아온다.
/// 연출 중에는 플레이어 함선을 도킹 내부 공간(항성계 밖이라 행성·그리드가 없다)에서 움직이고, 끝나면 제자리로 돌려놓는다.
/// 프리팹: Assets/Resources/Directors/Cue/EndingDirector.prefab
/// </summary>
public class EndingDirector : MonoBehaviour
{
    private const string SequenceId = "ending_temp";
    private const string TextSequenceId = "ending_temp_text";
    private const string CuePrefix = "ending_";
    private const string FlyCue = "ending_fly";
    private const string PullbackCue = "ending_pullback";
    private const string BlackCue = "ending_black";
    private const string TitleCue = "ending_title";

    [Header("UI (프리팹에서 연결)")]
    [SerializeField] private Canvas _canvas;
    [Tooltip("화면 전체를 덮는 검은 판")]
    [SerializeField] private Image _black;
    [SerializeField] private CanvasGroup _titleGroup;
    [SerializeField] private CanvasGroup _logo;
    [SerializeField] private CanvasGroup _divider;
    [Tooltip("엔딩 문구 줄 (title, body×3, footer 순서). JSON 줄 id로 짝을 맞춘다")]
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text[] _bodyTexts;
    [SerializeField] private TMP_Text _footerText;
    [SerializeField] private TMP_Text _hintText;

    [Header("엔진 빛")]
    [Tooltip("함선 뒤에 붙이는 푸른 엔진 빛 (파티클 + 조명). 크기 1 = 함선 길이 기준값")]
    [SerializeField] private GameObject _engineGlowPrefab;
    [Tooltip("엔진 빛 프리팹을 만들 때 기준으로 한 함선 길이")]
    [SerializeField, Min(1f)] private float _engineGlowReferenceLength = 40f;
    [Tooltip("지나갈 때 내는 소리 (SoundTable 이름, 비우면 없음)")]
    [SerializeField] private string _flybySound = "ending_flyby";

    [Header("장면 전환")]
    [SerializeField, Min(0.05f)] private float _switchFadeSeconds = 0.7f;

    [Header("함선 항해 (거리는 함선 길이 배수)")]
    [Tooltip("화면을 가로지르는 데 걸리는 시간 (이 시간이 지나면 다음 단계로, 함선은 계속 나아간다)")]
    [SerializeField, Min(1f)] private float _crossSeconds = 7f;
    [Tooltip("이 시간 동안 가로지르는 거리 (함선 길이 배수)")]
    [SerializeField, Min(1f)] private float _crossDistance = 4f;
    [Tooltip("처음 카메라 위치 (함선 경로 가운데 기준, 함선 길이 배수. +X가 함선 진행 방향)")]
    [SerializeField] private Vector3 _cameraOffset = new Vector3(0f, 0.55f, -1.7f);

    [Header("카메라 멀어짐")]
    [SerializeField, Min(0.5f)] private float _pullbackSeconds = 6f;
    [Tooltip("멀어진 뒤 카메라 위치 (함선 기준, 함선 길이 배수)")]
    [SerializeField] private Vector3 _pullbackOffset = new Vector3(-3f, 5f, -14f);

    [Header("암전 / 로고")]
    [SerializeField, Min(0.1f)] private float _blackSeconds = 2.5f;
    [SerializeField, Min(0.1f)] private float _logoFadeSeconds = 1.5f;
    [Tooltip("문구가 한 줄씩 떠오르는 시간")]
    [SerializeField, Min(0.1f)] private float _lineFadeSeconds = 0.9f;
    [Tooltip("다음 줄이 뜨기까지의 간격")]
    [SerializeField, Min(0f)] private float _lineGapSeconds = 0.6f;
    [Tooltip("모든 문구가 뜬 뒤 '아무 키나 눌러 계속'이 뜨기까지")]
    [SerializeField, Min(0f)] private float _hintDelaySeconds = 1.5f;
    [SerializeField, Min(0.1f)] private float _closeFadeSeconds = 1.2f;

    private readonly Queue<string> _queue = new Queue<string>();
    private readonly List<Canvas> _hiddenCanvases = new List<Canvas>();
    private Coroutine _pump;

    private bool _active;
    private bool _sceneSet;
    private bool _followShip;
    private bool _finalShown;
    private Transform _ship;
    private Rigidbody _rb;
    private Camera _cam;
    private CameraController _cameraController;
    private Vector3 _savedPosition;
    private Quaternion _savedRotation;
    private GameObject _engineGlow;
    private float _shipLength;
    private Vector3 _pathMid;
    private float _speed;

    private void OnEnable()
    {
        StoryPlayer.OnCue += HandleCue;
        StoryPlayer.OnSequenceEnded += HandleEnded;
    }

    private void OnDisable()
    {
        StoryPlayer.OnCue -= HandleCue;
        StoryPlayer.OnSequenceEnded -= HandleEnded;
        GameInputLock.Unlock(this);
    }

    private void Awake()
    {
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            GameFonts.ApplyKorean(text);

        _canvas.gameObject.SetActive(false);
    }

    // ── 큐 ────────────────────────────────────────────

    private void HandleCue(string cue)
    {
        // 건너뛰는 중에 지나가는 cue는 무시하고, 시퀀스가 끝날 때(HandleEnded) 마지막 상태로 간다
        if (!cue.StartsWith(CuePrefix) || StoryPlayer.IsSkipping)
            return;

        _queue.Enqueue(cue);
        if (_pump == null)
            _pump = StartCoroutine(Pump());
    }

    private void HandleEnded(string sequenceId, bool skipped)
    {
        if (sequenceId != SequenceId || !skipped)
            return;

        // 시퀀스가 끝나 스토리의 조작 잠금이 풀렸으므로, 화면을 닫을 때까지는 이 연출이 잠근다
        GameInputLock.Lock(this);
        if (_finalShown)
            return;

        // 건너뛰기: 진행 중인 연출을 멈추고 로고 화면으로 바로 간다
        StopAllCoroutines();
        _pump = null;
        _queue.Clear();
        StartCoroutine(SkipToFinal());
    }

    private IEnumerator Pump()
    {
        while (_queue.Count > 0)
        {
            string cue = _queue.Dequeue();
            switch (cue)
            {
                case FlyCue:
                    yield return StepFly();
                    StoryPlayer.Signal("ending_fly_done");
                    break;
                case PullbackCue:
                    yield return StepPullback();
                    StoryPlayer.Signal("ending_pullback_done");
                    break;
                case BlackCue:
                    yield return FadeBlack(1f, _blackSeconds);
                    StoryPlayer.Signal("ending_black_done");
                    break;
                case TitleCue:
                    yield return StepTitle(false);
                    StoryPlayer.Signal("ending_closed");
                    break;
            }
        }

        _pump = null;
    }

    // ── 단계 ──────────────────────────────────────────

    private IEnumerator StepFly()
    {
        Begin();
        yield return FadeBlack(1f, _switchFadeSeconds);

        SetUpScene();
        if (!string.IsNullOrEmpty(_flybySound))
            GameAudio.Play(_flybySound);

        yield return FadeBlack(0f, _switchFadeSeconds);

        // 함선은 Update에서 계속 나아간다. 가로지르는 시간 중 남은 만큼 기다린다.
        float wait = Mathf.Max(0f, _crossSeconds - _switchFadeSeconds);
        for (float t = 0f; t < wait; t += Time.unscaledDeltaTime)
            yield return null;
    }

    private IEnumerator StepPullback()
    {
        if (!_sceneSet)
            yield break;

        _followShip = true;
        Vector3 from = _cam.transform.position;
        for (float t = 0f; t < _pullbackSeconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / _pullbackSeconds);
            Vector3 to = _ship.position + _pullbackOffset * _shipLength;
            _cam.transform.position = Vector3.Lerp(from, to, k);
            yield return null;
        }
    }

    /// <summary>로고와 문구를 띄우고 아무 키나 누를 때까지 기다린 뒤 정거장 화면으로 돌아온다.</summary>
    private IEnumerator StepTitle(bool instant)
    {
        _finalShown = true;
        SetBlack(1f);
        _canvas.gameObject.SetActive(true);
        _titleGroup.alpha = 1f;

        var groups = new List<CanvasGroup> { _logo };
        foreach (TMP_Text text in OrderedTexts())
            groups.Add(GroupOf(text));
        groups.Insert(groups.Count - 1, _divider); // 구분선은 마지막 줄(footer) 바로 위

        foreach (CanvasGroup g in groups)
            g.alpha = instant ? 1f : 0f;
        GroupOf(_hintText).alpha = 0f;

        if (!instant)
        {
            yield return FadeGroup(_logo, 1f, _logoFadeSeconds);
            for (int i = 1; i < groups.Count; i++)
            {
                yield return Wait(_lineGapSeconds);
                yield return FadeGroup(groups[i], 1f, _lineFadeSeconds);
            }
        }

        yield return Wait(_hintDelaySeconds);
        StartCoroutine(FadeGroup(GroupOf(_hintText), 1f, _lineFadeSeconds));

        // 건너뛰기(Esc)를 누른 그 프레임은 넘긴다
        yield return null;
        while (!Input.anyKeyDown)
            yield return null;

        yield return FadeGroup(_titleGroup, 0f, _closeFadeSeconds);
        Restore();
        yield return FadeBlack(0f, _closeFadeSeconds);
        _canvas.gameObject.SetActive(false);
        End();
    }

    private IEnumerator SkipToFinal()
    {
        Begin();
        if (_sceneSet)
            StopShip();
        yield return StepTitle(true);
    }

    // ── 장면 ──────────────────────────────────────────

    /// <summary>
    /// 정거장 화면·HUD 등 다른 화면 UI를 숨긴다 (이미 했으면 건너뜀).
    /// 조작은 재생 중인 스토리가 잠그고 있다. 여기서 따로 잠그면 스토리가 Esc(건너뛰기)를 받지 못한다.
    /// </summary>
    private void Begin()
    {
        if (_active)
            return;

        _active = true;
        _finalShown = false;
        _canvas.gameObject.SetActive(true);
        _titleGroup.alpha = 0f;
        GroupOf(_hintText).alpha = 0f;
        SetBlack(0f);

        foreach (Canvas canvas in FindObjectsOfType<Canvas>())
        {
            if (canvas.isRootCanvas && canvas.enabled && canvas != _canvas)
            {
                canvas.enabled = false;
                _hiddenCanvases.Add(canvas);
            }
        }
    }

    /// <summary>함선을 경로 시작점에 놓고 카메라를 넘겨받는다 (화면이 검을 때 부른다)</summary>
    private void SetUpScene()
    {
        SpaceshipController ship = FindObjectOfType<SpaceshipController>();
        if (ship == null)
            return;

        _ship = ship.transform;
        _rb = ship.GetComponent<Rigidbody>();
        _savedPosition = _ship.position;
        _savedRotation = _ship.rotation;

        _cam = Camera.main;
        _cameraController = FindObjectOfType<CameraController>();
        if (_cameraController != null)
            _cameraController.enabled = false;

        Quaternion facing = Quaternion.LookRotation(Vector3.right);
        MoveShip(_savedPosition, facing);
        _shipLength = Mathf.Max(1f, ShipBounds(out Vector3 center, out Vector3 extents) * 2f);

        _speed = _crossDistance * _shipLength / _crossSeconds;
        _pathMid = _savedPosition;
        MoveShip(_pathMid - Vector3.right * (_crossDistance * _shipLength * 0.5f), facing);

        if (_engineGlowPrefab != null)
        {
            // 함선 뒤 끝(진행 방향 반대쪽)에 붙인다
            ShipBounds(out center, out extents);
            Vector3 stern = center - _ship.forward * Vector3.Dot(extents, Abs(_ship.forward));
            _engineGlow = Instantiate(_engineGlowPrefab, stern, _ship.rotation, _ship);
            _engineGlow.transform.localScale = Vector3.one * (_shipLength / _engineGlowReferenceLength);
        }

        _cam.transform.position = _pathMid + _cameraOffset * _shipLength;
        _cam.transform.rotation = Quaternion.LookRotation(_pathMid - _cam.transform.position);
        _sceneSet = true;
    }

    private void Update()
    {
        if (!_sceneSet || _ship == null)
            return;

        MoveShip(_ship.position + _ship.forward * (_speed * Time.unscaledDeltaTime), _ship.rotation);

        // 처음에는 경로 가운데를 보고(함선이 화면을 가로지른다), 멀어질 때는 함선을 따라 본다
        if (_followShip && _cam != null)
            _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation,
                Quaternion.LookRotation(_ship.position - _cam.transform.position), 2f * Time.unscaledDeltaTime);
    }

    private void StopShip()
    {
        _sceneSet = false;
    }

    /// <summary>함선·카메라·숨긴 UI를 원래대로 돌려놓는다 (화면이 검을 때 부른다)</summary>
    private void Restore()
    {
        _sceneSet = false;
        _followShip = false;

        if (_engineGlow != null)
            Destroy(_engineGlow);
        _engineGlow = null;

        if (_ship != null)
            MoveShip(_savedPosition, _savedRotation);

        if (_cameraController != null)
        {
            _cameraController.enabled = true;
            _cameraController.SnapToTarget();
        }

        foreach (Canvas canvas in _hiddenCanvases)
        {
            if (canvas != null)
                canvas.enabled = true;
        }

        _hiddenCanvases.Clear();
    }

    private void End()
    {
        _active = false;
        _ship = null;
        GameInputLock.Unlock(this);
    }

    private void MoveShip(Vector3 position, Quaternion rotation)
    {
        _ship.SetPositionAndRotation(position, rotation);
        if (_rb != null)
        {
            _rb.position = position;
            _rb.rotation = rotation;
        }
    }

    /// <summary>함선 모델 범위. 돌려주는 값은 진행 방향(forward)으로 잰 반 길이</summary>
    private float ShipBounds(out Vector3 center, out Vector3 extents)
    {
        center = _ship.position;
        extents = Vector3.zero;
        bool any = false;
        Bounds b = default;
        foreach (Renderer r in _ship.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || !r.enabled)
                continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }

        if (!any)
            return 10f;

        center = b.center;
        extents = b.extents;
        return Vector3.Dot(extents, Abs(_ship.forward));
    }

    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    // ── 문구 ──────────────────────────────────────────

    /// <summary>JSON의 엔딩 문구를 줄 id 순서대로 채우고, 그 텍스트들을 화면에 뜨는 순서대로 돌려준다</summary>
    private List<TMP_Text> OrderedTexts()
    {
        var ordered = new List<TMP_Text>();
        int body = 0;
        if (StoryDatabase.TryGetSequence(TextSequenceId, out StorySequence seq) && seq.lines != null)
        {
            foreach (StoryLine line in seq.lines)
            {
                string text = PlayerProfile.Format(line.text ?? "");
                switch (line.id != null && line.id.StartsWith("body") ? "body" : line.id)
                {
                    case "title":
                        _titleText.text = text;
                        ordered.Add(_titleText);
                        break;
                    case "body":
                        if (body < _bodyTexts.Length)
                        {
                            _bodyTexts[body].text = text;
                            ordered.Add(_bodyTexts[body]);
                            body++;
                        }
                        break;
                    case "footer":
                        _footerText.text = text;
                        ordered.Add(_footerText);
                        break;
                    case "hint":
                        _hintText.text = text;
                        break;
                }
            }
        }

        for (int i = body; i < _bodyTexts.Length; i++)
            _bodyTexts[i].gameObject.SetActive(false);

        return ordered;
    }

    private static CanvasGroup GroupOf(TMP_Text text)
    {
        CanvasGroup g = text.GetComponent<CanvasGroup>();
        return g != null ? g : text.gameObject.AddComponent<CanvasGroup>();
    }

    // ── 페이드 ────────────────────────────────────────

    private void SetBlack(float alpha)
    {
        Color c = _black.color;
        c.a = alpha;
        _black.color = c;
    }

    private IEnumerator FadeBlack(float to, float seconds)
    {
        float from = _black.color.a;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            SetBlack(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds)));
            yield return null;
        }

        SetBlack(to);
    }

    private static IEnumerator FadeGroup(CanvasGroup group, float to, float seconds)
    {
        float from = group.alpha;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }

        group.alpha = to;
    }

    private static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            yield return null;
    }
}
