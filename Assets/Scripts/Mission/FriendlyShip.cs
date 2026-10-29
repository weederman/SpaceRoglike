using UnityEngine;

/// <summary>
/// 플레이어 편 함선 표시 (호위 임무의 화물선 등). 함선 루트에 붙인다.
///  - 플레이어의 무기는 이 함선에 피해를 주지 않는다 (HitResolver)
///  - 목표 지정(TargetingSystem)과 상황 음악(MusicDirector)에서 적으로 세지 않는다
/// </summary>
public class FriendlyShip : MonoBehaviour
{
    /// <summary>이 컴포넌트(또는 그 부모)가 아군 함선의 일부인지</summary>
    public static bool Is(Component part)
    {
        return part != null && part.GetComponentInParent<FriendlyShip>() != null;
    }
}
