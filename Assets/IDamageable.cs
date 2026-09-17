using UnityEngine;

/// <summary>
/// 데미지를 받을 수 있는 모든 대상(쉴드, 선체 등)이 구현하는 인터페이스.
/// 총알/빔은 맞은 대상이 "무엇인지" 몰라도 이 인터페이스만 찾으면 된다.
/// </summary>
public interface IDamageable
{
    void TakeDamage(float damage, Vector3 hitPoint);
}
