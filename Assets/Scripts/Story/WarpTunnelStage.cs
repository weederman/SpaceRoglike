using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 워프 터널 안을 달리는 장면 무대. FORGE3D Warp Tunnel + Cluster 성운이 흐르는 원통형 터널을 실제 게임 공간과 겹치지 않는
/// 먼 곳에 만들고, 함선을 그 중심에 옮겨 놓은 뒤 "함선보다 약간 아래, 뒤에서 올려다보며 쫓아가는" 카메라로 비춘다.
/// Enter로 들어가고 Exit로 걷어낸다. 걷어낼 때 함선/카메라를 원래 자리로 돌리는 일은 부르는 쪽이 한다.
/// (프롤로그 연출에는 같은 구성이 PrologueDirector 안에 들어 있다. 이 컴포넌트는 그걸 따로 떼어 다른 장면에서 재사용하기 위한 것이다.)
/// 터널 색은 기본 파랑이다.
/// </summary>
public class WarpTunnelStage : MonoBehaviour
{
    [Header("에셋 (프리팹에서 연결)")]
    [SerializeField] private GameObject _tunnelPrefab;
    [SerializeField] private GameObject[] _clusters;
    [SerializeField] private WarpClusterFlow _flowPrefab;
    [Tooltip("터널 빛이 함선을 비추는 조명 (함선 아래쪽에서)")]
    [SerializeField] private Light _light;
    [SerializeField] private Vector3 _lightOffset = new Vector3(0f, -45f, -70f);

    [Header("무대")]
    [Tooltip("터널 무대 위치. 실제 게임 공간과 겹치지 않게 멀리 둔다. (Y는 함선 높이를 따른다)")]
    [SerializeField] private Vector2 _stageXZ = new Vector2(0f, -60000f);
    [SerializeField] private float _swayAmount = 3f;

    [Header("카메라: 함선보다 약간 아래, 뒤에서 쫓아가는 바텀업 뷰")]
    [SerializeField] private Vector3 _cameraOffset = new Vector3(0f, -16f, -105f);
    [SerializeField] private Vector3 _lookOffset = new Vector3(0f, 5f, 30f);
    [SerializeField] private float _cameraFollow = 3f;
    [SerializeField] private float _fovStart = 58f;
    [SerializeField] private float _fovWarp = 72f;

    private Transform _ship;
    private Rigidbody _shipRb;
    private Camera _cam;
    private GameObject _tunnel;
    private WarpClusterFlow _flow;
    private readonly List<Material> _materials = new List<Material>();
    private Vector3 _origin;
    private Vector3 _camPos;
    private float _originalFov;
    private float _enteredAt;

    public bool IsActive { get; private set; }

    /// <summary>터널에 들어온 지 몇 초째인지 (들어와 있지 않으면 0)</summary>
    public float ActiveSeconds => IsActive ? Time.time - _enteredAt : 0f;

    private void Awake()
    {
        if (_light != null)
            _light.enabled = false;
    }

    /// <summary>터널 무대를 만들고 함선을 그 중심으로 옮긴다. 카메라는 이 무대가 직접 움직인다(부르는 쪽은 CameraController를 꺼 둘 것).</summary>
    public void Enter(Transform ship, Rigidbody shipRb, Camera cam)
    {
        if (IsActive)
            return;

        _ship = ship;
        _shipRb = shipRb;
        _cam = cam;
        _originalFov = cam.fieldOfView;
        _origin = new Vector3(_stageXZ.x, ship.position.y, _stageXZ.y);

        _tunnel = Instantiate(_tunnelPrefab, _origin, Quaternion.identity);
        foreach (Renderer r in _tunnel.GetComponentsInChildren<Renderer>())
        {
            // 터널 머티리얼은 복제본이 생기므로, 걷어낼 때 같이 지운다
            if (r.sharedMaterial != null && r.sharedMaterial.shader.name == "FORGE3D/WarpTunnel")
                _materials.Add(r.material);
        }

        _flow = Instantiate(_flowPrefab, _origin, Quaternion.identity);
        _flow.SetPalette(_clusters);
        _flow.SpeedMultiplier = 0.4f;
        _flow.Prewarm();

        if (_light != null)
        {
            _light.enabled = true;
            _light.transform.position = _origin + _lightOffset;
        }

        _ship.SetPositionAndRotation(_origin, Quaternion.identity);
        _shipRb.position = _origin;
        _shipRb.rotation = Quaternion.identity;

        _enteredAt = Time.time;
        _cam.fieldOfView = _fovStart;
        _camPos = _origin + _cameraOffset;
        IsActive = true;
        ApplyCamera(true);
    }

    /// <summary>터널 무대를 걷어낸다. 함선 위치와 카메라는 부르는 쪽이 원래대로 돌려놓는다.</summary>
    public void Exit()
    {
        if (!IsActive)
            return;

        IsActive = false;

        if (_tunnel != null) Destroy(_tunnel);
        if (_flow != null) Destroy(_flow.gameObject);
        if (_light != null) _light.enabled = false;

        foreach (Material m in _materials)
        {
            if (m != null)
                Destroy(m);
        }

        _materials.Clear();
        _cam.fieldOfView = _originalFov;
    }

    private void Update()
    {
        // 처음엔 천천히, 점점 빠르게 흘러가 속도감이 붙는다
        if (IsActive)
            _flow.SpeedMultiplier = Mathf.MoveTowards(_flow.SpeedMultiplier, 1.3f, Time.deltaTime * 0.5f);
    }

    private void LateUpdate()
    {
        if (!IsActive)
            return;

        // 속도감을 위해 좌우로 천천히 흔들리며 약간 기울어진다
        float t = Time.time - _enteredAt;
        _ship.position = _origin + new Vector3(Mathf.Sin(t * 0.9f) * _swayAmount, Mathf.Sin(t * 1.3f) * _swayAmount * 0.5f, 0f);
        _ship.rotation = Quaternion.Euler(Mathf.Sin(t * 1.1f) * 2f, Mathf.Sin(t * 0.7f) * 3f, -Mathf.Sin(t * 0.9f) * 6f);
        _shipRb.position = _ship.position;
        _shipRb.rotation = _ship.rotation;

        ApplyCamera(false);
    }

    private void ApplyCamera(bool snap)
    {
        Vector3 shipPos = _ship.position;
        Vector3 wantPos = shipPos + _cameraOffset;
        _camPos = snap ? wantPos : Vector3.Lerp(_camPos, wantPos, 1f - Mathf.Exp(-_cameraFollow * Time.deltaTime));

        Quaternion rot = Quaternion.LookRotation(shipPos + _lookOffset - _camPos, Vector3.up);
        _cam.transform.SetPositionAndRotation(_camPos, rot);

        // 워프가 깊어질수록 시야를 넓혀 속도감을 준다
        float warp = Mathf.Clamp01((Time.time - _enteredAt) / 5f);
        _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, Mathf.Lerp(_fovStart, _fovWarp, warp), Time.deltaTime * 1.5f);
    }
}
