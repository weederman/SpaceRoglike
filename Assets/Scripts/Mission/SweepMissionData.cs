using System;
using UnityEngine;

/// <summary>
/// 해적 소탕 임무 하나의 정의(단일 출처). 위치: Assets/Resources/Missions/Sweep/*.asset
/// 수락하면 정해진 레벨 범위의 워프 비콘 중 가장 가까운 곳이 목적지가 된다. 그 비콘에 도착하면 해적 함대가 나타나고,
/// 목표(전원 격파 또는 표적 격파)를 이루면 성공. 표적이 도주하면 실패.
///  - 지휘함(commanderClass): 정해진 시간까지 살아 있으면 증원(reinforcements)을 부른다. 먼저 격파하면 증원은 오지 않는다.
///  - 표적(targetClass): 체력이 fleeBelowHpRatio 이하가 되면 도주하고, 플레이어에게서 escapeDistance 이상 멀어지면 워프로 달아난다.
/// 함종 이름은 EnemyShipCatalog의 id, 대사는 Assets/Resources/Story/MissionSweep.json. 실행은 SweepMissionDirector가 한다.
/// </summary>
[CreateAssetMenu(fileName = "SweepMission", menuName = "SpaceRoglike/Sweep Mission")]
public class SweepMissionData : MissionData
{
    public enum Goal
    {
        /// <summary>나타난 적(증원 포함)을 모두 격파</summary>
        DestroyAll,
        /// <summary>표적 함선만 격파하면 된다 (호위함은 물러난다)</summary>
        DestroyTarget
    }

    [Serializable]
    public class Group
    {
        [Tooltip("EnemyShipCatalog의 함종 id")]
        public string shipClass = "light_fighter";
        [Min(1)] public int count = 1;
    }

    [Header("목적지")]
    [Tooltip("이 레벨 범위의 워프 비콘 중 지금 위치에서 가장 가까운 곳")]
    public Vector2Int beaconLevels = new Vector2Int(1, 2);
    [Tooltip("비콘에 도착한 뒤 적이 나타날 때까지 (초)")]
    [Min(0f)] public float arrivalDelay = 3f;

    [Header("적")]
    public Group[] fleet = new Group[0];
    public Goal goal = Goal.DestroyAll;

    [Header("지휘함과 증원 (비우면 없음)")]
    public string commanderClass;
    [Tooltip("적이 나타난 뒤 이 시간(초)까지 지휘함이 살아 있으면 증원이 온다")]
    [Min(0f)] public float reinforceAfterSeconds = 60f;
    public Group[] reinforcements = new Group[0];

    [Header("표적과 도주 (비우면 없음)")]
    public string targetClass;
    [Range(0f, 1f)] public float fleeBelowHpRatio = 0.5f;
    [Tooltip("도주하는 표적이 플레이어에게서 이만큼 멀어지면 달아난다 (실패)")]
    [Min(100f)] public float escapeDistance = 1400f;

    public override string DirectorPath => "Missions/SweepMissionDirector";
    public override string Summary => $"해적 소탕 · 비콘 레벨 {beaconLevels.x}~{beaconLevels.y}";
}
