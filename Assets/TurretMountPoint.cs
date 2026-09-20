using UnityEngine;

/// <summary>
/// 함선에 터렛을 장착할 수 있는 위치. 평소엔 홀로그램 프리뷰(이 오브젝트의 메시)만 보이다가
/// 인벤토리에서 터렛 아이템을 드래그해 이 위치에 놓으면 실제로 작동하는 터렛 프리팹으로 교체된다.
/// </summary>
public class TurretMountPoint : MonoBehaviour
{
    private Renderer[] _hologramRenderers;
    private Collider _mountCollider;
    private GameObject _mountedTurret;

    public bool IsOccupied => _mountedTurret != null;

    private void Awake()
    {
        _hologramRenderers = GetComponentsInChildren<Renderer>(true);
        _mountCollider = GetComponent<Collider>();
    }

    /// <summary>item의 터렛 프리팹을 이 위치에 생성하고 홀로그램을 숨긴다. 이미 장착되어 있으면 무시.</summary>
    public bool Equip(TurretItemData item)
    {
        if (IsOccupied || item == null || item.turretPrefab == null)
            return false;

        _mountedTurret = Instantiate(item.turretPrefab, transform.position, transform.rotation, transform.parent);

        foreach (var r in _hologramRenderers)
            if (r != null)
                r.enabled = false;

        if (_mountCollider != null)
            _mountCollider.enabled = false;

        return true;
    }
}
