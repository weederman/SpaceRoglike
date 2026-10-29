using System;
using System.Collections;
using FORGE3D;
using UnityEngine;

/// <summary>
/// 워프 출발 연출 공용 (플레이어의 첫 워프·귀환·지도 워프, 해적선의 워프 이탈).
///  1) 함선 자리에 워프 게이트(워프 아웃 이펙트)가 열린다. 이펙트가 충전되는 동안(holdSeconds) 함선은 들어오던 속도를 줄이며 멈춘다.
///  2) 충전이 끝나면 함선이 점점 더 세게 가속하며 dir 방향으로 돌진한다. 감속하지 않는다.
///  3) 돌진이 끝나도 부르는 쪽이 터널로 넘기는 동안 Coast로 같은 속도로 계속 나아가게 해 "멈췄다가 넘어가는" 느낌이 없게 한다.
/// (FORGE3D 워프 이펙트는 충전 뒤 함선을 감속시키며 끝 지점에 멈추게 하므로, 함선 위치는 이펙트를 따르지 않고 여기서 정한다.)
/// </summary>
public static class WarpDash
{
    /// <summary>워프 이펙트가 충전하는 시간 (F3DWarpJump가 3초 뒤에 쏘아 보낸다)</summary>
    public const float GateChargeSeconds = 3f;

    /// <summary>워프 게이트 이펙트를 position에 dir을 향해 띄운다 (반복 재생은 끈다)</summary>
    public static GameObject SpawnGate(GameObject effectPrefab, Vector3 position, Vector3 dir)
    {
        if (effectPrefab == null)
            return null;

        GameObject gate = UnityEngine.Object.Instantiate(effectPrefab, position, Quaternion.LookRotation(dir));
        var jump = gate.GetComponent<F3DWarpJump>();
        if (jump != null)
            jump.DebugLoop = false; // 프리팹은 4초마다 반복 재생하도록 되어 있다 (Start 전에 끈다)

        return gate;
    }

    /// <summary>
    /// 감속해 멈췄다가(holdSeconds, 들어오던 속도 velocity를 0으로) 점점 더 세게 가속하며 dir 방향으로 distance만큼 돌진한다(dashSeconds).
    /// 끝날 때의 속도는 FinalSpeed. 끝난 위치는 LaunchEnd.
    /// </summary>
    public static IEnumerator Launch(Vector3 start, Vector3 velocity, float holdSeconds, Vector3 dir, float distance,
        float dashSeconds, Action<Vector3> move)
    {
        velocity.y = 0f;

        // 감속: 속도가 일정하게 줄어 holdSeconds 뒤에 멈춘다
        for (float t = 0f; t < holdSeconds; t += Time.deltaTime)
        {
            move(start + velocity * (t - t * t / (2f * holdSeconds)));
            yield return null;
        }

        Vector3 launch = start + velocity * (holdSeconds * 0.5f);

        // 돌진: 가속도가 점점 커진다 (거리 ∝ 시간³)
        for (float t = 0f; t < dashSeconds; t += Time.deltaTime)
        {
            float k = t / dashSeconds;
            move(launch + dir * (distance * k * k * k));
            yield return null;
        }

        move(launch + dir * distance);
    }

    /// <summary>Launch가 끝난 위치</summary>
    public static Vector3 LaunchEnd(Vector3 start, Vector3 velocity, float holdSeconds, Vector3 dir, float distance)
    {
        velocity.y = 0f;
        return start + velocity * (holdSeconds * 0.5f) + dir * distance;
    }

    /// <summary>Launch가 끝났을 때의 속도 (거리/초)</summary>
    public static float FinalSpeed(float distance, float dashSeconds)
    {
        return dashSeconds > 0f ? 3f * distance / dashSeconds : 0f;
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
