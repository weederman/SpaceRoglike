using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 인벤토리 슬롯 UI. 아이템(터렛)을 드래그해서 3D 씬의 TurretMountPoint 위에 놓으면 장착된다.
/// 드롭에 실패하면(대상이 없거나 이미 장착됨) 원래 위치로 되돌아간다.
/// </summary>
[RequireComponent(typeof(Image))]
public class InventorySlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private TurretItemData _item;
    [SerializeField] private float _dropRayDistance = 1000f;

    private Image _image;
    private RectTransform _rectTransform;
    private Canvas _canvas;
    private Vector2 _homeAnchoredPosition;
    private LayerMask _mountLayerMask;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _rectTransform = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        _mountLayerMask = LayerMask.GetMask("TurretMount");
        _homeAnchoredPosition = _rectTransform.anchoredPosition; // 이 슬롯의 고정 위치(상단바 등)
        Refresh();
    }

    private void Refresh()
    {
        _image.sprite = _item != null ? _item.icon : null;
        _image.enabled = _item != null;

        // 아이템이 채워질 때는 항상 이 슬롯의 원래 자리로 되돌린다.
        // (드래그해서 장착에 성공하면 아이콘이 드롭한 위치에 남아있는 상태로 item만 비워지는데,
        // 다음에 그 슬롯에 새 아이템이 들어오면 엉뚱한 위치에 나타나던 문제를 막는다)
        if (_item != null)
            _rectTransform.anchoredPosition = _homeAnchoredPosition;
    }

    /// <summary>이 슬롯에 놓인 아이템을 바꾼다. null이면 빈 슬롯이 된다.</summary>
    public void SetItem(TurretItemData item)
    {
        _item = item;
        Refresh();
    }

    public bool IsEmpty => _item == null;

    /// <summary>씬에 있는 인벤토리 슬롯 중 빈 슬롯 하나를 찾아 item을 넣는다. 자리가 없으면 false.</summary>
    public static bool TryGrantItem(TurretItemData item)
    {
        if (item == null)
            return false;

        var slots = FindObjectsOfType<InventorySlot>(true);
        foreach (var slot in slots)
        {
            if (slot.IsEmpty)
            {
                slot.SetItem(item);
                return true;
            }
        }

        return false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_item == null)
            return;

        _image.raycastTarget = false; // 자기 자신이 드롭 대상 판정에 걸리지 않도록

        // 드래그하는 동안은 이미 장착된 자리도 포함해서 어디에 놓을 수 있는지 홀로그램으로 보여준다
        foreach (var mount in FindObjectsOfType<TurretMountPoint>())
            mount.ShowHologramPreview();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_item == null)
            return;

        Camera uiCam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform,
            eventData.position,
            uiCam,
            out var localPoint);

        _rectTransform.anchoredPosition = localPoint;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_item == null)
            return;

        _image.raycastTarget = true;

        if (TryEquipAtScreenPoint(eventData))
        {
            SetItem(null);
        }
        else
        {
            _rectTransform.anchoredPosition = _homeAnchoredPosition;
        }

        // 미리보기 종료: 장착된 곳은 다시 숨기고, 빈 곳은 원래대로 계속 보여준다
        foreach (var mount in FindObjectsOfType<TurretMountPoint>())
            mount.RefreshHologramVisibility();
    }

    private bool TryEquipAtScreenPoint(PointerEventData eventData)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return false;

        Ray ray = cam.ScreenPointToRay(eventData.position);

        if (!Physics.Raycast(ray, out RaycastHit hit, _dropRayDistance, _mountLayerMask))
            return false;

        TurretMountPoint mount = hit.collider.GetComponentInParent<TurretMountPoint>();
        if (mount == null)
            return false;

        return mount.Equip(_item);
    }
}
