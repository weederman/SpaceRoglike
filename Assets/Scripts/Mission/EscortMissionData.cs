using System;
using UnityEngine;

/// <summary>
/// 호송 임무 하나의 정의(단일 출처). 위치: Assets/Resources/Missions/Escort/*.asset
/// 화물선이 워프 준비를 마칠 때까지(warpPrepSeconds) 지키면 성공, 화물선이 파괴되면 실패.
/// 적은 waves에 적힌 시각에 나타나며, 함종 이름은 EnemyShipCatalog의 id다.
/// 수락/성공 대사는 Assets/Resources/Story/MissionEscort.json의 시퀀스 id로 부른다.
/// 실행은 EscortMissionDirector가 한다.
/// </summary>
[CreateAssetMenu(fileName = "EscortMission", menuName = "SpaceRoglike/Escort Mission")]
public class EscortMissionData : MissionData
{
    /// <summary>적이 먼저 노리는 대상</summary>
    public enum Priority { Freighter, Player }

    [Serializable]
    public class Wave
    {
        [Tooltip("워프 준비 시작 후 이 시각(초)에 나타난다")]
        [Min(0f)] public float time;
        [Tooltip("EnemyShipCatalog의 함종 id")]
        public string shipClass = "light_fighter";
        [Min(1)] public int count = 1;
        public Priority target = Priority.Freighter;
    }

    [Header("화물선")]
    [Min(1f)] public float freighterHp = 4000f;
    [Tooltip("워프 준비에 걸리는 시간 = 지켜야 하는 시간 (초)")]
    [Min(1f)] public float warpPrepSeconds = 90f;

    [Header("적")]
    public Wave[] waves = new Wave[0];

    [Header("보상 조건")]
    [Tooltip("화물선 체력이 이 비율 미만으로 끝나면 reducedReward를 준다. 0이면 감소 없음")]
    [Range(0f, 1f)] public float reducedBelowHpRatio;
    public int reducedReward;

    [Tooltip("이 함종을 모두, 그 무기가 화물선에 닿기 전에 격파하면 bonusReward를 더 준다. 비우면 보너스 없음")]
    public string bonusShipClass;
    public int bonusReward;
    [Tooltip("결과 화면에 보이는 보너스 이름")]
    public string bonusLabel;

    public override string DirectorPath => "Missions/EscortMissionDirector";
    public override string Summary => $"호송 · {warpPrepSeconds:0}초";

    /// <summary>화물선이 남긴 체력 비율에 따른 기본 보수 (보너스 제외)</summary>
    public int BaseReward(float freighterHpRatio)
    {
        return reducedBelowHpRatio > 0f && freighterHpRatio < reducedBelowHpRatio ? reducedReward : reward;
    }
}
