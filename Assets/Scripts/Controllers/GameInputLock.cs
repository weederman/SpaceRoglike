using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 입력(이동/발사/줌/배치 클릭)을 잠시 막는 잠금. 개발자 모드 창, 스토리 재생처럼
/// "지금은 플레이어 조작이 게임에 전달되면 안 되는" 쪽이 Lock(자신)으로 잠그고 Unlock(자신)으로 푼다.
/// 여러 곳이 동시에 잠글 수 있고, 모두 풀려야 입력이 돌아온다. 같은 주체가 두 번 잠그거나 풀어도 안전하다.
/// </summary>
public static class GameInputLock
{
    private static readonly HashSet<object> _owners = new HashSet<object>();

    public static bool IsLocked => _owners.Count > 0;

    public static void Lock(object owner)
    {
        if (owner != null)
            _owners.Add(owner);
    }

    public static void Unlock(object owner)
    {
        if (owner != null)
            _owners.Remove(owner);
    }

    /// <summary>자신을 뺀 다른 곳이 잠그고 있는지 (예: 개발자 창이 열려 있으면 스토리가 클릭을 받지 않도록).</summary>
    public static bool IsLockedExcept(object owner)
    {
        return _owners.Count > (_owners.Contains(owner) ? 1 : 0);
    }

    // 에디터에서 도메인 리로드를 꺼 둬도 플레이마다 깨끗이 시작하도록
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        _owners.Clear();
    }
}
