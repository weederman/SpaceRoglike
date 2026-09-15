using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Camera Angle")]
    [SerializeField] private float height = 15f;
    [SerializeField] private float distance = 12f;

    [Header("Camera Follow")]
    [SerializeField] private float followSpeed = 8f;

    private void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("CameraController: Target이 지정되지 않았습니다.");
            return;
        }

        // 시작할 때 우주선 위쪽 + 뒤쪽에 카메라 배치
        Vector3 offset = new Vector3(
            0f,
            height,
            -distance
        );

        transform.position = target.position + offset;

        // 우주선을 바라봄
        LookAtTarget();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        FollowTarget();
    }

    private void FollowTarget()
    {
        // 우주선과 카메라 사이의 상대 위치
        Vector3 offset = new Vector3(
            0f,
            height,
            -distance
        );

        Vector3 targetPosition =
            target.position + offset;

        // 부드럽게 따라가기
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            followSpeed * Time.deltaTime
        );

        LookAtTarget();
    }

    private void LookAtTarget()
    {
        Vector3 direction =
            target.position - transform.position;

        if (direction.sqrMagnitude < 0.001f)
            return;

        transform.rotation =
            Quaternion.LookRotation(direction);
    }
}