using System;
using UnityEngine;

/// <summary>
/// 운송 임무 하나의 정의(단일 출처). 위치: Assets/Resources/Missions/Transport/*.asset
/// 임무를 받은 정거장(출발지)에서 화물을 화물칸에 싣고, 다른 정거장(도착지)으로 옮겨 그곳 미션 탭에서 납품한다.
///  - 도착지: 출발지에서 가까운 순으로 늘어놓은 다른 정거장 중 destinationRank 구간에서 무작위 (초급은 가까운 곳, 상급은 먼 곳)
///  - 도착지 지역에 들어오면 적 함대가 한 번만 나타난다. 모두 격파해야 도킹·납품할 수 있다.
///  - 화물은 광석과 같은 화물칸(CargoHold)에 실려 한도를 함께 쓴다. 자리가 모자라면 수락할 수 없다.
///  - failBelowHullRatio가 0보다 크면, 화물을 싣고 있는 동안 플레이어 선체가 그 비율 아래로 내려가면 실패(화물 파손).
/// 실행은 TransportMissionDirector가 한다.
/// </summary>
[CreateAssetMenu(fileName = "TransportMission", menuName = "SpaceRoglike/Transport Mission")]
public class TransportMissionData : MissionData
{
    [Serializable]
    public class Group
    {
        [Tooltip("EnemyShipCatalog의 함종 id")]
        public string shipClass = "light_fighter";
        [Min(1)] public int count = 1;
        [Tooltip("이 함종의 공격력 배율 (함종 기본 공격력 × 배율)")]
        [Min(0f)] public float damageMultiplier = 1f;
    }

    [Header("화물")]
    public string cargoName = "정비 부품";
    [Min(1)] public int cargoAmount = 10;

    [Header("도착지")]
    [Tooltip("출발지에서 가까운 순으로 늘어놓은 다른 정거장 중 이 구간(0 = 가장 가까운 곳, 1 = 가장 먼 곳)에서 고른다")]
    public Vector2 destinationRank = new Vector2(0f, 0.34f);
    [Tooltip("도착지 지역에 들어온 뒤 적이 나타날 때까지 (초)")]
    [Min(0f)] public float arrivalDelay = 3f;

    [Header("도착 시 적")]
    public Group[] fleet = new Group[0];

    [Header("실패 조건")]
    [Tooltip("화물을 싣고 있는 동안 플레이어 선체가 이 비율 미만이 되면 실패. 0이면 없음")]
    [Range(0f, 1f)] public float failBelowHullRatio;

    public override string DirectorPath => "Missions/TransportMissionDirector";
    public override string Summary => $"운송 · {cargoName} {cargoAmount:N0}개";

    public override bool CanStart(out string error)
    {
        CargoHold cargo = CargoHold.Instance;
        error = null;
        if (cargo != null && cargo.Capacity - cargo.Used < cargoAmount)
            error = $"화물칸 자리가 모자랍니다. {cargoName} {cargoAmount}개를 실을 자리가 필요합니다 " +
                    $"(지금 {Mathf.FloorToInt(cargo.Used)} / {Mathf.FloorToInt(cargo.Capacity)}). 광석을 팔고 오세요.";
        return error == null;
    }
}
