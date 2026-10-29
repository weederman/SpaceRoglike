using System;
using UnityEngine;

/// <summary>
/// 잔해 수색 임무의 정의 (메인 스토리 ① 4. 「버려진 화물」). 위치: Assets/Resources/Missions/Main/*.asset
/// 목적지 비콘에 도착하면 습격당한 화물선의 잔해 지대가 나타난다. 잔해 곁에 머무르면(salvageSeconds) 수색이 끝나고,
/// 잔해마다 특수 합금이 들어 있거나 비어 있다. 모두 모으면(FirstShipQuest의 특수 합금 필요량) 성공.
///  - 잔해 주변에는 해적이 대기한다. 가까이 다가가거나(aggroRadius) 공격하면 덤빈다 → 피해서 수색하거나 격퇴한다.
///  - 떠다니는 파편은 부딪히는 장애물이다.
/// 라운지 목록에는 나오지 않고 라스 항목에서 시작한다. 실행은 SalvageMissionDirector.
/// </summary>
[CreateAssetMenu(fileName = "SalvageMission", menuName = "SpaceRoglike/Salvage Mission")]
public class SalvageMissionData : MissionData
{
    [Serializable]
    public class Group
    {
        [Tooltip("EnemyShipCatalog의 함종 id")]
        public string shipClass = "light_fighter";
        [Min(1)] public int count = 1;
    }

    [Header("목적지")]
    [Tooltip("이 레벨 범위의 워프 비콘 중 지금 위치에서 가장 가까운 곳")]
    public Vector2Int beaconLevels = new Vector2Int(2, 3);

    [Header("잔해")]
    [Tooltip("특수 합금이 든 잔해 수 (필요량을 나눠 담는다)")]
    [Min(1)] public int filledWrecks = 4;
    [Tooltip("비어 있는 잔해 수 (수색해도 나오지 않는다)")]
    [Min(0)] public int emptyWrecks = 2;
    [Tooltip("잔해 모델 (차례대로 돌려 쓴다)")]
    public GameObject[] wreckModels = new GameObject[0];
    [Tooltip("잔해의 가장 긴 변")]
    [Min(1f)] public float wreckSize = 120f;
    [Tooltip("잔해 지대 중심에서 잔해까지 거리 (최소~최대)")]
    public Vector2 wreckDistance = new Vector2(150f, 650f);
    [Tooltip("이 거리 안에 머무르면 수색한다")]
    [Min(10f)] public float salvageRange = 150f;
    [Min(0.1f)] public float salvageSeconds = 4f;

    [Header("파편 (장애물)")]
    public GameObject[] debrisModels = new GameObject[0];
    [Min(0)] public int debrisCount = 14;
    public Vector2 debrisSize = new Vector2(25f, 60f);

    [Header("해적")]
    public Group[] guards = new Group[0];
    [Tooltip("플레이어가 해적에게 이보다 가까이 오면 덤빈다")]
    [Min(10f)] public float aggroRadius = 420f;

    public override string DirectorPath => "Missions/SalvageMissionDirector";
    public override string Summary => "잔해 수색 · 특수 합금 회수";
    public override bool OnBoard => false;
}
