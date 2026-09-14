using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SpaceshipController : MonoBehaviour
{
    [Header("Click Marker")]
    [SerializeField] private GameObject clickMarkerPrefab;

    private GameObject currentClickMarker;

    [Header("Movement")]
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float maxSpeed = 12f;

    [Header("Turning")]
    [SerializeField] private float rotationSpeed = 5f;

    [Header("Braking")]
    [SerializeField] private float brakingPower = 4f;
    [SerializeField] private float stoppingDistance = 1.5f;

    [Header("Camera Zoom")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float zoomSpeed = 3f;
    [SerializeField] private float minZoom = 3f;
    [SerializeField] private float maxZoom = 15f;

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

        // 카메라를 Inspector에 넣지 않았으면 Main Camera 자동 검색
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    private void Update()
    {
        HandleMouseInput();
        HandleCameraZoom();
    }

    private void FixedUpdate()
    {
        MoveToTarget();
        RotateToMovement();
        LimitSpeed();
    }

    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            Plane groundPlane =
                new Plane(Vector3.up, Vector3.zero);

            if (groundPlane.Raycast(ray, out float distance))
            {
                targetPosition = ray.GetPoint(distance);

                targetPosition.y = transform.position.y;

                hasTarget = true;

                SpawnClickMarker(targetPosition);
            }
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

        currentClickMarker = Instantiate(
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

        float distance = direction.magnitude;

        if (distance <= stoppingDistance)
        {
            Brake();

            if (GetHorizontalSpeed() < 0.2f)
            {
                rb.velocity = new Vector3(
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
        Vector3 velocity = rb.velocity;

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
                rotationSpeed *
                Time.fixedDeltaTime
            );

        rb.MoveRotation(newRotation);
    }

    private void LimitSpeed()
    {
        Vector3 velocity = rb.velocity;

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

            rb.velocity = new Vector3(
                horizontalVelocity.x,
                velocity.y,
                horizontalVelocity.z
            );
        }
    }

    private float GetHorizontalSpeed()
    {
        Vector3 velocity = rb.velocity;

        velocity.y = 0f;

        return velocity.magnitude;
    }

    // =========================================================
    // 카메라 줌
    // =========================================================

    private void HandleCameraZoom()
    {
        if (mainCamera == null)
            return;

        float scroll =
            Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) < 0.01f)
            return;

        float newZoom =
            mainCamera.orthographicSize -
            scroll * zoomSpeed;

        newZoom =
            Mathf.Clamp(
                newZoom,
                minZoom,
                maxZoom
            );

        mainCamera.orthographicSize =
            newZoom;
    }
}