using System;
using UnityEngine;

/// <summary>
/// 채굴 임무 하나의 정의(단일 출처). 위치: Assets/Resources/Missions/Mining/*.asset
/// 광석을 캐서 임무를 받은 정거장의 미션 탭에서 납품한다. 여러 번 나눠 납품할 수 있고, 목표량을 채우면 성공.
/// 임무용 광석을 따로 두지 않고 화물칸(CargoHold)의 광석을 그대로 납품한다 (oreId가 비면 종류 상관없음).
///  - 목적지: 임무 레벨 범위의 비콘 중 가장 가까운 곳 (지도에 표시). 다른 곳에서 캔 광석도 인정한다.
///  - 해적 순찰대: 목적지에서 채굴하는 동안 일정 간격마다 확률로 나타난다 (시간이 지날수록 확률이 오를 수 있다).
///  - 보수: reward(임무 보너스) + 납품한 광석의 시장가(payMarketValue) + 납품량 단계별 보너스(stages)
/// 실행은 MiningMissionDirector가 한다.
/// </summary>
[CreateAssetMenu(fileName = "MiningMission", menuName = "SpaceRoglike/Mining Mission")]
public class MiningMissionData : MissionData
{
    [Serializable]
    public class Stage
    {
        [Tooltip("누적 납품량이 이만큼이 되면")]
        [Min(1)] public int delivered = 100;
        [Tooltip("이만큼 보너스를 바로 준다")]
        [Min(0)] public int bonus = 2000;
    }

    [Serializable]
    public class Group
    {
        [Tooltip("EnemyShipCatalog의 함종 id")]
        public string shipClass = "light_fighter";
        [Min(1)] public int count = 1;
    }

    [Header("납품")]
    [Tooltip("인정하는 광석 (OreCatalog의 id). 비우면 종류 상관없음")]
    public string oreId = "iron";
    [Min(1)] public int amount = 100;
    [Tooltip("납품한 광석의 시장가도 함께 준다")]
    public bool payMarketValue;
    public Stage[] stages = new Stage[0];

    [Header("목적지")]
    [Tooltip("이 레벨 범위의 워프 비콘 중 지금 위치에서 가장 가까운 곳")]
    public Vector2Int beaconLevels = new Vector2Int(1, 2);

    [Header("해적 순찰대 (확률 0이면 없음)")]
    [Tooltip("목적지에서 이 간격(초)마다 순찰대가 나타날지 굴린다")]
    [Min(1f)] public float patrolCheckSeconds = 20f;
    [Range(0f, 1f)] public float patrolChance;
    [Tooltip("임무를 받은 뒤 1분마다 확률이 이만큼 오른다")]
    [Range(0f, 1f)] public float patrolChancePerMinute;
    public Group[] patrol = new Group[0];

    public override string DirectorPath => "Missions/MiningMissionDirector";
    public override string Summary => $"채굴 납품 · {OreLabel} {amount:N0}개";

    /// <summary>"철광석" 또는 "광석(종류 무관)"</summary>
    public string OreLabel => string.IsNullOrEmpty(oreId) ? "광석" : OreCatalog.NameOf(oreId);
}
