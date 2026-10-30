using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 메인 스토리 ① 첫 번째 함선 해금 퀘스트의 진행 (정의는 FirstShipQuestData).
///  1) 새로운 제안: 보유 자금이 unlockCredits 이상이 되면(대사·임무·워프 중이 아닐 때) 라스의 수신 통신이 온다.
///     선택지 "자세히 / 구매 / 보류". 보류해도 사라지지 않고, 정거장 스페이스 라운지의 '라스' 항목에서 언제든 다시 살 수 있다.
///  2) 설계도와 대가: blueprintPrice를 내고 블루프린트를 산다 (재구매 없음).
///  3) 선체부터 준비해: 광물(materials)을 라스에게 납품한다. 화물칸이 작아 여러 번 나눠 낼 수 있다.
///  4) 버려진 화물: 특수 합금 회수 임무(alloyMission, SalvageMissionDirector)
///  5) 흔적을 쫓아서: 고급 동력 코어 확보 임무(coreMission, InterceptMissionDirector). 적 지휘관 통신 기록은 선택 목표.
///  6) 새로운 함선: 라스에게서 최종 조립 → 함선(shipId) 지급, 바로 탑승.
/// 라운지·미션 탭의 '라스' 항목(MissionBoardWindow)이 이 클래스의 단계별 버튼을 보여 준다.
/// 진행 상태(단계·납품량·퀘스트 물품)는 저장(SaveSystem)에 함께 남는다. 단계가 바뀌면 도킹 중일 때 바로 저장한다.
/// </summary>
public class FirstShipQuest : MonoBehaviour
{
    public enum Stage
    {
        Locked,     // 아직 라스의 연락 전
        Offered,    // 연락을 받았고 블루프린트를 사지 않음
        Minerals,   // 블루프린트 보유, 광물 납품 중
        Alloy,      // 특수 합금 회수
        Core,       // 고급 동력 코어 확보
        Assembly,   // 최종 조립 대기
        Done        // 함선 지급 완료
    }

    [Serializable]
    public class SaveState
    {
        public int stage;
        [Tooltip("라스에게 납품한 광물 (id:양)")]
        public string[] delivered = new string[0];
        public int alloys;
        public bool core;
        public bool commsLog;
    }

    private const float CheckInterval = 1f;

    private static FirstShipQuest _instance;
    private static Stage _stage;
    private static readonly Dictionary<string, int> Delivered = new Dictionary<string, int>();
    private static int _alloys;
    private static bool _core;
    private static bool _commsLog;
    private static bool _buyChosen;
    private static bool _assembling;

    /// <summary>단계나 물품이 바뀌었을 때 (UI 갱신용)</summary>
    public static event Action OnChanged;

    public static FirstShipQuestData Data => FirstShipQuestData.Load();
    public static Stage Current => _stage;
    public static int Alloys => _alloys;
    public static bool HasCore => _core;
    public static bool HasCommsLog => _commsLog;
    public static bool HasBlueprint => _stage >= Stage.Minerals && _stage < Stage.Done;

    /// <summary>라운지·미션 탭에 '라스' 항목을 보여 줄지 (연락을 받은 뒤부터 함선을 받기 전까지)</summary>
    public static bool IsVisible => _stage >= Stage.Offered && _stage < Stage.Done;

    private float _nextCheck;

