using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyOrbitController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Orbit")]
    [SerializeField] private float orbitDistance = 8f;
    [SerializeField] private float orbitSpeed = 3f;
    [SerializeField] private bool clockwise = true;

    [Header("Movement")]
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float maxSpeed = 10f;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 5f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.drag = 0.05f;
        rb.angularDrag = 0.5f;

        rb.constraints =
            RigidbodyConstraints.FreezePositionY |
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;
    }

    private void Start()
    {
        // Target이 지정되지 않았다면 Player 태그를 찾음
        if (target == null)
        {
            GameObject player =
                GameObject.FindGameObjectWithTag("Player");

            if (player != null)
            {
                target = player.transform;
            }
        }
    }

    private void FixedUpdate()
    {
        if (target == null)
            return;

        OrbitTarget();
        RotateToMovement();
        LimitSpeed();
    }

    private void OrbitTarget()
    {
        // 플레이어 → 적 방향
        Vector3 offset =
            transform.position - target.position;

        offset.y = 0f;

        float distance = offset.magnitude;

        if (distance < 0.01f)
            return;

        Vector3 radialDirection =
            offset.normalized;

        // ====================================
        // 원의 접선 방향 계산
        // ====================================

        Vector3 tangentDirection;

        if (clockwise)
        {
            tangentDirection =
                new Vector3(
                    radialDirection.z,
                    0f,
                    -radialDirection.x
                );
        }
        else
        {
            tangentDirection =
                new Vector3(
                    -radialDirection.z,
                    0f,
                    radialDirection.x
                );
        }

        // ====================================
        // 플레이어와의 거리 유지
        // ====================================

        float distanceError =
            distance - orbitDistance;

        Vector3 radialCorrection =
            -radialDirection * distanceError;

        // ====================================
        // 최종 이동 방향
        // ====================================

        Vector3 desiredDirection =
            tangentDirection * orbitSpeed +
            radialCorrection;

        if (desiredDirection.sqrMagnitude > 0.01f)
        {
            desiredDirection.Normalize();

            rb.AddForce(
                desiredDirection * acceleration,
                ForceMode.Acceleration
            );
        }
    }

    private void RotateToMovement()
    {
        // 현재 속도
        Vector3 velocity =
            rb.velocity;

        velocity.y = 0f;

        // 너무 느리면 회전하지 않음
        if (velocity.sqrMagnitude < 0.1f)
            return;

        // 이동 방향을 바라봄
        Quaternion targetRotation =
            Quaternion.LookRotation(
                velocity.normalized
            );

        Quaternion newRotation =
            Quaternion.Slerp(
                rb.rotation,
                targetRotation,
                rotationSpeed * Time.fixedDeltaTime
            );

        rb.MoveRotation(newRotation);
    }

    private void LimitSpeed()
    {
        Vector3 velocity =
            rb.velocity;

        Vector3 horizontalVelocity =
            new Vector3(
                velocity.x,
                0f,
                velocity.z
            );

        if (horizontalVelocity.magnitude > maxSpeed)
        {
            horizontalVelocity =
                horizontalVelocity.normalized *
                maxSpeed;

            rb.velocity =
                new Vector3(
                    horizontalVelocity.x,
                    velocity.y,
                    horizontalVelocity.z
                );
        }
    }
}