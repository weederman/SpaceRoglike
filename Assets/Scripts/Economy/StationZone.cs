using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 정거장 구역 감지. 기준 평면(_area)의 XZ 범위 안이면서 평면보다 위(y)에 플레이어가 있으면 "진입" 상태.
/// 플레이어 쉴드 콜라이더가 커서 물리 트리거 대신 플레이어 위치로 직접 판정한다.
/// 정거장은 여러 개일 수 있다(항성계의 행성 궤도마다 하나). 씬에 놓인 정거장이 모항(Home, 튜토리얼 정거장)이고,
/// 항성계(StarSystem)가 런타임에 만드는 나머지 정거장은 Init으로 구역을 정한다.
/// </summary>
public class StationZone : MonoBehaviour
{
    [Tooltip("정거장 영역을 표시하는 평면 오브젝트의 Renderer")]
    [SerializeField] private Renderer _area;

    /// <summary>지금 있는 정거장 구역 전부</summary>
    public static readonly List<StationZone> All = new List<StationZone>();

    /// <summary>어느 정거장 구역이든 플레이어가 들어왔을 때 / 나갔을 때</summary>
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

    /// <summary>플레이어가 들어와 있는 정거장 구역 (없으면 null)</summary>
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

    public bool IsInside { get; private set; }
    public bool IsHome { get; private set; } = true;

    /// <summary>정거장 이름 (항성계 데이터에서 받는다. 모항은 StarSystem이 채운다)</summary>
    public string DisplayName { get; set; }

    private Transform _player;
    private Bounds _bounds;

    // 에디터에서 도메인 리로드를 꺼 둬도 플레이마다 깨끗이 시작하도록
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        All.Clear();
        AnyEntered = null;
        AnyExited = null;
    }

    /// <summary>런타임에 만든 정거장: 구역을 표시하는 Renderer를 정한다(모항이 아님).</summary>
    public void Init(Renderer area, string displayName)
    {
        _area = area;
        IsHome = false;
        DisplayName = displayName;
    }

    private void OnEnable()
    {
        All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);

        if (IsInside)
        {
            IsInside = false;
            Exited?.Invoke();
            AnyExited?.Invoke(this);
        }
    }

    private void Start()
    {
        _bounds = _area.bounds;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            _player = player.transform;
    }

    private void Update()
    {
        if (_player == null)
            return;

        Vector3 p = _player.position;
        bool inside =
            p.x >= _bounds.min.x && p.x <= _bounds.max.x &&
            p.z >= _bounds.min.z && p.z <= _bounds.max.z &&
            p.y > _bounds.center.y;

        if (inside == IsInside)
            return;

        IsInside = inside;
        if (inside)
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
}
