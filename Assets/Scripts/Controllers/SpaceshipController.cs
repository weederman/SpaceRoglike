using UnityEngine;

/// <summary>
/// 플레이어 함선: WASD로 화면 기준 이동(W=화면 위, S=아래, A=왼쪽, D=오른쪽), 입력이 없으면 감속.
/// 카메라를 돌리면(CameraController.Yaw) 이동 방향도 같이 돈다. 카메라가 기본 자리면 W=+Z, D=+X.
/// </summary>
public class SpaceshipController : ShipMovementBase
{
    [Header("Braking")]
    [SerializeField] private float brakingPower = 4f;

    private CameraController _camera;

    protected override void ApplyMovement()
    {
        // 개발자 모드 창 입력, 스토리 재생 중에는 WASD가 함선을 움직이지 않는다
        Vector3 inputDirection = GameInputLock.IsLocked
            ? Vector3.zero
            : new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));

        // 대각선 이동 속도 보정
        if (inputDirection.sqrMagnitude > 1f)
            inputDirection.Normalize();

        if (inputDirection.sqrMagnitude < 0.01f)
        {
            Brake();
            return;
        }

        rb.AddForce(Quaternion.Euler(0f, CameraYaw(), 0f) * inputDirection.normalized * acceleration, ForceMode.Acceleration);
    }

    private float CameraYaw()
    {
        if (_camera == null)
            _camera = FindObjectOfType<CameraController>();

        return _camera != null ? _camera.Yaw : 0f;
    }

    private void Brake()
    {
        Vector3 horizontalVelocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        if (horizontalVelocity.sqrMagnitude < 0.01f)
            return;

        rb.AddForce(-horizontalVelocity * brakingPower, ForceMode.Acceleration);
    }
}
