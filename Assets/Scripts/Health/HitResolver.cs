using System;
using FORGE3D;
using UnityEngine;

/// <summary>
/// Raycast 결과를 받아 맞은 대상에게 데미지를 전달하는 단일 창구.
/// FORGE3D 에셋 스크립트에는 이 함수 호출 한 줄만 넣는다.
/// </summary>
public static class HitResolver
{
    private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

    /// <summary>
    /// 발사체/빔용 Raycast. 쏜 함선(ignoreRoot) 자신의 콜라이더(쉴드·선체·터렛)와 트리거(터렛 장착 위치 등)는 무시하고
    /// 가장 가까운 것을 돌려준다. 소켓이 자기 쉴드 경계에 붙어 있어 옆으로 쏘면 자기 함선에 맞던 문제를 막는다.
    /// </summary>
    public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, int layerMask,
        Transform ignoreRoot, out RaycastHit hit)
    {
        int count = Physics.RaycastNonAlloc(origin, direction, HitBuffer, maxDistance, layerMask,
            QueryTriggerInteraction.Ignore);

        hit = default;
        float closest = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            RaycastHit candidate = HitBuffer[i];
            if (ignoreRoot != null && candidate.collider.transform.IsChildOf(ignoreRoot))
                continue;

            if (candidate.distance < closest)
            {
                closest = candidate.distance;
                hit = candidate;
            }
        }

        return closest < float.PositiveInfinity;
    }

    /// <summary>
    /// 무기가 피해 대상(쉴드/선체)을 맞혔을 때 (맞은 대상, 무기 종류, 쏜 쪽).
    /// 튜토리얼처럼 "레이저로 맞혔는지"를 알아야 하는 쪽에서 구독한다. 지속형 빔은 프레임마다 온다.
    /// </summary>
    public static event Action<IDamageable, F3DFXType, WeaponOwner> OnHit;

    /// <param name="shieldMultiplier">대상이 쉴드일 때 데미지에 곱할 배율</param>
    /// <param name="armorMultiplier">대상이 선체(아머)일 때 데미지에 곱할 배율</param>
    public static void Resolve(in RaycastHit hit, float damage, float shieldMultiplier, float armorMultiplier,
        F3DFXType weapon, WeaponOwner owner)
    {
        // 플레이어의 무기는 아군 함선(호위하는 화물선 등)에 피해를 주지 않는다
        if (owner == WeaponOwner.Player && FriendlyShip.Is(hit.collider))
            return;

        // 적의 무기는 플레이어 편(플레이어 함선, 아군 함선)에만 피해를 준다 (해적끼리 맞아 격파되지 않게)
        if (owner == WeaponOwner.Enemy && hit.collider != null
            && hit.collider.GetComponentInParent<SpaceshipController>() == null && !FriendlyShip.Is(hit.collider))
            return;

        // 아군 NPC의 무기는 적에게만 피해를 준다 (플레이어 함선·아군 함선은 맞지 않는다)
        if (owner == WeaponOwner.Ally && hit.collider != null
            && (hit.collider.GetComponentInParent<SpaceshipController>() != null || FriendlyShip.Is(hit.collider)))
            return;

        IDamageable target = Resolve(hit, damage, shieldMultiplier, armorMultiplier);
        if (target != null)
            OnHit?.Invoke(target, weapon, owner);
    }

    /// <returns>데미지를 받은 대상 (없으면 null)</returns>
    public static IDamageable Resolve(in RaycastHit hit, float damage, float shieldMultiplier = 1f, float armorMultiplier = 1f)
    {
        if (hit.collider == null || damage <= 0f)
            return null;

        // 콜라이더 자신 → 부모 방향으로 가장 가까운 IDamageable을 찾는다.
        // ShieldHitbox(자식)에 맞으면 → 부모의 ShieldHealth
        // 선체 모델에 맞으면        → Ship 루트의 HullHealth
        IDamageable target = hit.collider.GetComponentInParent<IDamageable>();

        if (target == null)
            return null;

        float multiplier = 1f;
        if (target is ShieldHealth)
            multiplier = shieldMultiplier;
        else if (target is HullHealth)
            multiplier = armorMultiplier;

        target.TakeDamage(damage * multiplier, hit.point);
        return target;
    }
}
