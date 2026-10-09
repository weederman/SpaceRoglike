using UnityEngine;

/// <summary>
/// 적 함선 위에 떠 있는 월드 공간 체력바. 채우는 규칙은 ShipHealthBar와 같고,
/// 여기서는 부모 함선을 따라다니며 항상 카메라를 향하게(빌보드) 하는 일만 한다.
/// </summary>
public class EnemyHealthBar : ShipHealthBar
{
    [SerializeField] private Vector3 _worldOffset = new Vector3(0f, 15f, 0f);

    private Transform _followTarget;
    private Camera _cam;

    protected override void Awake()
    {
        base.Awake();

        _followTarget = transform.parent;
        _cam = Camera.main;
    }

    private void LateUpdate()
    {
        if (_followTarget != null)
            transform.position = _followTarget.position + _worldOffset;

        if (_cam == null)
            _cam = Camera.main;

        if (_cam != null)
            transform.rotation = _cam.transform.rotation;
    }
}
