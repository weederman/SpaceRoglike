using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Camera Angle")]
    [SerializeField] private float height = 120f;
    [SerializeField] private float distance = 80f;

    [Header("Camera Follow")]
    [SerializeField] private float followSpeed = 8f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 20f;
    [SerializeField] private float minZoom = 45f;
    [SerializeField] private float maxZoom = 120f;
    [SerializeField] private float zoomSmoothSpeed = 8f;

    [Header("Zoom Height")]
    [SerializeField] private float minHeight = 67.5f;
    [SerializeField] private float maxHeight = 180f;

    private float targetZoom;
    private float targetHeight;

    private void Start()
    {
        if (target == null)
        {
            Debug.LogWarning(
                "CameraController: Target이 지정되지 않았습니다."
            );

            return;
        }

        // 시작값
        targetZoom = distance;
        targetHeight = height;

        Vector3 offset = new Vector3(
            0f,
            height,
            -distance
        );

        transform.position =
            target.position + offset;

        LookAtTarget();
    }

    private void Update()
    {
        HandleZoom();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        FollowTarget();
    }

    private void HandleZoom()
    {
        float scroll =
            Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) > 0.01f)
        {
            // 휠 위 = 줌인
            // 휠 아래 = 줌아웃
            targetZoom -=
                scroll * zoomSpeed;

            targetZoom =
                Mathf.Clamp(
                    targetZoom,
                    minZoom,
                    maxZoom
                );

            // 45 ~ 120 거리 사이의 비율
            float zoomRatio =
                Mathf.InverseLerp(
                    minZoom,
                    maxZoom,
                    targetZoom
                );

            // 높이도 같이 변경
            targetHeight =
                Mathf.Lerp(
                    minHeight,
                    maxHeight,
                    zoomRatio
                );
        }

        // 거리 부드럽게 변경
        distance =
            Mathf.Lerp(
                distance,
                targetZoom,
                zoomSmoothSpeed * Time.deltaTime
            );

        // 높이 부드럽게 변경
        height =
            Mathf.Lerp(
                height,
                targetHeight,
                zoomSmoothSpeed * Time.deltaTime
            );
    }

    private void FollowTarget()
    {
        Vector3 offset = new Vector3(
            0f,
            height,
            -distance
        );

        Vector3 targetPosition =
            target.position + offset;

        transform.position =
            Vector3.Lerp(
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