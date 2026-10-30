using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 자동 저장과 이어 하기.
/// 저장하는 때:
///  - 우주정거장에 도킹했을 때 (StationDocking). 대사가 진행 중이면 대사가 끝난 뒤 그때도 도킹해 있으면 저장한다
///    (예: 튜토리얼 귀환 도킹 → 정산 대사가 끝난 뒤 저장).
///  - 우주정거장에서 출항할 때 (임무 출발로 나갈 때 포함). 정거장 안에서 한 일(판매·구매·장착, 라스 연락 등)이 남고,
///    다시 시작하면 그 정거장 안에서 시작한다. 대사가 진행 중이면 저장하지 않는다.
///  - 임무를 수락했을 때. 스토리 도중이면 지금 장면 다음 장면(then)부터 이어 가도록 표시해 둔다
///    (예: 술집에서 임무 수락 → 다시 시작하면 격납고 장면부터).
/// 저장 내용: 플레이어 이름, 도킹한 정거장, 크레딧, 진행 중인 임무와 완료한 목표, 장착 무기, 격납고 보관 무기, 장갑·쉴드 장비, 화물칸 광석, 이어 갈 스토리 장면.
/// 게임을 켜거나 씬을 다시 불러올 때(패배 후 재시작) 저장이 있으면 프롤로그 없이 저장한 정거장 안에 도킹한 채(StationDocking) 시작하고,
/// 이어 갈 장면이 있으면 그 장면부터 재생한다.
/// 저장 파일: Application.persistentDataPath/save.json (개발자 모드: save / save clear)
/// 에디터에서는 개발 중 처음부터 확인하기 쉽도록 기본으로 저장을 불러오지 않는다(저장은 한다).
/// 이어 하기를 시험하려면 메뉴 SpaceRoglike/저장/에디터에서 이어 하기 를 켠다. 빌드에서는 항상 이어 한다.
/// </summary>
public class SaveSystem : MonoBehaviour
{
    private const int Version = 1;
    private const string FileName = "save.json";

    [Serializable]
    public class SaveData
    {
        public int version;
        public string savedAt;
        public int station = -1;
        public int credits;
        public string quest;
        public string[] questDone = new string[0];
        public string[] loadout = new string[0];
        [Tooltip("격납고에 보관 중인 무기 (장착하지 않은 것)")]
        public string[] hangar = new string[0];
        [Tooltip("화물칸의 광석 (id:양)")]
        public string[] cargo = new string[0];
        [Tooltip("함선에 단 장비 (EquipmentCatalog id)")]
        public string[] equipped = new string[0];
        [Tooltip("예전 저장(장갑 칸·쉴드 칸)의 장착 장비. 읽기만 하고 equipped가 비었을 때 쓴다")]
        public string armor;
        public string shield;
        [Tooltip("격납고에 보관 중인 장비 (장착하지 않은 것)")]
        public string[] equipment = new string[0];
        [Tooltip("타고 있는 함선과 가진 함선 (ShipCatalog id, 예전 저장은 비어 있음 → 기본 함선)")]
        public string ship;
        public string[] ships = new string[0];
        [Tooltip("타고 있지 않은 함선들의 무장 (함선id=무기1,무기2,...). 타고 있는 함선의 무장은 loadout")]
        public string[] shipLoadouts = new string[0];
        [Tooltip("메인 스토리 ① 첫 번째 함선 해금 진행")]
        public FirstShipQuest.SaveState firstShip = new FirstShipQuest.SaveState();
        [Tooltip("다시 시작하면 재생할 스토리 시퀀스 (없으면 자유 플레이)")]
        public string resumeSequence;
        [Tooltip("플레이어 이름 (새 게임에서 입력, 예전 저장은 비어 있음)")]
        public string playerName;
    }

    private static SaveSystem _instance;

    private bool _pendingDockSave;
    private bool _loading; // 불러오는 동안(함선을 정거장에 놓는 것도 도킹으로 잡힌다)은 저장하지 않는다

