using UnityEngine;

/// <summary>
/// 임무 하나의 정의에서 모든 임무 유형이 공통으로 갖는 부분 (번호·이름·의뢰인·보상·대사).
/// 유형별 내용은 하위 클래스가 더한다: EscortMissionData(호송), SweepMissionData(해적 소탕).
/// 에셋 위치: Assets/Resources/Missions/ 아래 (유형별 폴더). 실행은 유형별 MissionDirector가 한다.
/// </summary>
public abstract class MissionData : ScriptableObject
{
    public const string ResourceFolder = "Missions";

    [Header("임무")]
    [Tooltip("개발자 명령에서 부르는 번호 (예: 1-1)")]
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

    [Header("대사 (Story JSON 시퀀스 id)")]
    public string acceptSequence;
    public string successSequence;

    /// <summary>이 임무를 실행하는 Director 프리팹 (Resources 경로)</summary>
    public abstract string DirectorPath { get; }

    /// <summary>임무 목록에 보여줄 한 줄 요약 (예: "90초 호위")</summary>
    public abstract string Summary { get; }
}
