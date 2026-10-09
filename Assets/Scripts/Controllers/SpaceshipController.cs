using UnityEngine;

/// <summary>플레이어 함선: WASD로 월드 기준 이동(W=+Z, S=-Z, A=-X, D=+X), 입력이 없으면 감속.</summary>
public class SpaceshipController : ShipMovementBase
{
    [Header("Braking")]
    [SerializeField] private float brakingPower = 4f;

    protected override void ApplyMovement()
    {
        Vector3 inputDirection = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));

        // 대각선 이동 속도 보정
        if (inputDirection.sqrMagnitude > 1f)
            inputDirection.Normalize();

        if (inputDirection.sqrMagnitude < 0.01f)
        {
            Brake();
            return;
        }

        rb.AddForce(inputDirection.normalized * acceleration, ForceMode.Acceleration);
    }

    private void Brake()
    {
        Vector3 horizontalVelocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        if (horizontalVelocity.sqrMagnitude < 0.01f)
            return;

        rb.AddForce(-horizontalVelocity * brakingPower, ForceMode.Acceleration);
    }
}
