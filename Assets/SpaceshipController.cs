
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SpaceshipController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float maxSpeed = 12f;

    [Header("Turning")]
    [SerializeField] private float rotationSpeed = 5f;

    [Header("Braking")]
    [SerializeField] private float brakingPower = 4f;
    [SerializeField] private float stoppingDistance = 1.5f;

    [Header("Click Effect")]
    [SerializeField] private GameObject clickEffect;

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
        HandleMouseInput();
    }

    private void FixedUpdate()
    {
        MoveToTarget();
        RotateToMovement();
        LimitSpeed();
    }

    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

            if (groundPlane.Raycast(ray, out float distance))
            {
                targetPosition = ray.GetPoint(distance);

                targetPosition.y = transform.position.y;

                hasTarget = true;

                // 클릭 이펙트 생성
                SpawnClickEffect(targetPosition);
            }
        }
    }

    private void SpawnClickEffect(Vector3 position)
    {
        if (clickEffect == null)
            return;

        GameObject effect = Instantiate(
            clickEffect,
            position,
            Quaternion.identity
        );

        // 파티클이 끝나면 자동 삭제
        ParticleSystem particle =
            effect.GetComponent<ParticleSystem>();

        if (particle != null)
        {
            Destroy(
                effect,
                particle.main.duration +
                particle.main.startLifetime.constantMax
            );
        }
        else
        {
            Destroy(effect, 2f);
        }
    }

    private void MoveToTarget()
    {
        if (!hasTarget)
            return;

        Vector3 direction = targetPosition - transform.position;
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
        Vector3 horizontalVelocity = new Vector3(
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
            Quaternion.LookRotation(velocity.normalized);

        Quaternion newRotation = Quaternion.Slerp(
            rb.rotation,
            targetRotation,
            rotationSpeed * Time.fixedDeltaTime
        );

        rb.MoveRotation(newRotation);
    }

    private void LimitSpeed()
    {
        Vector3 velocity = rb.velocity;

        Vector3 horizontalVelocity = new Vector3(
            velocity.x,
            0f,
            velocity.z
        );

        if (horizontalVelocity.magnitude > maxSpeed)
        {
            horizontalVelocity =
                horizontalVelocity.normalized * maxSpeed;

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
}
