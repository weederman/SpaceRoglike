using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 함선을 뒤쪽 위에서 따라가는 카메라. 휠로 줌한다.
/// 둘러보기(OrbitEnabled, 정거장 안에서 StationDocking이 켠다)가 켜져 있으면 UI가 아닌 곳을 마우스로 끌어
/// 함선 둘레를 돌며(좌우) 내려다보는 높이(상하)를 바꿀 수 있다. 끄면 원래 자리로 돌아온다.
/// </summary>
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

    [Header("둘러보기 (정거장 안)")]
    [Tooltip("마우스를 1픽셀 끌 때 도는 각도")]
    [SerializeField] private float orbitSensitivity = 0.3f;
    [Tooltip("내려다보는 각도 범위 (도, 수평 기준)")]
    [SerializeField] private Vector2 orbitPitchRange = new Vector2(8f, 85f);

    private float targetZoom;
    private float targetHeight;

    private bool _orbitEnabled;
    private float _orbitYaw;
    private float _orbitPitch; // 기본 각도에 더하는 값
    private bool _dragging;
    private Vector3 _lastMouse;

    /// <summary>마우스로 끌어 함선 둘레를 둘러볼 수 있는지. 끄면 원래 각도로 돌아온다.</summary>
    public bool OrbitEnabled
    {
        get => _orbitEnabled;
        set
        {
            _orbitEnabled = value;
            _dragging = false;
            if (!value)
            {
                _orbitYaw = 0f;
                _orbitPitch = 0f;
            }
        }
    }

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

    /// <summary>함선이 순간이동했을 때 등, 카메라가 미끄러져 오지 않고 곧바로 함선 뒤 제자리로 가게 한다.</summary>
    public void SnapToTarget()
    {
        if (target == null)
            return;

        transform.position = target.position + Offset();
        LookAtTarget();
    }

    private void Update()
    {
        HandleZoom();
        HandleOrbit();
    }

    /// <summary>함선에서 카메라까지 (둘러보기 각도 포함)</summary>
    private Vector3 Offset()
    {
        if (_orbitYaw == 0f && _orbitPitch == 0f)
            return new Vector3(0f, height, -distance);

        float radius = Mathf.Sqrt(height * height + distance * distance);
        float pitch = Mathf.Clamp(Mathf.Atan2(height, distance) * Mathf.Rad2Deg + _orbitPitch, orbitPitchRange.x, orbitPitchRange.y)
                      * Mathf.Deg2Rad;
        return Quaternion.Euler(0f, _orbitYaw, 0f) * new Vector3(0f, radius * Mathf.Sin(pitch), -radius * Mathf.Cos(pitch));
    }

    private void HandleOrbit()
    {
        if (!_orbitEnabled)
            return;

        bool held = Input.GetMouseButton(0) || Input.GetMouseButton(1);
        if (!held)
        {
            _dragging = false;
            return;
        }

        // UI(정거장 창·사이드바) 위에서 누른 것은 끌기로 보지 않는다
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            _dragging = EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
            _lastMouse = Input.mousePosition;
            return;
        }

        if (!_dragging)
            return;

        Vector3 delta = Input.mousePosition - _lastMouse;
        _lastMouse = Input.mousePosition;
        _orbitYaw += delta.x * orbitSensitivity;

        // 기본 각도 + 더한 값이 범위를 넘지 않게 더하는 값 자체를 묶는다
        float basePitch = Mathf.Atan2(height, distance) * Mathf.Rad2Deg;
        _orbitPitch = Mathf.Clamp(_orbitPitch - delta.y * orbitSensitivity, orbitPitchRange.x - basePitch, orbitPitchRange.y - basePitch);
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        FollowTarget();
    }

    private void HandleZoom()
    {
        // 개발자 모드 창의 채팅 로그를 스크롤할 때 줌이 같이 바뀌지 않도록 함
        if (GameInputLock.IsLocked)
            return;

        // 정거장 안에서는 창의 목록 등 UI 위에서 휠을 굴릴 때 줌하지 않는다
        float scroll = _orbitEnabled && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()
            ? 0f
            : Input.GetAxis("Mouse ScrollWheel");

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
        Vector3 targetPosition =
            target.position + Offset();

        // 프레임 시간이 들쭉날쭉해도 같은 속도로 따라가도록 지수 감쇠로 보간한다
        // (Lerp(followSpeed * deltaTime)는 프레임마다 따라가는 비율이 달라져 고속에서 떨림이 보인다)
        transform.position =
            Vector3.Lerp(
                transform.position,
                targetPosition,
                1f - Mathf.Exp(-followSpeed * Time.deltaTime)
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