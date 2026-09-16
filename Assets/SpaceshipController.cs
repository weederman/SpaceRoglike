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

    [Header("Engine Sound")]
    [SerializeField] private AudioSource engineAudio;
    [SerializeField] private float engineMinVolume = 0f;
    [SerializeField] private float engineMaxVolume = 0.7f;
    [SerializeField] private float engineMinPitch = 0.8f;
    [SerializeField] private float engineMaxPitch = 1.3f;
    [SerializeField] private float engineFadeSpeed = 5f;

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
            RigidbodyConstraints.FreezePositionY |
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;

        // 엔진음 초기 설정
        if (engineAudio != null)
        {
            engineAudio.loop = true;
            engineAudio.playOnAwake = false;
            engineAudio.volume = 0f;
        }
    }

    private void Update()
    {
        HandleMovementInput();
        UpdateEngineSound();
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

        Ray ray =
            mainCamera.ScreenPointToRay(Input.mousePosition);

        Plane groundPlane =
            new Plane(
                Vector3.up,
                new Vector3(
                    0f,
                    transform.position.y,
                    0f
                )
            );

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

    // =========================
    // Engine Sound
    // =========================

    private void UpdateEngineSound()
    {
        if (engineAudio == null)
            return;

        float speed = GetHorizontalSpeed();

        // 0 ~ 1 사이로 속도 변환
        float speedRatio =
            Mathf.Clamp01(speed / maxSpeed);

        // 속도에 따라 목표 볼륨 계산
        float targetVolume =
            Mathf.Lerp(
                engineMinVolume,
                engineMaxVolume,
                speedRatio
            );

        // 속도에 따라 Pitch 변화
        float targetPitch =
            Mathf.Lerp(
                engineMinPitch,
                engineMaxPitch,
                speedRatio
            );

        // 부드럽게 볼륨 변화
        engineAudio.volume =
            Mathf.Lerp(
                engineAudio.volume,
                targetVolume,
                engineFadeSpeed * Time.deltaTime
            );

        // 부드럽게 Pitch 변화
        engineAudio.pitch =
            Mathf.Lerp(
                engineAudio.pitch,
                targetPitch,
                engineFadeSpeed * Time.deltaTime
            );

        // 움직이기 시작하면 재생
        if (speed > 0.1f)
        {
            if (!engineAudio.isPlaying)
            {
                engineAudio.Play();
            }
        }
        else
        {
            // 완전히 멈추면 정지
            if (engineAudio.isPlaying &&
                engineAudio.volume < 0.01f)
            {
                engineAudio.Stop();
            }
        }
    }
}