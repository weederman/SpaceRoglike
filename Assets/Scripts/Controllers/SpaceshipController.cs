using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SpaceshipController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float maxSpeed = 12f;

    [Header("Braking")]
    [SerializeField] private float brakingPower = 4f;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 8f;

    [Header("Engine Sound")]
    [SerializeField] private AudioSource engineAudio;
    [SerializeField] private float engineMinVolume = 0f;
    [SerializeField] private float engineMaxVolume = 0.7f;
    [SerializeField] private float engineMinPitch = 0.8f;
    [SerializeField] private float engineMaxPitch = 1.3f;
    [SerializeField] private float engineFadeSpeed = 5f;

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
        UpdateEngineSound();
    }

    private void FixedUpdate()
    {
        HandleMovement();
        RotateToMovement();
        LimitSpeed();
    }

    // =========================
    // RPG 스타일 WASD 이동
    // =========================

    private void HandleMovement()
    {
        float horizontal =
            Input.GetAxisRaw("Horizontal");

        float vertical =
            Input.GetAxisRaw("Vertical");

        Vector3 inputDirection =
            new Vector3(
                horizontal,
                0f,
                vertical
            );

        // 대각선 이동 속도 보정
        if (inputDirection.sqrMagnitude > 1f)
        {
            inputDirection.Normalize();
        }

        // ====================================
        // 입력이 없으면 감속
        // ====================================

        if (inputDirection.sqrMagnitude < 0.01f)
        {
            Brake();
            return;
        }

        // ====================================
        // RPG 방식
        //
        // 월드 기준으로 WASD 이동
        // W = +Z
        // S = -Z
        // A = -X
        // D = +X
        // ====================================

        Vector3 moveDirection =
            inputDirection.normalized;

        rb.AddForce(
            moveDirection * acceleration,
            ForceMode.Acceleration
        );
    }

    // =========================
    // 감속
    // =========================

    private void Brake()
    {
        Vector3 horizontalVelocity =
            new Vector3(
                rb.velocity.x,
                0f,
                rb.velocity.z
            );

        if (horizontalVelocity.sqrMagnitude < 0.01f)
            return;

        rb.AddForce(
            -horizontalVelocity * brakingPower,
            ForceMode.Acceleration
        );
    }

    // =========================
    // 이동 방향을 바라보기
    // =========================

    private void RotateToMovement()
    {
        Vector3 velocity =
            rb.velocity;

        velocity.y = 0f;

        if (velocity.sqrMagnitude < 0.1f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(
                velocity.normalized,
                Vector3.up
            );

        Quaternion newRotation =
            Quaternion.Slerp(
                rb.rotation,
                targetRotation,
                rotationSpeed * Time.fixedDeltaTime
            );

        rb.MoveRotation(newRotation);
    }

    // =========================
    // 최대 속도 제한
    // =========================

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

    // =========================
    // 현재 수평 속도
    // =========================

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

        float speed =
            GetHorizontalSpeed();

        float speedRatio =
            Mathf.Clamp01(
                speed / maxSpeed
            );

        // 속도에 따른 볼륨
        float targetVolume =
            Mathf.Lerp(
                engineMinVolume,
                engineMaxVolume,
                speedRatio
            );

        // 속도에 따른 Pitch
        float targetPitch =
            Mathf.Lerp(
                engineMinPitch,
                engineMaxPitch,
                speedRatio
            );

        // 볼륨 부드럽게 변화
        engineAudio.volume =
            Mathf.Lerp(
                engineAudio.volume,
                targetVolume,
                engineFadeSpeed * Time.deltaTime
            );

        // Pitch 부드럽게 변화
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
            if (engineAudio.isPlaying &&
                engineAudio.volume < 0.01f)
            {
                engineAudio.Stop();
            }
        }
    }
}