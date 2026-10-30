using UnityEngine;

/// <summary>
/// 임무 하나의 정의에서 모든 임무 유형이 공통으로 갖는 부분 (번호·이름·의뢰인·보상·대사).
/// 유형별 내용은 하위 클래스가 더한다: EscortMissionData(호송), SweepMissionData(해적 소탕), MiningMissionData(채굴), TransportMissionData(운송).
/// 에셋 위치: Assets/Resources/Missions/ 아래 (유형별 폴더). 실행은 유형별 MissionDirector가 한다.
/// </summary>
public abstract class MissionData : ScriptableObject
{
    public const string ResourceFolder = "Missions";

    [Header("임무")]
    [Tooltip("임무 번호 (예: 1-1). 정거장 목록 순서와 개발자 명령 mission start에 쓴다")]
    public string id = "1-1";
    public string title;
    [Tooltip("초급 / 중급 / 상급")]
    public string tier;
    public string client;
    [TextArea] public string objective;

    [Header("보상")]
    public int reward = 8000;
    [Tooltip("돈이 아닌 보상 (평판·우대권 등). 아직 시스템이 없어 결과 화면에 표시만 한다")]
    public string[] extraRewards = new string[0];

    [Header("희귀 부품 (중급·상급 임무의 무작위 보상)")]
    [Tooltip("성공했을 때 희귀 부품을 받을 확률 (0이면 없음)")]
    [Range(0f, 1f)] public float rarePartChance;
    [Tooltip("받는 개수 (최소~최대)")]
    public Vector2Int rarePartAmount = new Vector2Int(1, 1);
    [Tooltip("희귀 부품이 될 수 있는 항목 (OreCatalog의 id). 이 중 하나가 무작위로 나온다")]
    public string[] rarePartPool = { "rare_crystal" };

    [Header("대사 (Story JSON 시퀀스 id)")]
    public string acceptSequence;
    public string successSequence;

    /// <summary>이 임무를 실행하는 Director 프리팹 (Resources 경로)</summary>
    public abstract string DirectorPath { get; }

    /// <summary>임무 목록에 보여줄 한 줄 요약 (예: "90초 호위")</summary>
    public abstract string Summary { get; }

    /// <summary>스페이스 라운지 목록에 나오는 반복 임무인지. 메인 퀘스트 임무는 false (라스 항목에서 시작한다)</summary>
    public virtual bool OnBoard => true;

    /// <summary>지금 이 임무를 받을 수 있는지 (못 받으면 이유). 유형별 조건(운송 임무의 화물칸 자리 등)은 하위 클래스가 정한다.</summary>
    public virtual bool CanStart(out string error)
    {
        error = null;
        return true;
    }
}
