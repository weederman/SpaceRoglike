using System;
using UnityEngine;

/// <summary>
/// 메인 스토리 ① 첫 번째 함선 해금 퀘스트의 정의(단일 출처). 위치: Assets/Resources/MainQuest/FirstShipQuest.asset
/// 기획 문서 4장 "첫 번째 함선 해금" (docs/game-design.md 3장): 라스의 연락 → 블루프린트 구매 → 광물 납품 → 특수 합금 회수
/// → 고급 동력 코어 확보 → 최종 조립. 진행은 FirstShipQuest가 하고, 대사는 Story JSON(MainFirstShip.json)의 시퀀스 id로 부른다.
/// </summary>
[CreateAssetMenu(fileName = "FirstShipQuest", menuName = "SpaceRoglike/First Ship Quest")]
public class FirstShipQuestData : ScriptableObject
{
    public const string ResourcePath = "MainQuest/FirstShipQuest";

    [Serializable]
    public class Material
    {
        [Tooltip("OreCatalog의 광석 id")]
        public string oreId;
        [Min(1)] public int amount = 100;
    }

    [Header("1. 새로운 제안 / 2. 설계도와 대가")]
    [Tooltip("라스의 첫 연락이 오는 보유 자금")]
    [Min(0)] public int unlockCredits = 1000000;
    [Min(0)] public int blueprintPrice = 1000000;
    public string blueprintName = "1차 특수 함선 블루프린트";

    [Header("3. 선체부터 준비해 (모항의 라스에게 납품, 여러 번 나눠 낼 수 있다)")]
    public Material[] materials = new Material[0];

    [Header("4. 버려진 화물 / 5. 흔적을 쫓아서")]
    public string alloyName = "특수 합금";
    [Min(1)] public int alloyRequired = 10;
    public string coreName = "고급 동력 코어";
    public string commsLogName = "적 지휘관 통신 기록";
    [Tooltip("특수 합금 회수 임무 (MissionData id)")]
    public string alloyMission = "main_1_4";
    [Tooltip("고급 동력 코어 확보 임무 (MissionData id)")]
    public string coreMission = "main_1_5";

    [Header("6. 새로운 함선")]
    [Tooltip("지급하는 함선 (ShipCatalog id)")]
    public string shipId = "ironwall";

    [Header("대사 (Story JSON 시퀀스 id)")]
    [Tooltip("라스의 첫 연락 (선택지: 자세히 / 구매 / 보류)")]
    public string offerSequence = "first_ship_offer";
    [Tooltip("보류한 뒤 라운지에서 라스와 다시 통신")]
    public string offerAgainSequence = "first_ship_offer_again";
    [Tooltip("구매 직후 (이어서 광물 퀘스트 안내)")]
    public string purchasedSequence = "first_ship_purchased";
    public string noMoneySequence = "first_ship_no_money";
    [Tooltip("광물을 모두 납품한 뒤 (이어서 특수 합금 퀘스트 안내)")]
    public string mineralsDoneSequence = "first_ship_minerals_done";
    [Tooltip("최종 조립 (함선 지급 cue 포함)")]
    public string assemblySequence = "first_ship_assembly";
    [Tooltip("선택지에서 구매를 뜻하는 선택지 id")]
    public string buyChoiceId = "buy";

    [Header("HUD 목표 창 (Story JSON quests의 id)")]
    public string questMinerals = "first_ship_minerals";
    public string questAlloy = "first_ship_alloy";
    public string questCore = "first_ship_core";
    public string questBuild = "first_ship_build";

    private static FirstShipQuestData _loaded;

    public static FirstShipQuestData Load()
    {
        if (_loaded == null)
            _loaded = Resources.Load<FirstShipQuestData>(ResourcePath);
        return _loaded;
    }
}
