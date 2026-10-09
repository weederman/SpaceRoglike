using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 정거장 구역에 들어가면 무기 상점 UI를 열고, 무기를 고르면 터렛 장착 위치(홀로그램)를 보여준 뒤
/// 플레이어가 클릭한 위치에 무기를 장착하고 크레딧을 차감한다.
/// </summary>
public class StationShop : MonoBehaviour
{
    /// <summary>터렛 위치를 고르는 중. 이 동안 마우스 클릭이 발사로 이어지지 않게 한다.</summary>
    public static bool IsPlacingWeapon { get; private set; }

    [SerializeField] private StationZone _zone;
    [SerializeField] private GameObject _panel;
    [SerializeField] private Transform _gridRoot;
    [SerializeField] private ShopSlotView _slotTemplate;
    [SerializeField] private GameObject _hint;
    [SerializeField] private TurretItemData[] _items;
    [SerializeField, Min(1f)] private float _pickRayDistance = 2000f;

    private readonly List<ShopSlotView> _slots = new List<ShopSlotView>();
    private TurretItemData _pending;
    private int _mountMask;

    private void Start()
    {
        _mountMask = LayerMask.GetMask("TurretMount");

        _slotTemplate.gameObject.SetActive(false);
        foreach (var item in _items)
        {
            if (item == null)
                continue;

            var slot = Instantiate(_slotTemplate, _gridRoot);
            slot.gameObject.SetActive(true);
            slot.Bind(item, OnSlotClicked);
            _slots.Add(slot);
        }

        _panel.SetActive(false);
        _hint.SetActive(false);

        _zone.Entered += Open;
        _zone.Exited += Close;

        if (CreditWallet.Instance != null)
            CreditWallet.Instance.OnChanged += RefreshAffordability;
    }

    private void OnDestroy()
    {
        IsPlacingWeapon = false;

        if (_zone != null)
        {
            _zone.Entered -= Open;
            _zone.Exited -= Close;
        }

        if (CreditWallet.Instance != null)
            CreditWallet.Instance.OnChanged -= RefreshAffordability;
    }

    private void Update()
    {
        if (_pending == null)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPlacement();
            return;
        }

        if (Input.GetMouseButtonDown(0) &&
            !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
        {
            PlaceAtScreenPoint(Input.mousePosition);
        }
    }

    private void Open()
    {
        _panel.SetActive(true);
        RefreshAffordability(CreditWallet.Instance != null ? CreditWallet.Instance.Amount : 0);
    }

    private void Close()
    {
        CancelPlacement();
        _panel.SetActive(false);
    }

    private void RefreshAffordability(int credits)
    {
        foreach (var slot in _slots)
            slot.SetAffordable(credits >= slot.Item.price);
    }

    private void OnSlotClicked(TurretItemData item)
    {
        if (CreditWallet.Instance == null || CreditWallet.Instance.Amount < item.price)
            return;

        // 다른 무기를 눌렀다면 선택만 바꾼다
        _pending = item;
        IsPlacingWeapon = true;
        _hint.SetActive(true);

        foreach (var mount in FindObjectsOfType<TurretMountPoint>())
            mount.ShowHologramPreview();
    }

    /// <summary>화면 좌표의 터렛 위치(홀로그램)에 선택한 무기를 장착하고 크레딧을 차감한다.</summary>
    public bool PlaceAtScreenPoint(Vector3 screenPoint)
    {
        if (_pending == null || Camera.main == null)
            return false;

        Ray ray = Camera.main.ScreenPointToRay(screenPoint);
        if (!Physics.Raycast(ray, out RaycastHit hit, _pickRayDistance, _mountMask))
            return false;

        var mount = hit.collider.GetComponentInParent<TurretMountPoint>();
        if (mount == null)
            return false;

        var wallet = CreditWallet.Instance;
        if (wallet == null || wallet.Amount < _pending.price)
            return false;

        if (!mount.Equip(_pending))
            return false;

        wallet.TrySpend(_pending.price);
        EndPlacement();
        return true;
    }

    private void CancelPlacement()
    {
        if (_pending == null)
            return;

        EndPlacement();
    }

    private void EndPlacement()
    {
        _pending = null;
        _hint.SetActive(false);

        foreach (var mount in FindObjectsOfType<TurretMountPoint>())
            mount.RefreshHologramVisibility();

        // 같은 프레임에 발사 입력이 처리되지 않도록 프레임 끝에 플래그를 내린다
        StartCoroutine(ClearPlacingFlagAtEndOfFrame());
    }

    private IEnumerator ClearPlacingFlagAtEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        if (_pending == null)
            IsPlacingWeapon = false;
    }
}
