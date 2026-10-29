using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 임무 실행 담당의 공통 부분. 한 번에 임무 하나만 진행되며(Current), 임무가 끝나면 Director가 스스로 사라진다.
/// 유형별 진행은 하위 클래스(EscortMissionDirector, SweepMissionDirector)의 Run이 정하고, 여기서는
/// 시작/중단, 수락·성공 대사, 임무 HUD, 적 함선 생성·집계·후퇴, 보수 지급과 결과 표시를 맡는다.
/// 진행 중 정거장 도킹·지도 워프를 막을지는 BlocksTravel로 알린다 (StationDocking, StarSystemWarp가 읽는다).
/// 떠날 수 있는 동안(막지 않는 동안) 플레이어와 _despawnDistance보다 멀어진 임무 적(채굴지 순찰대 등)은 없앤다.
/// 임무가 끝나면(성공·실패·중단) Ended를 알린다 (정거장 임무 게시판이 목록을 새로 뽑는다).
/// 정거장의 스페이스 라운지(MissionBoardWindow)에서 수락하면 시작한다 (개발자 명령 mission start 번호로도 가능).
/// </summary>
public abstract class MissionDirector : MonoBehaviour
{
    /// <summary>진행 중인 임무 (없으면 null)</summary>
    public static MissionDirector Current { get; private set; }
    public static bool IsRunning => Current != null;

    /// <summary>진행 중인 임무가 지금 정거장 도킹과 지도 워프를 막는지 (교전 중 등)</summary>
    public static bool BlocksTravel => Current != null && Current.TravelBlocked;

    [Header("공통 프리팹")]
    [Tooltip("적이 워프로 물러날 때의 게이트 이펙트")]
    [SerializeField] protected GameObject _warpOutPrefab;
    [SerializeField] private MissionHud _hudPrefab;

    [Header("마무리")]
    [Tooltip("끝난 뒤 적이 물러나기 시작할 때까지 (초)")]
    [SerializeField, Min(0f)] private float _retreatDelay = 2f;
    [Tooltip("결과를 화면에 보여 두는 시간 (초)")]
    [SerializeField, Min(0f)] private float _resultSeconds = 8f;
    [Tooltip("떠날 수 있는 동안, 플레이어와 이보다 멀어진 임무 적은 없앤다 (수평 거리)")]
    [SerializeField, Min(100f)] private float _despawnDistance = 2500f;

    /// <summary>임무가 끝났을 때 (성공·실패·중단 모두)</summary>
    public static event System.Action Ended;

    /// <summary>이 Director가 진행하는 임무</summary>
    public MissionData Mission { get; private set; }

    protected MissionHud Hud { get; private set; }
    protected EnemyShipCatalog Catalog { get; private set; }
    protected Transform Player { get; private set; }
    protected readonly List<PirateShip> Enemies = new List<PirateShip>();

    /// <summary>하위 클래스: 지금 도킹·지도 워프를 막아야 하는지</summary>
    protected abstract bool TravelBlocked { get; }

    /// <summary>하위 클래스: 임무 데이터를 받아 둔다. 이 Director가 다룰 수 없는 데이터면 false.</summary>
    protected abstract bool Setup(MissionData data);

    /// <summary>하위 클래스: 임무를 처음부터 끝까지 진행한다. 끝나면 Finish 등으로 스스로 사라진다.</summary>
    protected abstract IEnumerator Run();

    // ── 임무 목록 / 시작 / 중단 ─────────────────────────

    public static IEnumerable<MissionData> AllMissions()
    {
        return Resources.LoadAll<MissionData>(MissionData.ResourceFolder).OrderBy(m => m.id);
    }

    public static MissionData FindMission(string id)
    {
        return AllMissions().FirstOrDefault(m => m.id == id);
    }

    /// <summary>임무를 시작한다. 시작할 수 없으면 false와 이유.</summary>
    public static bool TryStart(string id, out string error)
    {
        error = null;
        MissionData data = FindMission(id);
        if (data == null)
            error = $"'{id}' 임무가 없습니다. (mission list)";
        else if (IsRunning)
            error = "이미 진행 중인 임무가 있습니다. (mission stop)";
        else if (StoryPlayer.IsPlaying)
            error = "스토리가 재생 중입니다.";
        else if (StarSystemWarp.IsWarping)
            error = "워프 중입니다.";
        else if (FindObjectOfType<SpaceshipController>() == null)
            error = "플레이어 함선이 없습니다.";

        MissionDirector prefab = error == null ? Resources.Load<MissionDirector>(data.DirectorPath) : null;
        if (error == null && prefab == null)
            error = $"Assets/Resources/{data.DirectorPath}.prefab이 없습니다.";

        if (error != null)
            return false;

        MissionDirector director = Instantiate(prefab);
        director.name = $"Mission_{data.id}";
        if (!director.Setup(data))
        {
            Destroy(director.gameObject);
            error = $"'{data.id}' 임무를 {prefab.name}로 실행할 수 없습니다.";
            return false;
        }

        director.Begin(data);
        return true;
    }

    /// <summary>진행 중인 임무를 결과 없이 끝내고 임무 함선들을 치운다 (개발자 명령 mission stop).</summary>
    public static void Abort()
    {
        if (Current == null)
            return;

        // Destroy는 프레임 끝에 일어나므로, 바로 다음 임무를 시작할 수 있게 먼저 비운다
        MissionDirector director = Current;
        Current = null;
        Destroy(director.gameObject);
    }

