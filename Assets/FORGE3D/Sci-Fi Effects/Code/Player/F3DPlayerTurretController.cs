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
            // [추가] 조작이 잠긴 동안(스토리 재생 등)이나 정거장에 도킹해 있는 동안에는 터렛이 마우스를 따라 돌지도 않는다
            if (!GameInputLock.IsLocked && !StationDocking.IsDocked)
                CheckForTurn();
            CheckForFire();
        }

        void CheckForFire()
        {
            // 개발자 모드 창이 열려 있거나 스토리가 재생되는 동안, [추가] 정거장에 도킹해 있는 동안에는 발사하지 않고, 쏘던 중이면 멈춘다
            if (GameInputLock.IsLocked || StationDocking.IsDocked)
            {
                if (isFiring)
                {
                    isFiring = false;
                    fxController.Stop();
                }
                return;
            }

            // [추가] 무기를 바꾼 직후(격납고 장착·loadout cue)에는 쏘던 것을 멈춘다 (아래에서 새 무기로 다시 쏜다)
            if (!PlayerWeaponSelector.CanFire())
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

            // Fire turret
            // [추가] 누르는 순간뿐 아니라 누르고 있는 동안에도 시작한다 (무기를 바꾼 뒤 버튼을 누른 채면 새 무기로 이어 쏜다)
            if (!isFiring && Input.GetKey(KeyCode.Mouse0))
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