    // ── 시작 ──────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        ResetState();
        OnChanged = null;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void ResetState()
    {
        _stage = Stage.Locked;
        Delivered.Clear();
        _alloys = 0;
        _core = false;
        _commsLog = false;
        _buyChosen = false;
        _assembling = false;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 플레이어 함선이 있는 게임 씬에서만. 다시 시작하면 처음부터 (이어 하기는 SaveSystem이 다시 채운다)
        if (FindObjectOfType<SpaceshipController>() == null)
            return;

        ResetState();
        if (_instance == null)
        {
            var go = new GameObject("FirstShipQuest");
            _instance = go.AddComponent<FirstShipQuest>();
        }

        // StoryPlayer가 플레이를 시작할 때 OnChoice를 비우므로(정적 초기화) 씬을 불러올 때 구독한다 (중복 없이)
        StoryPlayer.OnChoice -= HandleChoice;
        StoryPlayer.OnChoice += HandleChoice;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    // 1) 보유 자금이 기준을 넘으면 라스의 연락
    private void Update()
    {
        if (_stage != Stage.Locked || Time.unscaledTime < _nextCheck)
            return;

        _nextCheck = Time.unscaledTime + CheckInterval;
        FirstShipQuestData data = Data;
        if (data == null || CreditWallet.Instance == null || CreditWallet.Instance.Amount < data.unlockCredits)
            return;
        if (StoryPlayer.IsPlaying || MissionDirector.IsRunning || StarSystemWarp.IsWarping || SaveSystem.IsLoading)
            return;

        _stage = Stage.Offered;
        Changed();
        PlayOffer(false);
    }

    // ── 1~2. 연락 / 구매 ──────────────────────────────

    /// <summary>라스의 블루프린트 제안 통신을 재생한다 (again: 라운지에서 다시 통신할 때의 짧은 버전)</summary>
    public static bool PlayOffer(bool again)
    {
        FirstShipQuestData data = Data;
        if (data == null || _stage != Stage.Offered)
            return false;

        _buyChosen = false;
        return StoryPlayer.Play(again ? data.offerAgainSequence : data.offerSequence, () =>
        {
            if (!_buyChosen)
                return;
            _buyChosen = false;
            Purchase();
        });
    }

    private static void HandleChoice(string lineId, string choiceId, int index)
    {
        FirstShipQuestData data = Data;
        if (data != null && _stage == Stage.Offered && choiceId == data.buyChoiceId)
            _buyChosen = true;
    }

    public static bool CanPurchase(out string reason)
    {
        FirstShipQuestData data = Data;
        reason = null;
        if (data == null || _stage != Stage.Offered)
            reason = "지금은 살 수 없습니다.";
        else if (!StationShop.CanAfford(data.blueprintPrice))
            reason = $"크레딧이 부족합니다. ({data.blueprintPrice:N0} CR 필요)";
        return reason == null;
    }

    /// <summary>블루프린트를 산다. 돈이 모자라면 라스가 그렇게 말한다.</summary>
    public static bool Purchase()
    {
        FirstShipQuestData data = Data;
        if (data == null || _stage != Stage.Offered)
            return false;

        if (!CanPurchase(out _) || !CreditWallet.Instance.TrySpend(data.blueprintPrice))
        {
            StoryPlayer.Play(data.noMoneySequence);
            return false;
        }

        _stage = Stage.Minerals;
        Changed();
        StoryPlayer.Play(data.purchasedSequence); // 이어서 광물 퀘스트 안내 (quest_accept)
        return true;
    }

    // ── 3. 광물 납품 ──────────────────────────────────

    public static int DeliveredOf(string oreId)
    {
        return Delivered.TryGetValue(oreId, out int amount) ? amount : 0;
    }

    public static bool MaterialsComplete
    {
        get
        {
            FirstShipQuestData data = Data;
            return data != null && data.materials.All(m => DeliveredOf(m.oreId) >= m.amount);
        }
    }

    /// <summary>라스에게 지금 납품할 수 있는지 (어느 정거장이나)</summary>
    public static bool CanDeliver(StationZone zone, out string reason)
    {
        reason = null;
        CargoHold cargo = CargoHold.Instance;
        if (_stage != Stage.Minerals)
            reason = "납품할 단계가 아닙니다.";
        else if (zone == null)
            reason = "정거장에 도킹해야 라스에게 납품할 수 있습니다.";
        else if (cargo == null || Data.materials.All(m => DeliveredOf(m.oreId) >= m.amount || cargo.Amount(m.oreId) < 1f))
            reason = "화물칸에 납품할 광물이 없습니다.";
        return reason == null;
    }

    /// <summary>화물칸의 광물을 필요한 만큼 납품한다. 낸 개수를 돌려준다. 모두 채우면 다음 단계(특수 합금)로.</summary>
    public static int DeliverMinerals(StationZone zone)
    {
        if (!CanDeliver(zone, out _))
            return 0;

        CargoHold cargo = CargoHold.Instance;
        int total = 0;
        foreach (FirstShipQuestData.Material m in Data.materials)
        {
            int want = m.amount - DeliveredOf(m.oreId);
            int taken = Mathf.FloorToInt(cargo.Take(m.oreId, want));
            if (taken <= 0)
                continue;

            Delivered[m.oreId] = DeliveredOf(m.oreId) + taken;
            total += taken;
            if (DeliveredOf(m.oreId) >= m.amount)
                CompleteObjective(Data.questMinerals, m.oreId);
        }

        if (MaterialsComplete)
        {
            CompleteObjective(Data.questMinerals, "deliver");
            _stage = Stage.Alloy;
            StoryPlayer.Play(Data.mineralsDoneSequence); // 이어서 특수 합금 안내 (quest_accept)
        }

        Changed();
        return total;
    }

    // ── 4~5. 특수 부품 (임무가 부른다) ─────────────────

    /// <summary>특수 합금 회수 임무를 시작할 수 있는 단계인지와, 시작</summary>
    public static bool StartPartMission(out string error)
    {
        error = null;
        FirstShipQuestData data = Data;
        string id = _stage == Stage.Alloy ? data.alloyMission : _stage == Stage.Core ? data.coreMission : null;
        if (id == null)
        {
            error = "지금은 출발할 임무가 없습니다.";
            return false;
        }

        return MissionDirector.TryStart(id, out error);
    }

    /// <summary>잔해에서 특수 합금을 회수했다</summary>
    public static void AddAlloys(int amount)
    {
        _alloys = Mathf.Min(Data.alloyRequired, _alloys + amount);
        if (_alloys >= Data.alloyRequired)
            CompleteObjective(Data.questAlloy, "alloy");
        Changed();
    }

    /// <summary>특수 합금 임무 성공 → 고급 동력 코어 단계</summary>
    public static void CompleteAlloyStage()
    {
        if (_stage != Stage.Alloy)
            return;
        _alloys = Mathf.Max(_alloys, Data.alloyRequired);
        _stage = Stage.Core;
        Changed();
    }

    public static void SetCommsLog()
    {
        _commsLog = true;
        CompleteObjective(Data.questCore, "log");
        Changed();
    }

    /// <summary>고급 동력 코어 확보 → 최종 조립 단계</summary>
    public static void CompleteCoreStage()
    {
        if (_stage != Stage.Core)
            return;
        _core = true;
        CompleteObjective(Data.questCore, "core");
        _stage = Stage.Assembly;
        Changed();
    }

    // ── 6. 최종 조립 ──────────────────────────────────

    public static bool CanAssemble(StationZone zone, out string reason)
    {
        reason = null;
        if (_stage != Stage.Assembly || _assembling)
            reason = "아직 재료가 모두 모이지 않았습니다.";
        else if (zone == null)
            reason = "정거장에 도킹해야 라스에게 최종 조립을 맡길 수 있습니다.";
        else if (StoryPlayer.IsPlaying)
            reason = "대화 중입니다.";
        return reason == null;
    }

    /// <summary>최종 조립: 대사 → 함선 지급(대사의 ship_grant cue, 건너뛰어도 끝나면 지급) → 라스의 통신 「남겨진 의문」 → 임시 엔딩(대사에 then으로 이어짐, EndingDirector) → 퀘스트 완료</summary>
    public static bool Assemble(StationZone zone)
    {
        if (!CanAssemble(zone, out _))
            return false;

        // 대사 도중 ship_grant cue로 함선이 바뀌고, 건너뛰거나 cue가 없어도 끝나면 지급한다.
        // 단계는 지급한 뒤에 Done으로 (대사 도중 꺼져도 다시 조립할 수 있게)
        FirstShipQuestData data = Data;
        _assembling = true;
        bool started = StoryPlayer.Play(data.assemblySequence, () => FinishAssembly(data));
        if (!started)
            FinishAssembly(data);
        return true;
    }

    private static void FinishAssembly(FirstShipQuestData data)
    {
        _assembling = false;
        if (PlayerShips.CurrentId != data.shipId || !PlayerShips.Owns(data.shipId))
            PlayerShips.Grant(data.shipId);
        CompleteObjective(data.questBuild, "assemble");
        _stage = Stage.Done;
        Changed();
    }

    // ── 공통 ──────────────────────────────────────────

    private static void Changed()
    {
        OnChanged?.Invoke();
        SaveIfDocked();
    }

    // 진행이 바뀌면 도킹 중일 때 바로 저장한다 (도킹 저장은 들어올 때만 하므로)
    private static void SaveIfDocked()
    {
        if (StationZone.Current != null && !SaveSystem.IsLoading && !MissionDirector.IsRunning)
            SaveSystem.Save(null);
    }

    private static void CompleteObjective(string questId, string objectiveId)
    {
        QuestTracker tracker = FindObjectOfType<QuestTracker>(true);
        if (tracker != null && tracker.CurrentQuestId == questId)
            tracker.Complete(questId, objectiveId);
    }

    /// <summary>개발자 명령: 그 단계로 바로 옮긴다. 앞 단계의 재료·물품은 채워 둔다.</summary>
    public static void DevSetStage(Stage stage)
    {
        FirstShipQuestData data = Data;
        ResetState();
        _stage = stage;
        if (stage > Stage.Minerals)
            foreach (FirstShipQuestData.Material m in data.materials)
                Delivered[m.oreId] = m.amount;
        if (stage > Stage.Alloy)
            _alloys = data.alloyRequired;
        if (stage > Stage.Core)
            _core = true;
        Changed();
    }

    // ── 저장 ───────────────────────────────────────────

    public static SaveState ToSave()
    {
        return new SaveState
        {
            stage = (int)_stage,
            delivered = Delivered.Select(p => $"{p.Key}:{p.Value}").ToArray(),
            alloys = _alloys,
            core = _core,
            commsLog = _commsLog,
        };
    }

    public static void Restore(SaveState state)
    {
        ResetState();
        if (state != null)
        {
            _stage = (Stage)Mathf.Clamp(state.stage, 0, (int)Stage.Done);
            foreach (string entry in state.delivered ?? new string[0])
            {
                string[] parts = entry.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out int amount))
                    Delivered[parts[0]] = amount;
            }

            _alloys = state.alloys;
            _core = state.core;
            _commsLog = state.commsLog;
        }

        OnChanged?.Invoke();
    }
}
