using UnityEngine;

/// <summary>
/// 플레이어 함선 겉모습(PlayerShipAppearance)에 쓰는 모델과 크기. 위치: Assets/Resources/Ships/PlayerShipLooks.asset
/// </summary>
[CreateAssetMenu(fileName = "PlayerShipLooks", menuName = "SpaceRoglike/Player Ship Looks")]
public class PlayerShipLooks : ScriptableObject
{
    [Tooltip("보상으로 받은 소형 전투선 (프롤로그 이후의 플레이어 함선)")]
    public PlayerShipAppearance.LookSettings fighter = new PlayerShipAppearance.LookSettings();
    [Tooltip("주인공의 원래 함선, UTI 화물선 (프롤로그 동안)")]
    public PlayerShipAppearance.LookSettings freighter = new PlayerShipAppearance.LookSettings { hullRatio = 1.35f };

    public PlayerShipAppearance.LookSettings Get(PlayerShipAppearance.Look look)
    {
        return look == PlayerShipAppearance.Look.Freighter ? freighter : fighter;
    }
}
