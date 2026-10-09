using System;
using UnityEngine;

/// <summary>
/// 정거장 구역 감지. 기준 평면(_area)의 XZ 범위 안이면서 평면보다 위(y)에 플레이어가 있으면 "진입" 상태.
/// 플레이어 쉴드 콜라이더가 커서 물리 트리거 대신 플레이어 위치로 직접 판정한다.
/// </summary>
public class StationZone : MonoBehaviour
{
    [Tooltip("정거장 영역을 표시하는 평면 오브젝트의 Renderer")]
    [SerializeField] private Renderer _area;

    public event Action Entered;
    public event Action Exited;

    public bool IsInside { get; private set; }

    private Transform _player;
    private Bounds _bounds;

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
            Entered?.Invoke();
        else
            Exited?.Invoke();
    }
}
