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
    private Vector2 _dragStartAnchoredPosition;
    private LayerMask _mountLayerMask;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _rectTransform = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        _mountLayerMask = LayerMask.GetMask("TurretMount");
        Refresh();
    }

    private void Refresh()
    {
        _image.sprite = _item != null ? _item.icon : null;
        _image.enabled = _item != null;
    }

    /// <summary>이 슬롯에 놓인 아이템을 바꾼다. null이면 빈 슬롯이 된다.</summary>
    public void SetItem(TurretItemData item)
    {
        _item = item;
        Refresh();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_item == null)
            return;

        _dragStartAnchoredPosition = _rectTransform.anchoredPosition;
        _image.raycastTarget = false; // 자기 자신이 드롭 대상 판정에 걸리지 않도록
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
            _rectTransform.anchoredPosition = _dragStartAnchoredPosition;
        }
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
