using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Camera Position")]
    [SerializeField] private float height = 12f;
    [SerializeField] private float distance = 8f;

    [Header("Camera Angle")]
    [SerializeField] private float angle = 55f;

    [Header("Camera Follow")]
    [SerializeField] private float followSpeed = 8f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 3f;
    [SerializeField] private float minZoom = 6f;
    [SerializeField] private float maxZoom = 18f;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        if (cam == null)
        {
            cam = Camera.main;
        }
    }

    private void Start()
    {
        if (target == null)
            return;

        // 시작 위치 설정
        Vector3 startPosition =
            target.position +
            new Vector3(0f, height, -distance);

        transform.position = startPosition;

        // 쿼터뷰 방향
        transform.rotation =
            Quaternion.Euler(angle, 0f, 0f);
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        FollowTarget();
        HandleZoom();
    }

    private void FollowTarget()
    {
        Vector3 targetPosition =
            target.position +
            new Vector3(0f, height, -distance);

        // 부드럽게 따라가기
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            followSpeed * Time.deltaTime
        );

        // 항상 우주선을 바라봄
        Vector3 lookDirection =
            target.position - transform.position;

        transform.rotation =
            Quaternion.LookRotation(lookDirection);
    }

    private void HandleZoom()
    {
        float scroll =
            Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) < 0.01f)
            return;

        if (cam.orthographic)
        {
            float newSize =
                cam.orthographicSize -
                scroll * zoomSpeed;

            cam.orthographicSize =
                Mathf.Clamp(
                    newSize,
                    minZoom,
                    maxZoom
                );
        }
        else
        {
            float newFOV =
                cam.fieldOfView -
                scroll * zoomSpeed * 5f;

            cam.fieldOfView =
                Mathf.Clamp(
                    newFOV,
                    30f,
                    80f
                );
        }
    }
}