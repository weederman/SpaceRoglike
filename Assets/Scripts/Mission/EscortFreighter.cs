using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 호송 임무에서 지키는 화물선. 프리팹: Assets/Resources/Missions/EscortFreighter.prefab (SF RF5 화물선 모델)
/// 워프 준비 동안 앞쪽으로 천천히 나아가고, 준비가 끝나면 플레이어 워프와 같은 방식(WarpDash)으로 워프해 사라진다.
/// 쉴드 없이 선체(HullHealth)만 있다. 아군(FriendlyShip)이라 플레이어의 무기에는 맞지 않는다.
/// 체력이 0이 되면 ShipDestructionEffect가 폭발시키고 제거한다.
/// </summary>
[RequireComponent(typeof(FriendlyShip))]
public class EscortFreighter : MonoBehaviour
{
    [SerializeField] private HullHealth _hull;
    [Tooltip("워프 준비 동안 나아가는 속도")]
    [SerializeField, Min(0f)] private float _cruiseSpeed = 6f;
    [Tooltip("워프 게이트가 충전된 뒤 이만큼 돌진하고 사라진다")]
    [SerializeField, Min(1f)] private float _warpDistance = 400f;
    [SerializeField, Min(0.1f)] private float _warpDashSeconds = 1.4f;

    /// <summary>워프로 사라졌을 때 (파괴가 아님)</summary>
    public event Action OnWarpedOut;

    public HullHealth Hull => _hull;
    public bool IsWarping { get; private set; }

    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    /// <summary>체력과 나아갈 방향을 정한다.</summary>
    public void Init(float maxHp, Vector3 heading)
    {
        _hull.SetMaxHp(maxHp);

        heading.y = 0f;
        if (heading.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(heading.normalized);
    }

    private void FixedUpdate()
    {
        if (IsWarping || !_hull.IsActive)
            return;

        Vector3 next = transform.position + transform.forward * (_cruiseSpeed * Time.fixedDeltaTime);
        if (_rb != null)
            _rb.MovePosition(next);
        else
            transform.position = next;
    }

    /// <summary>워프 게이트를 열고 돌진해 사라진다. 워프하는 동안은 피해를 받지 않는다.</summary>
    public void WarpOut(GameObject warpPrefab)
    {
        if (IsWarping || !_hull.IsActive)
            return;

        IsWarping = true;
        _hull.Invincible = true;
        StartCoroutine(WarpRoutine(warpPrefab));
    }

    private IEnumerator WarpRoutine(GameObject warpPrefab)
    {
        Vector3 start = transform.position;
        Vector3 dir = transform.forward;
        GameObject fx = WarpDash.SpawnGate(warpPrefab, start, dir);
        GameAudio.Play("warp_start");

        Action<Vector3> move = p =>
        {
            transform.position = p;
            if (_rb != null)
                _rb.position = p;
        };

        yield return WarpDash.Launch(start, dir * _cruiseSpeed, WarpDash.GateChargeSeconds, dir,
            _warpDistance, _warpDashSeconds, move);

        if (fx != null)
            Destroy(fx, 3f);

        OnWarpedOut?.Invoke();
        Destroy(gameObject);
    }
}
