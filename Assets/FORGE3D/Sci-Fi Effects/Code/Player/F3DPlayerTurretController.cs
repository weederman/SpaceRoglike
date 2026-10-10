using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;

namespace FORGE3D
{
    public class F3DPlayerTurretController : MonoBehaviour
    {
        RaycastHit hitInfo; // Raycast structure
        public F3DTurret turret;
        bool isFiring; // Is turret currently in firing state
        public F3DFXController fxController;

        void Update()
        {
            CheckForTurn();
            CheckForFire();
        }

        void CheckForFire()
        {
            // 개발자 모드 창이 열려 있는 동안에는 발사하지 않고, 쏘던 중이면 멈춘다
            if (DevTuning.InputBlocked)
            {
                if (isFiring)
                {
                    isFiring = false;
                    fxController.Stop();
                }
                return;
            }

            // UI(상점 등) 위를 클릭했을 때는 무기가 같이 발사되지 않도록 함
            if (!isFiring &&
                EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            // 상점에서 터렛 배치 위치를 고르는 중에는 클릭이 발사로 이어지지 않도록 함
            if (!isFiring && StationShop.IsPlacingWeapon)
                return;

            // Fire turret
            if (!isFiring && Input.GetKeyDown(KeyCode.Mouse0))
            {
                isFiring = true;
                fxController.Fire();
            }

            // Stop firing
            if (isFiring && Input.GetKeyUp(KeyCode.Mouse0))
            {
                isFiring = false;
                fxController.Stop();
            }
        }

        void CheckForTurn()
        {
            Camera mainCamera = Camera.main;

            if (mainCamera == null)
                return;

            Ray ray =
                mainCamera.ScreenPointToRay(Input.mousePosition);

            Plane aimPlane =
                new Plane(
                    Vector3.up,
                    new Vector3(
                        0f,
                        turret.transform.position.y,
                        0f
                    )
                );

            if (aimPlane.Raycast(ray, out float distance))
            {
                Vector3 target =
                    ray.GetPoint(distance);

                target.y =
                    turret.transform.position.y;

                turret.SetNewTarget(target);
            }
        }
    }
}