using UnityEngine;

/// <summary>
/// 함선 이동의 기반 클래스 (지금은 플레이어 함선 SpaceshipController가 쓴다).
/// Rigidbody 초기 설정, 이동 방향 바라보기, 최대 속도 제한, 엔진 사운드를 담당하고,
/// "어떤 힘을 줄 것인가"(WASD 입력)만 자식 클래스가 ApplyMovement로 정한다.
/// 스스로 움직이는 다른 함선(해적선 PirateShip)도 Rigidbody 설정은 ConfigureBody로 같게 맞춘다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public abstract class ShipMovementBase : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] protected float acceleration = 8f;
    [SerializeField] protected float maxSpeed = 12f;

    [Header("Rotation")]
    [SerializeField] protected float rotationSpeed = 8f;

    [Header("Engine Sound")]
    [SerializeField] private AudioSource engineAudio;
    [SerializeField] private float engineMinVolume = 0f;
    [SerializeField] private float engineMaxVolume = 0.7f;
    [SerializeField] private float engineMinPitch = 0.8f;
    [SerializeField] private float engineMaxPitch = 1.3f;
    [SerializeField] private float engineFadeSpeed = 5f;

    protected Rigidbody rb;

    public float MaxSpeed
    {
        get => maxSpeed;
        set => maxSpeed = Mathf.Max(0f, value);
    }

    public float Acceleration
    {
        get => acceleration;
        set => acceleration = Mathf.Max(0f, value);
    }

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ConfigureBody(rb);

        if (engineAudio != null)
        {
            engineAudio.loop = true;
            engineAudio.playOnAwake = false;
            engineAudio.volume = 0f;
            GameSettings.Route(engineAudio, VolumeChannel.Sfx); // 음량 설정: 효과음
        }
    }

    /// <summary>함선 Rigidbody 공통 설정: 약한 감속, 보간, XZ 평면 이동과 Y축 회전만</summary>
    public static void ConfigureBody(Rigidbody rb)
    {
        rb.drag = 0.05f;
        rb.angularDrag = 0.5f;

        // 물리(FixedUpdate)와 화면 프레임 사이를 보간한다. 꺼져 있으면 고속에서 함선 위치가
        // 프레임마다 들쭉날쭉해져, 따라가는 카메라가 떨린다.
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // XZ 평면에서만 움직이고 Y축으로만 회전한다
        rb.constraints =
            RigidbodyConstraints.FreezePositionY |
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;
    }

    private void Update()
    {
        UpdateEngineSound();
    }

    private void FixedUpdate()
    {
        if (!CanMove())
            return;

        ApplyMovement();
        RotateToMovement();
        LimitSpeed();
    }

    /// <summary>이번 물리 프레임에 이동 처리를 할지 (예: 적은 추적 대상이 있을 때만).</summary>
    protected virtual bool CanMove()
    {
        return true;
    }

    /// <summary>rb에 이동 힘을 준다.</summary>
    protected abstract void ApplyMovement();

    /// <summary>속도 방향으로 부드럽게 회전한다. 너무 느리면 회전하지 않는다.</summary>
    private void RotateToMovement()
    {
        Vector3 velocity = rb.velocity;
        velocity.y = 0f;

        if (velocity.sqrMagnitude < 0.1f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
    }

    private void LimitSpeed()
    {
        Vector3 velocity = rb.velocity;
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);

        if (horizontalVelocity.magnitude > maxSpeed)
        {
            horizontalVelocity = horizontalVelocity.normalized * maxSpeed;
            rb.velocity = new Vector3(horizontalVelocity.x, velocity.y, horizontalVelocity.z);
        }
    }

    protected float GetHorizontalSpeed()
    {
        Vector3 velocity = rb.velocity;
        velocity.y = 0f;
        return velocity.magnitude;
    }

    private void UpdateEngineSound()
    {
        if (engineAudio == null)
            return;

        float speed = GetHorizontalSpeed();
        float speedRatio = Mathf.Clamp01(speed / maxSpeed);

        float targetVolume = Mathf.Lerp(engineMinVolume, engineMaxVolume, speedRatio);
        float targetPitch = Mathf.Lerp(engineMinPitch, engineMaxPitch, speedRatio);

        engineAudio.volume = Mathf.Lerp(engineAudio.volume, targetVolume, engineFadeSpeed * Time.deltaTime);
        engineAudio.pitch = Mathf.Lerp(engineAudio.pitch, targetPitch, engineFadeSpeed * Time.deltaTime);

        // 움직이기 시작하면 재생하고, 멈춰서 소리가 사라지면 정지한다
        if (speed > 0.1f)
        {
            if (!engineAudio.isPlaying)
                engineAudio.Play();
        }
        else if (engineAudio.isPlaying && engineAudio.volume < 0.01f)
        {
            engineAudio.Stop();
        }
    }
}