    private void Begin(MissionData data)
    {
        Current = this;
        Mission = data;
        Catalog = EnemyShipCatalog.Load();
        Hud = Instantiate(_hudPrefab, transform);

        var ship = FindObjectOfType<SpaceshipController>();
        Player = ship != null ? ship.transform : null;

        StartCoroutine(Run());
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Current = null;
        Ended = null;
    }

    protected virtual void Update()
    {
        if (TravelBlocked || Player == null || Time.frameCount % 15 != 0)
            return;

        for (int i = Enemies.Count - 1; i >= 0; i--)
        {
            PirateShip enemy = Enemies[i];
            if (enemy == null)
                continue;

            Vector3 d = enemy.transform.position - Player.position;
            d.y = 0f;
            if (d.magnitude > _despawnDistance)
            {
                Enemies.RemoveAt(i);
                Destroy(enemy.gameObject);
            }
        }
    }

    protected virtual void OnDestroy()
    {
        if (Current == this)
            Current = null;
        if (Mission != null)
            Ended?.Invoke();

        CombatHud.HideWaypoint();

        foreach (PirateShip enemy in Enemies)
        {
            if (enemy != null)
                Destroy(enemy.gameObject);
        }
    }

    // ── 진행 도구 ──────────────────────────────────────

    /// <summary>스토리 시퀀스를 재생하고 끝날 때까지 기다린다 (없으면 바로 넘어간다).</summary>
    protected IEnumerator PlayStory(string sequenceId)
    {
        if (string.IsNullOrEmpty(sequenceId))
            yield break;

        bool done = false;
        if (!StoryPlayer.Play(sequenceId, () => done = true))
            yield break;

        while (!done)
            yield return null;
    }

    /// <summary>정거장 안이면 출항하고 바깥에 나올 때까지 기다린다 (도킹 연출 중이면 끝난 뒤에).</summary>
    protected IEnumerator LeaveStation()
    {
        while (StationDocking.IsDocked && StationDocking.Instance != null)
        {
            StationDocking.Instance.Undock();
            yield return null;
        }
    }

    /// <summary>함종의 적 함선을 만들어 target을 습격하게 한다. 만들 수 없으면 null.</summary>
    protected PirateShip SpawnEnemy(string shipClass, Vector3 position, Vector3 faceDir, Transform target)
    {
        if (Catalog == null || Player == null)
            return null;

        faceDir.y = 0f;
        Quaternion rotation = faceDir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(faceDir) : Quaternion.identity;
        PirateShip enemy = Catalog.Spawn(shipClass, position, rotation, Player, _warpOutPrefab);
        if (enemy == null)
            return null;

        enemy.Raid(target != null ? target : Player);
        Enemies.Add(enemy);
        return enemy;
    }

    /// <summary>플레이어의 항성계 위치. 도킹 중이면(함선은 먼 내부 공간에 있다) 그 정거장 위치.</summary>
    protected Vector3 PlayerWorldPosition()
    {
        if (StationDocking.IsDocked && StationZone.Current != null)
            return StationZone.Current.transform.position;
        return Player != null ? Player.position : Vector3.zero;
    }

    protected static bool IsAlive(PirateShip enemy)
    {
        return enemy != null && enemy.Hull != null && enemy.Hull.IsActive && enemy.Current != PirateShip.Mode.Warp;
    }

    protected int AliveEnemies()
    {
        return Enemies.Count(IsAlive);
    }

    /// <summary>남은 적들이 차례로 워프해 물러난다.</summary>
    protected IEnumerator RetreatEnemies()
    {
        yield return new WaitForSeconds(_retreatDelay);

        foreach (PirateShip enemy in Enemies.ToArray())
        {
            if (!IsAlive(enemy))
                continue;

            enemy.SetMode(PirateShip.Mode.Warp);
            yield return new WaitForSeconds(Random.Range(0.2f, 0.6f));
        }

        Hud.SetEnemies(0);
    }

    /// <summary>성공: 적 후퇴 → 성공 대사 → 보수 지급 → 결과 표시 → 끝. lines는 보수 줄 아래에 덧붙일 내용.</summary>
    protected IEnumerator Succeed(MissionData data, int credits, string rewardLine, IEnumerable<string> extraLines)
    {
        CombatHud.HideWaypoint();
        yield return RetreatEnemies();
        yield return PlayStory(data.successSequence);

        if (CreditWallet.Instance != null)
            CreditWallet.Instance.Add(credits);

        var lines = new List<string> { rewardLine };
        if (extraLines != null)
            lines.AddRange(extraLines);
        if (data.extraRewards != null)
            lines.AddRange(data.extraRewards.Where(r => !string.IsNullOrEmpty(r)));

        Hud.ShowResult(true, "임무 완료", string.Join("\n", lines));
        yield return Finish();
    }

    /// <summary>실패: 결과 표시 → 적 후퇴 → 끝.</summary>
    protected IEnumerator Fail(string reason)
    {
        CombatHud.HideWaypoint();
        Hud.ShowResult(false, "임무 실패", reason);
        yield return RetreatEnemies();
        yield return Finish();
    }

    private IEnumerator Finish()
    {
        yield return new WaitForSeconds(_resultSeconds);
        Destroy(gameObject);
    }
}
