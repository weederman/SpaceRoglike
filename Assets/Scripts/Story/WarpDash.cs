using System;
using System.Collections;
using FORGE3D;
using UnityEngine;

/// <summary>
/// 워프 출발 연출 공용: 함선 앞쪽에 워프 게이트(워프 아웃 이펙트)를 띄우고, 함선이 멈춘 상태에서 점점 빨라지며 게이트로 돌진한다.
/// 감속하지 않고 게이트에 닿는 순간 부르는 쪽이 섬광 → 워프 터널로 넘긴다. 그동안에도 Coast로 같은 속도로 계속 나아가게 해
/// "멈췄다가 넘어가는" 느낌이 없게 한다.
/// (FORGE3D 워프 이펙트는 3초 충전 동안 함선을 붙잡아 두고 감속하며 끝 지점에 멈추게 하므로, 함선 위치는 이펙트를 따르지 않고 여기서 정한다.)
/// </summary>
public static class WarpDash
{
    /// <summary>워프 게이트 이펙트를 start에서 dir 방향 distance 앞에 띄운다 (반복 재생은 끈다)</summary>
    public static GameObject SpawnGate(GameObject effectPrefab, Vector3 start, Vector3 dir, float distance)
    {
        if (effectPrefab == null)
            return null;

        GameObject gate = UnityEngine.Object.Instantiate(effectPrefab, start + dir * distance, Quaternion.LookRotation(dir));
        var jump = gate.GetComponent<F3DWarpJump>();
        if (jump != null)
            jump.DebugLoop = false; // 프리팹은 4초마다 반복 재생하도록 되어 있다 (Start 전에 끈다)

        return gate;
    }

    /// <summary>start에서 dir 방향으로 seconds 동안 distance만큼 돌진한다. 가속만 하고 감속하지 않는다.</summary>
    public static IEnumerator Accelerate(Vector3 start, Vector3 dir, float distance, float seconds, Action<Vector3> move)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = t / seconds;
            move(start + dir * (distance * k * k));
            yield return null;
        }

        move(start + dir * distance);
    }

    /// <summary>Accelerate가 끝났을 때의 속도 (거리/초)</summary>
    public static float FinalSpeed(float distance, float seconds)
    {
        return seconds > 0f ? 2f * distance / seconds : 0f;
    }

    /// <summary>from에서 같은 속도로 계속 나아간다. 부르는 쪽이 StopCoroutine으로 멈춘다.</summary>
    public static IEnumerator Coast(Vector3 from, Vector3 dir, float speed, Action<Vector3> move)
    {
        Vector3 pos = from;
        while (true)
        {
            pos += dir * (speed * Time.deltaTime);
            move(pos);
            yield return null;
        }
    }
}
