using UnityEngine;

/// <summary>
/// Raycast 결과를 받아 맞은 대상에게 데미지를 전달하는 단일 창구.
/// FORGE3D 에셋 스크립트에는 이 함수 호출 한 줄만 넣는다.
/// </summary>
public static class HitResolver
{
    /// <param name="shieldMultiplier">대상이 쉴드일 때 데미지에 곱할 배율</param>
    /// <param name="armorMultiplier">대상이 선체(아머)일 때 데미지에 곱할 배율</param>
    public static void Resolve(in RaycastHit hit, float damage, float shieldMultiplier = 1f, float armorMultiplier = 1f)
    {
        if (hit.collider == null || damage <= 0f)
            return;

        // 콜라이더 자신 → 부모 방향으로 가장 가까운 IDamageable을 찾는다.
        // ShieldHitbox(자식)에 맞으면 → 부모의 ShieldHealth
        // 선체 모델에 맞으면        → Ship 루트의 HullHealth
        IDamageable target = hit.collider.GetComponentInParent<IDamageable>();

        if (target == null)
            return;

        float multiplier = 1f;
        if (target is ShieldHealth)
            multiplier = shieldMultiplier;
        else if (target is HullHealth)
            multiplier = armorMultiplier;

        target.TakeDamage(damage * multiplier, hit.point);
    }
}
