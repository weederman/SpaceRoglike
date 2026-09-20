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
            // UI(인벤토리 드래그 등) 위를 클릭했을 때는 무기가 같이 발사되지 않도록 함
            if (!isFiring &&
                EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

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

                Debug.Log("Target = " + target);

                turret.SetNewTarget(target);
            }
        }
    }
}