using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SpaceshipController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float maxSpeed = 12f;

    [Header("Braking")]
    [SerializeField] private float brakingPower = 4f;
    [SerializeField] private float stoppingDistance = 1.5f;

    [Header("Click Marker")]
    [SerializeField] private GameObject clickMarkerPrefab;

    private GameObject currentClickMarker;

    private Rigidbody rb;

    private Vector3 targetPosition;
    private bool hasTarget = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.drag = 0.05f;
        rb.angularDrag = 0.5f;

        rb.constraints =
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;
    }

    private void Update()
    {
        HandleMovementInput();
    }

    private void FixedUpdate()
    {
        MoveToTarget();
        RotateToMovement();
        LimitSpeed();
    }

    private void HandleMovementInput()
    {
        // 우클릭 = 이동
        if (!Input.GetMouseButtonDown(1))
            return;

        Camera mainCamera = Camera.main;

        if (mainCamera == null)
            return;

        // 마우스 위치에서 Ray 발사
        Ray ray =
            mainCamera.ScreenPointToRay(Input.mousePosition);

        // 우주선이 있는 높이의 XZ 평면
        Plane groundPlane =
            new Plane(
                Vector3.up,
                new Vector3(
                    0f,
                    transform.position.y,
                    0f
                )
            );

        // Ray와 XZ 평면의 교차점
        if (groundPlane.Raycast(ray, out float distance))
        {
            targetPosition =
                ray.GetPoint(distance);

            targetPosition.y =
                transform.position.y;

            hasTarget = true;

            SpawnClickMarker(targetPosition);
        }
    }

    private void SpawnClickMarker(Vector3 position)
    {
        if (clickMarkerPrefab == null)
            return;

        if (currentClickMarker != null)
        {
            Destroy(currentClickMarker);
        }

        currentClickMarker =
            Instantiate(
                clickMarkerPrefab,
                position,
                Quaternion.Euler(90f, 0f, 0f)
            );

        currentClickMarker.transform.position =
            new Vector3(
                position.x,
                0.02f,
                position.z
            );
    }

    private void MoveToTarget()
    {
        if (!hasTarget)
            return;

        Vector3 direction =
            targetPosition - transform.position;

        direction.y = 0f;

        float distance =
            direction.magnitude;

        // 목적지 근처
        if (distance <= stoppingDistance)
        {
            Brake();

            if (GetHorizontalSpeed() < 0.2f)
            {
                rb.velocity =
                    new Vector3(
                        0f,
                        rb.velocity.y,
                        0f
                    );

                hasTarget = false;
            }

            return;
        }

        direction.Normalize();

        rb.AddForce(
            direction * acceleration,
            ForceMode.Acceleration
        );
    }

    private void Brake()
    {
        Vector3 horizontalVelocity =
            new Vector3(
                rb.velocity.x,
                0f,
                rb.velocity.z
            );

        rb.AddForce(
            -horizontalVelocity * brakingPower,
            ForceMode.Acceleration
        );
    }

    private void RotateToMovement()
    {
        Vector3 velocity =
            rb.velocity;

        velocity.y = 0f;

        if (velocity.sqrMagnitude < 0.1f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(
                velocity.normalized
            );

        Quaternion newRotation =
            Quaternion.Slerp(
                rb.rotation,
                targetRotation,
                5f * Time.fixedDeltaTime
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

    private float GetHorizontalSpeed()
    {
        Vector3 velocity =
            rb.velocity;

        velocity.y = 0f;

        return velocity.magnitude;
    }
}