using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 정거장마다 스페이스 라운지에서 받을 수 있는 임무 목록. 정거장마다 임무 데이터(MissionDirector.AllMissions) 중
/// 정해진 수만큼 무작위로 뽑아 두고, 어떤 임무든 끝나면(MissionDirector.Ended: 성공·실패·중단) 모든 정거장의 목록을 새로 뽑는다.
/// 그래서 정거장을 드나들어도 목록은 그대로다. 게임을 다시 시작하면 새로 뽑는다(저장하지 않는다).
/// </summary>
public static class MissionBoard
{
    // 정거장 번호 → 뽑아 둔 임무 id
    private static readonly Dictionary<int, List<string>> Offers = new Dictionary<int, List<string>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Offers.Clear();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    // 플레이를 시작할 때 MissionDirector.Ended가 비워지므로(정적 초기화), 씬을 불러올 때 구독한다 (중복 없이)
    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Offers.Clear();
        MissionDirector.Ended -= Reroll;
        MissionDirector.Ended += Reroll;
    }

    /// <summary>모든 정거장의 목록을 버린다 (다음에 열 때 새로 뽑는다)</summary>
    public static void Reroll()
    {
        Offers.Clear();
    }

    /// <summary>이 정거장에서 받을 수 있는 임무 (처음 열면 count개를 무작위로 뽑는다)</summary>
    public static List<MissionData> OffersAt(StationZone zone, int count)
    {
        int key = zone != null ? zone.StationIndex : -1;
        List<MissionData> all = MissionDirector.AllMissions().Where(m => m.OnBoard).ToList();

        if (!Offers.TryGetValue(key, out List<string> ids))
        {
            ids = all.OrderBy(_ => Random.value).Take(count).Select(m => m.id).ToList();
            Offers[key] = ids;
        }

        // 뽑은 순서가 아니라 임무 번호 순으로 보여 준다
        return all.Where(m => ids.Contains(m.id)).ToList();
    }
}
