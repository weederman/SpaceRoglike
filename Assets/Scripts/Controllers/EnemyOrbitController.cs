using UnityEngine;

/// <summary>적 함선: 대상(기본 Player 태그)을 일정 거리를 유지하며 원형으로 공전한다.</summary>
public class EnemyOrbitController : ShipMovementBase
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Orbit")]
    [SerializeField] private float orbitDistance = 8f;
    [SerializeField] private float orbitSpeed = 3f;
    [SerializeField] private bool clockwise = true;

    public float OrbitDistance
    {
        get => orbitDistance;
        set => orbitDistance = Mathf.Max(0f, value);
    }

    private void Start()
    {
        // Target이 지정되지 않았다면 Player 태그를 찾는다
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");

            if (player != null)
                target = player.transform;
        }
    }

    protected override bool CanMove()
    {
        return target != null;
    }

    protected override void ApplyMovement()
    {
        // 대상 → 적 방향
        Vector3 offset = transform.position - target.position;
        offset.y = 0f;

        float distance = offset.magnitude;

        if (distance < 0.01f)
            return;

        Vector3 radialDirection = offset.normalized;

        // 원의 접선 방향
        Vector3 tangentDirection = clockwise
            ? new Vector3(radialDirection.z, 0f, -radialDirection.x)
            : new Vector3(-radialDirection.z, 0f, radialDirection.x);

        // 대상과의 거리 유지
        float distanceError = distance - orbitDistance;
        Vector3 radialCorrection = -radialDirection * distanceError;

        Vector3 desiredDirection = tangentDirection * orbitSpeed + radialCorrection;

        if (desiredDirection.sqrMagnitude > 0.01f)
        {
            desiredDirection.Normalize();
            rb.AddForce(desiredDirection * acceleration, ForceMode.Acceleration);
        }
    }
}
