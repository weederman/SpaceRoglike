using UnityEngine;

namespace FORGE3D
{
    public class F3DPlayerController1 : MonoBehaviour
    {
        [Header("Turrets")]
        public F3DTurret[] Turret;

        [Header("Target Debug")]
        public bool DebugDrawTarget = true;

        private Vector3 targetPos;

        private void Update()
        {
            // 마우스 위치를 이용해 조준 위치 계산
            UpdateTargetPosition();

            // 모든 포탑 업데이트
            for (int i = 0; i < Turret.Length; i++)
            {
                if (Turret[i] == null)
                    continue;

                // 좌클릭 = 발사
                if (Input.GetMouseButtonDown(0))
                {
                    Turret[i].PlayAnimation();
                }

                // 좌클릭을 떼면 발사 중지
                if (Input.GetMouseButtonUp(0))
                {
                    Turret[i].StopAnimation();
                }

                // 포탑이 마우스 방향을 바라봄
                Turret[i].SetNewTarget(targetPos);
            }
        }

        private void UpdateTargetPosition()
        {
            Camera mainCamera = Camera.main;

            if (mainCamera == null)
                return;

            // 마우스 위치에서 Ray 생성
            Ray ray =
                mainCamera.ScreenPointToRay(
                    Input.mousePosition
                );

            // 우주선과 같은 높이의 XZ 평면
            Plane targetPlane =
                new Plane(
                    Vector3.up,
                    new Vector3(
                        0f,
                        transform.position.y,
                        0f
                    )
                );

            // Ray와 XZ 평면의 교차점
            if (targetPlane.Raycast(
                ray,
                out float distance))
            {
                targetPos =
                    ray.GetPoint(distance);

                // Y는 항상 우주선 높이
                targetPos.y =
                    transform.position.y;
            }
        }

        private void OnDrawGizmos()
        {
            if (!DebugDrawTarget)
                return;

            if (targetPos == Vector3.zero)
                return;

            // 조준 위치
            Gizmos.color = Color.red;

            Gizmos.DrawSphere(
                targetPos,
                0.5f
            );

            // 우주선 → 마우스 방향
            Gizmos.DrawLine(
                transform.position,
                targetPos
            );
        }
    }
}