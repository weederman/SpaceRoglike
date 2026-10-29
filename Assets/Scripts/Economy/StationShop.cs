using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 정거장 구역에 들어가면 상점 창을 연다.
///  - 함선 수리: 크레딧을 내고 선체를 최대로 회복한다 (깎인 체력 1당 repairCostPerHp 크레딧).
///  - 무기: 고르면 터렛 장착 위치(홀로그램)를 보여준 뒤, 플레이어가 클릭한 위치에 무기를 장착하고 크레딧을 차감한다.
/// 창 모양은 Assets/Resources/UI/StationShop.prefab (SCI-FI UI Pack 기반 StationShopView)이며 실행 중에 만든다.
/// 씬에 있던 옛 상점 창은 쓰지 않고 숨긴다.
/// 조작이 잠긴 스토리(대사)가 재생되는 동안에는 열지 않고, 끝난 뒤 구역 안에 있으면 연다.
/// 정거장이 여러 개여도(항성계) 어느 정거장 구역에 들어가든 같은 상점을 연다.
/// </summary>
public class StationShop : MonoBehaviour
{
    /// <summary>터렛 위치를 고르는 중. 이 동안 마우스 클릭이 발사로 이어지지 않게 한다.</summary>
    public static bool IsPlacingWeapon { get; private set; }

    private const string ViewPath = "UI/StationShop";

    [Tooltip("씬에 있던 옛 상점 창. 실행 중에는 숨기고 새 상점 창(StationShopView)을 쓴다.")]
    [SerializeField] private GameObject _panel;
    [SerializeField] private GameObject _hint;
    [SerializeField] private TurretItemData[] _items;
    [SerializeField, Min(1f)] private float _pickRayDistance = 2000f;
    [Tooltip("선체 체력 1을 고치는 데 드는 크레딧")]
    [SerializeField, Min(0f)] private float _repairCostPerHp = 0.2f;

    private readonly List<ShopItemView> _slots = new List<ShopItemView>();
    private StationShopView _view;
    private HullHealth _hull;
    private TurretItemData _pending;
    private int _mountMask;

    private void Start()
    {
        _mountMask = LayerMask.GetMask("TurretMount");

        // 옛 상점 창은 숨기고, SCI-FI UI Pack 기반 새 창을 만든다
        if (_panel != null) _panel.SetActive(false);
        if (_hint != null) _hint.SetActive(false);

        var prefab = Resources.Load<StationShopView>(ViewPath);
        if (prefab == null)
        {
            Debug.LogError($"[Shop] Assets/Resources/{ViewPath}.prefab을 찾지 못해 상점을 열 수 없습니다.");
            enabled = false;
            return;
        }

        _view = Instantiate(prefab);
        _panel = _view.Root;
        _hint = _view.Hint;

        _view.ItemTemplate.gameObject.SetActive(false);
        foreach (var item in _items)
        {
            if (item == null)
                continue;

            ShopItemView slot = Instantiate(_view.ItemTemplate, _view.Grid);
            slot.gameObject.SetActive(true);
            slot.Bind(item, OnSlotClicked);
            _slots.Add(slot);
        }

        _view.RepairButton.onClick.AddListener(Repair);

        _panel.SetActive(false);
        _hint.SetActive(false);

        StationZone.AnyEntered += HandleZoneEntered;
        StationZone.AnyExited += HandleZoneExited;

        if (CreditWallet.Instance != null)
            CreditWallet.Instance.OnChanged += RefreshAffordability;
    }

    private void OnDestroy()
    {
        IsPlacingWeapon = false;

        StationZone.AnyEntered -= HandleZoneEntered;
        StationZone.AnyExited -= HandleZoneExited;

        if (CreditWallet.Instance != null)
            CreditWallet.Instance.OnChanged -= RefreshAffordability;
    }

    private void Update()
    {
        // 구역 안에서 대사가 시작되면 닫고, 끝나면 다시 연다
        bool storyLocked = StoryPlayer.IsPlaying && !StoryPlayer.IsControlMode;
        if (_panel.activeSelf && storyLocked)
            Close();
        else if (!_panel.activeSelf && !storyLocked && StationZone.Current != null)
            Open();

        if (_panel.activeSelf)
            RefreshRepair();

        if (_pending == null)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPlacement();
            return;
        }

        if (Input.GetMouseButtonDown(0) &&
            !GameInputLock.IsLocked &&
            !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
        {
            PlaceAtScreenPoint(Input.mousePosition);
        }
    }

    private void HandleZoneEntered(StationZone zone)
    {
        Open();
    }

    private void HandleZoneExited(StationZone zone)
    {
        // 겹친 다른 정거장 구역 안에 아직 있으면 닫지 않는다
        if (StationZone.Current == null)
            Close();
    }

    private void Open()
    {
        if (StoryPlayer.IsPlaying && !StoryPlayer.IsControlMode)
            return; // 대사가 끝나면 Update에서 연다

        _panel.SetActive(true);
        StationZone zone = StationZone.Current;
        _view.Title.text = zone != null && !string.IsNullOrEmpty(zone.DisplayName) ? zone.DisplayName : "정거장";
        RefreshAffordability(CreditWallet.Instance != null ? CreditWallet.Instance.Amount : 0);
        RefreshRepair();
    }

    // ── 함선 수리 ─────────────────────────────────────

    private HullHealth PlayerHull
    {
        get
        {
            if (_hull == null)
            {
                var ship = FindObjectOfType<SpaceshipController>();
                _hull = ship != null ? ship.GetComponentInChildren<HullHealth>() : null;
            }

            return _hull;
        }
    }

    /// <summary>선체를 최대로 고치는 데 드는 크레딧 (고칠 게 없으면 0)</summary>
    private int RepairCost(HullHealth hull)
    {
        float missing = Mathf.Max(0f, hull.MaxHp - hull.CurrentHp);
        return missing < 0.5f ? 0 : Mathf.Max(1, Mathf.CeilToInt(missing * _repairCostPerHp));
    }

    private void RefreshRepair()
    {
        HullHealth hull = PlayerHull;
        if (hull == null)
            return;

        float ratio = hull.MaxHp > 0f ? hull.CurrentHp / hull.MaxHp : 0f;
        _view.HullFill.fillAmount = ratio;
        _view.HullText.text = $"선체 {Mathf.CeilToInt(hull.CurrentHp)} / {Mathf.CeilToInt(hull.MaxHp)}";

        int cost = RepairCost(hull);
        int credits = CreditWallet.Instance != null ? CreditWallet.Instance.Amount : 0;
        if (cost == 0)
        {
            _view.RepairButton.interactable = false;
            _view.RepairLabel.text = "수리 완료";
        }
        else
        {
            _view.RepairButton.interactable = credits >= cost;
            _view.RepairLabel.text = credits >= cost ? $"수리 · {cost} 크레딧" : $"크레딧 부족 · {cost}";
        }
    }

    private void Repair()
    {
        HullHealth hull = PlayerHull;
        var wallet = CreditWallet.Instance;
        if (hull == null || wallet == null)
            return;

        int cost = RepairCost(hull);
        if (cost == 0 || !wallet.TrySpend(cost))
            return;

        hull.Restore(1f);
        RefreshRepair();
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
