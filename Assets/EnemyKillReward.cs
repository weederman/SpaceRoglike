using UnityEngine;

/// <summary>
/// 적 선체가 파괴되면(HullHealth.OnBroken) 인벤토리 빈 슬롯에 보상 터렛 아이템을 지급한다.
/// </summary>
[RequireComponent(typeof(HullHealth))]
public class EnemyKillReward : MonoBehaviour
{
    [SerializeField] private TurretItemData _rewardItem;

    private HullHealth _hull;

    private void Awake()
    {
        _hull = GetComponent<HullHealth>();
        _hull.OnBroken += HandleBroken;
    }

    private void OnDestroy()
    {
        if (_hull != null)
            _hull.OnBroken -= HandleBroken;
    }

    private void HandleBroken()
    {
        InventorySlot.TryGrantItem(_rewardItem);
    }
}
