using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 우주정거장 하나. 플레이어가 이 정거장에 도킹해 있는 동안 "안에 있음"(IsInside) 상태가 되고,
/// 그동안 상점·수리(StationShop)가 열리고 자동 저장(SaveSystem)이 된다. 도킹/출항은 StationDocking이 정한다.
/// 정거장은 여러 개일 수 있다(항성계의 행성 궤도마다 하나). 씬에 놓인 정거장이 모항(Home, 튜토리얼 정거장)이고,
/// 항성계(StarSystem)가 런타임에 만드는 나머지 정거장은 Init으로 이름을 정한다.
/// 위치는 이 오브젝트의 위치다. (씬 모항의 흰 평면 _area는 예전 구역 표시로, 지금은 보이지 않게 하고 판정에 쓰지 않는다)
/// </summary>
public class StationZone : MonoBehaviour
{
    [Tooltip("예전 정거장 구역 표시(흰 평면). 지금은 쓰지 않고 숨긴다.")]
    [SerializeField] private Renderer _area;

    /// <summary>지금 있는 정거장 전부</summary>
    public static readonly List<StationZone> All = new List<StationZone>();

    /// <summary>어느 정거장이든 도킹했을 때 / 출항했을 때</summary>
    public static event Action<StationZone> AnyEntered;
    public static event Action<StationZone> AnyExited;

    /// <summary>씬에 놓인 모항 정거장 (튜토리얼 정거장). 없으면 null.</summary>
    public static StationZone Home
    {
        get
        {
            foreach (StationZone zone in All)
            {
                if (zone.IsHome)
                    return zone;
            }

            return null;
        }
    }

    /// <summary>플레이어가 도킹해 있는 정거장 (없으면 null)</summary>
    public static StationZone Current
    {
        get
        {
            foreach (StationZone zone in All)
            {
                if (zone.IsInside)
                    return zone;
            }

            return null;
        }
    }

    public event Action Entered;
    public event Action Exited;

    /// <summary>플레이어가 이 정거장에 도킹해 있는지</summary>
    public bool IsInside { get; private set; }

    /// <summary>예전 구역 표시 Renderer (모항의 흰 평면). 없으면 null.</summary>
    public Renderer Area => _area;
    public bool IsHome { get; private set; } = true;

    /// <summary>정거장 이름 (항성계 데이터에서 받는다. 모항은 StarSystem이 채운다)</summary>
    public string DisplayName { get; set; }

    /// <summary>항성계 데이터의 정거장 번호 (StarSystem이 채운다, 없으면 -1). 저장에 쓴다.</summary>
    public int StationIndex { get; set; } = -1;

    /// <summary>정거장 모델 (도킹 거리를 재는 데 쓴다. StarSystem이 채운다)</summary>
    public Transform Model { get; set; }

    // 에디터에서 도메인 리로드를 꺼 둬도 플레이마다 깨끗이 시작하도록
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        All.Clear();
        AnyEntered = null;
        AnyExited = null;
    }

    /// <summary>런타임에 만든 정거장의 이름을 정한다(모항이 아님).</summary>
    public void Init(string displayName)
    {
        IsHome = false;
        DisplayName = displayName;
    }

    /// <summary>도킹(true) / 출항(false). StationDocking이 부른다.</summary>
    public void SetDocked(bool docked)
    {
        if (docked == IsInside)
            return;

        IsInside = docked;
        if (docked)
        {
            Entered?.Invoke();
            AnyEntered?.Invoke(this);
        }
        else
        {
            Exited?.Invoke();
            AnyExited?.Invoke(this);
        }
    }

    private void OnEnable()
    {
        All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
        SetDocked(false);
    }
}
