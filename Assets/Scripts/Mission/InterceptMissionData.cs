using System;
using UnityEngine;

/// <summary>
/// 수송선 요격 임무의 정의 (메인 스토리 ① 5. 「흔적을 쫓아서」). 위치: Assets/Resources/Missions/Main/*.asset
/// 목적지 비콘에 도착하면 군수품 수송선이 호위 함대와 함께 나타나 달아난다. 수송선을 격파하면 고급 동력 코어를 얻고 성공.
///  - escapeSeconds 안에 격파하지 못하면 수송선이 워프로 달아나 실패 (라스 항목에서 다시 출발할 수 있다).
///  - 지휘함을 격파하면 적 지휘관의 통신 기록을 얻는다 (선택 목표). 지휘함이 처음 맞거나 교전 뒤 몇 초가 지나면 지휘관 통신이 들린다.
/// 라운지 목록에는 나오지 않고 라스 항목에서 시작한다. 실행은 InterceptMissionDirector.
/// </summary>
[CreateAssetMenu(fileName = "InterceptMission", menuName = "SpaceRoglike/Intercept Mission")]
public class InterceptMissionData : MissionData
{
    [Serializable]
    public class Group
    {
        [Tooltip("EnemyShipCatalog의 함종 id")]
        public string shipClass = "interceptor";
        [Min(1)] public int count = 1;
    }

    [Header("목적지")]
    [Tooltip("이 레벨 범위의 워프 비콘 중 지금 위치에서 가장 가까운 곳")]
    public Vector2Int beaconLevels = new Vector2Int(3, 4);
    [Tooltip("도착한 뒤 함대가 나타날 때까지 (초)")]
    [Min(0f)] public float arrivalDelay = 3f;

    [Header("수송선")]
    [Tooltip("수송선 함종 (달아난다)")]
    public string transportClass = "munitions_transport";
    [Tooltip("이 시간 안에 격파하지 못하면 워프로 달아난다 (초)")]
    [Min(10f)] public float escapeSeconds = 150f;

    [Header("호위")]
    public Group[] escorts = new Group[0];
    [Tooltip("지휘함 함종 (비우면 없음). 격파하면 통신 기록")]
    public string commanderClass = "commander";

    [Header("지휘관 통신")]
    [Tooltip("교전 중 지휘관 통신 (조작하며 듣는 control 시퀀스)")]
    public string commanderSequence;
    [Tooltip("지휘함이 맞지 않아도 교전 뒤 이 시간이 지나면 통신이 들린다 (초)")]
    [Min(0f)] public float commanderLineDelay = 12f;
    [Tooltip("통신 기록 없이 성공했을 때의 성공 대사 (비우면 successSequence)")]
    public string successSequenceNoLog;

    public override string DirectorPath => "Missions/InterceptMissionDirector";
    public override string Summary => "수송선 요격 · 고급 동력 코어 확보";
    public override bool OnBoard => false;
}