    public const string ContinueInEditorPrefKey = "SpaceRoglike.ContinueInEditor";

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);
    public static bool HasSave => File.Exists(FilePath);

    /// <summary>이어 하기로 저장을 불러오는 중인지 (이 동안은 저장·퀘스트 연락을 하지 않는다)</summary>
    public static bool IsLoading => _instance != null && _instance._loading;

    /// <summary>
    /// 게임 씬을 시작할 때 저장을 불러와 이어 하는지.
    ///  - 패배 후 '다시 시작'(ContinueOnNextLoad)이면 저장이 있을 때 이어 한다.
    ///  - 타이틀에서 '이어 하기'면 이어 하고, '새 게임'이면 이어 하지 않는다 (GameLaunch).
    ///  - 메인 씬을 바로 Play했으면(개발용) 에디터에서는 메뉴로 켰을 때만, 빌드에서는 저장이 있으면 이어 한다.
    /// </summary>
    public static bool WillContinue
    {
        get
        {
            if (_continueOnNextLoad || GameLaunch.Current == GameLaunch.Mode.Continue)
                return HasSave;
            if (GameLaunch.Current == GameLaunch.Mode.NewGame)
                return false;
#if UNITY_EDITOR
            if (!UnityEditor.EditorPrefs.GetBool(ContinueInEditorPrefKey, false))
                return false;
#endif
            return HasSave;
        }
    }

    private static bool _continueOnNextLoad;

    /// <summary>다음에 게임 씬을 불러올 때 저장에서 이어 한다 (패배 후 다시 시작). 한 번만 적용된다.</summary>
    public static void ContinueOnNextLoad()
    {
        _continueOnNextLoad = true;
    }

    // ── 시작 ──────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _continueOnNextLoad = false;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 플레이어 함선이 있는 게임 씬에서만
        if (FindObjectOfType<SpaceshipController>() == null)
            return;

        if (_instance == null)
        {
            var go = new GameObject("SaveSystem");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SaveSystem>();
        }

        bool resume = WillContinue;
        _continueOnNextLoad = false;
        if (!resume)
            return;

        SaveData data = Read();
        _instance._loading = true;
        _instance.StartCoroutine(_instance.LoadRoutine(data));
    }

    private static SaveData Read()
    {
        try
        {
            return JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Save] 저장 파일을 읽지 못해 처음부터 시작합니다: {e.Message}");
            return null;
        }
    }

    private void OnEnable()
    {
        StationZone.AnyEntered += HandleDocked;
        StationZone.AnyExited += HandleUndocked;
        StoryPlayer.OnCue += HandleCue;
    }

    private void OnDisable()
    {
        StationZone.AnyEntered -= HandleDocked;
        StationZone.AnyExited -= HandleUndocked;
        StoryPlayer.OnCue -= HandleCue;
    }

    // ── 저장 ──────────────────────────────────────────

    private void HandleDocked(StationZone zone)
    {
        _pendingDockSave = true;
    }

    private void HandleUndocked(StationZone zone)
    {
        // 정거장 안에서 한 일(광석 판매로 라스 연락, 장비 구매·장착 등)을 남긴다. 다시 시작하면 이 정거장 안에서 시작한다.
        // 대사가 진행 중이면(튜토리얼·임무 출발 연출) 대사 쪽이 저장 시점을 정하므로 여기서는 저장하지 않는다.
        _pendingDockSave = false;
        if (_loading || StoryPlayer.IsPlaying)
            return;

        Save(null, zone);
    }

    private void HandleCue(string cue)
    {
        if (!cue.StartsWith("quest_accept:"))
            return;

        // 임무 수락: 스토리 도중이면 다음 장면부터 이어 가게 저장한다 (수락 연출이 끝난 뒤의 상태로)
        StartCoroutine(SaveNextFrame(NextSequence(StoryPlayer.CurrentSequenceId)));
    }

    private IEnumerator SaveNextFrame(string resume)
    {
        yield return null; // 같은 줄의 다른 cue(퀘스트 창 등)가 먼저 처리되도록
        Save(resume);
    }

    private void Update()
    {
        // 도킹 저장은 대사·워프가 끝난 뒤, 그때도 정거장 안에 있으면
        if (_loading || !_pendingDockSave || StoryPlayer.IsPlaying || StarSystemWarp.IsWarping)
            return;

        _pendingDockSave = false;
        if (StationZone.Current != null)
            Save(null);
    }

    /// <summary>
    /// 지금 상태를 저장한다. resumeSequence: 다시 시작하면 재생할 스토리 시퀀스 (없으면 자유 플레이).
    /// station: 다시 시작할 정거장 (없으면 지금 도킹한 정거장, 그것도 없으면 모항)
    /// </summary>
    public static void Save(string resumeSequence, StationZone station = null)
    {
        var data = new SaveData
        {
            version = Version,
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            resumeSequence = resumeSequence,
            playerName = PlayerProfile.Name,
        };

        StationZone zone = station != null ? station : StationZone.Current;
        if (zone == null && StarSystem.Instance != null)
            zone = StationZone.Home; // 정거장 밖에서 저장하면(개발자 모드 등) 모항으로
        data.station = zone != null ? zone.StationIndex : -1;

        if (CreditWallet.Instance != null)
            data.credits = CreditWallet.Instance.Amount;

        QuestTracker quest = FindObjectOfType<QuestTracker>(true);
        if (quest != null && quest.CurrentQuestId != null)
        {
            data.quest = quest.CurrentQuestId;
            data.questDone = new System.Collections.Generic.List<string>(quest.DoneObjectives).ToArray();
        }

        data.ship = PlayerShips.CurrentId;
        data.ships = PlayerShips.OwnedForSave();
        data.shipLoadouts = PlayerShips.LoadoutsForSave();
        data.firstShip = FirstShipQuest.ToSave();
        if (PlayerWeaponSelector.Instance != null)
            data.loadout = PlayerWeaponSelector.Instance.CurrentLoadout();
        data.hangar = PlayerHangar.ToNames();
        data.equipped = PlayerEquipment.EquippedIds();
        data.equipment = PlayerEquipment.StoredIds();
        if (CargoHold.Instance != null)
            data.cargo = CargoHold.Instance.ToSave();

        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
            Debug.Log($"[Save] 저장했습니다: 정거장 {data.station}, 크레딧 {data.credits}, 이어 갈 장면 {(data.resumeSequence ?? "없음")}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Save] 저장하지 못했습니다: {e.Message}");
        }
    }

    public static void Clear()
    {
        if (HasSave)
            File.Delete(FilePath);
    }

    /// <summary>시퀀스의 다음 시퀀스(then). 없으면 null.</summary>
    private static string NextSequence(string sequenceId)
    {
        if (string.IsNullOrEmpty(sequenceId) || !StoryDatabase.TryGetSequence(sequenceId, out StorySequence sequence))
            return null;

        return string.IsNullOrEmpty(sequence.then) ? null : sequence.then;
    }

    // ── 불러오기 ──────────────────────────────────────

    private IEnumerator LoadRoutine(SaveData data)
    {
        yield return ApplyLoad(data);

        // 불러오면서 생긴 도킹은 저장하지 않는다 (불러온 상태 그대로가 저장 내용)
        _pendingDockSave = false;
        _loading = false;
    }

    private IEnumerator ApplyLoad(SaveData data)
    {
        if (data == null)
            yield break;

        // 씬 오브젝트의 Start(지갑, 무기 슬롯, 항성계 등)가 끝난 뒤에 적용한다
        yield return null;
        yield return null;

        var ship = FindObjectOfType<SpaceshipController>();
        StarSystem system = StarSystem.Instance;
        if (ship == null)
            yield break;

        // 저장한 정거장 안에 도킹한 채로 시작한다 (튜토리얼 도중이면 대사가 정거장을 다루므로 정거장 곁에 둔다)
        StationZone savedZone = null;
        foreach (StationZone zone in StationZone.All)
        {
            if (zone.StationIndex == data.station)
                savedZone = zone;
        }

        if (savedZone != null && string.IsNullOrEmpty(data.resumeSequence) && StationDocking.Instance != null)
        {
            StationDocking.Instance.DockImmediately(savedZone);
        }
        else if (system != null && data.station >= 0 && data.station < system.Data.stations.Length)
        {
            StationZone zone = StarSystem.FindStation(data.station);
            Vector3 dock = zone != null ? system.StationArrival(zone) : system.StationWorld(data.station);
            dock.y = system.Origin.y;
            var rb = ship.GetComponent<Rigidbody>();
            ship.transform.SetPositionAndRotation(dock, Quaternion.identity);
            if (rb != null)
            {
                rb.position = dock;
                rb.rotation = Quaternion.identity;
                if (!rb.isKinematic)
                    rb.velocity = Vector3.zero;
            }

            var cam = FindObjectOfType<CameraController>();
            if (cam != null)
                cam.SnapToTarget();
        }

        if (CreditWallet.Instance != null)
            CreditWallet.Instance.SetAmount(data.credits);
        if (!string.IsNullOrEmpty(data.playerName))
            PlayerProfile.Name = data.playerName;

        // 함선이 먼저다 (터렛 수 = 무기 슬롯 수, 장비 칸 수)
        PlayerShips.Restore(data.ship, data.ships, data.shipLoadouts);
        if (PlayerWeaponSelector.Instance != null && data.loadout != null && data.loadout.Length > 0)
            PlayerWeaponSelector.Instance.ApplyLoadout(data.loadout);
        PlayerHangar.Restore(data.hangar);
        bool oldSlots = data.equipped == null || data.equipped.Length == 0;
        PlayerEquipment.Restore(oldSlots ? new[] { data.armor, data.shield } : data.equipped, data.equipment);
        if (CargoHold.Instance != null)
            CargoHold.Instance.Restore(data.cargo);
        FirstShipQuest.Restore(data.firstShip);

        StoryPlayer.EnsureCreated();
        yield return null; // 퀘스트 창이 생기도록

        if (!string.IsNullOrEmpty(data.quest))
        {
            QuestTracker quest = FindObjectOfType<QuestTracker>(true);
            if (quest != null)
                quest.Restore(data.quest, data.questDone ?? new string[0]);
        }

        // 튜토리얼 도중이었으면 그 장면부터 이어 간다
        if (!string.IsNullOrEmpty(data.resumeSequence))
            StoryPlayer.Play(data.resumeSequence);

        Debug.Log($"[Save] 이어 하기: 정거장 {data.station}, 크레딧 {data.credits}, 이어 갈 장면 {(data.resumeSequence ?? "없음")} ({data.savedAt})");
    }
}
