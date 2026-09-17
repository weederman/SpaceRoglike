using UnityEngine;

/// <summary>
/// Raycast 결과를 받아 맞은 대상에게 데미지를 전달하는 단일 창구.
/// FORGE3D 에셋 스크립트에는 이 함수 호출 한 줄만 넣는다.
/// </summary>
public static class HitResolver
{
    public static void Resolve(in RaycastHit hit, float damage)
    {
        if (hit.collider == null || damage <= 0f)
            return;

        // 콜라이더 자신 → 부모 방향으로 가장 가까운 IDamageable을 찾는다.
        // ShieldHitbox(자식)에 맞으면 → 부모의 ShieldHealth
        // 선체 모델에 맞으면        → Ship 루트의 HullHealth
        IDamageable target = hit.collider.GetComponentInParent<IDamageable>();

        if (target != null)
            target.TakeDamage(damage, hit.point);
    }
}
