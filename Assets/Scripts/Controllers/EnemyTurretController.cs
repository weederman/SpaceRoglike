using UnityEngine;

namespace FORGE3D
{
    public class EnemyTurretController : MonoBehaviour
    {
        [Header("Turret")]
        public F3DTurret turret;

        [Header("FX")]
        public F3DFXController fxController;

        [Header("Target")]
        public Transform target;

        [Header("Auto Fire")]
        public float fireInterval = 2f;
        public float fireRange = 30f;

        private float fireTimer = 0f;
        private bool isFiring = false;

        private void Start()
        {
            // Target이 Inspector에 지정되지 않았다면
            // Player 태그를 가진 오브젝트를 찾음
            if (target == null)
            {
                GameObject player =
                    GameObject.FindGameObjectWithTag("Player");

                if (player != null)
                {
                    target = player.transform;
                }
            }
        }

        private void Update()
        {
            if (turret == null || target == null)
                return;

            AimAtPlayer();
            HandleAutoFire();
        }

        private void AimAtPlayer()
        {
            Vector3 targetPosition =
                target.position;

            // 터렛과 같은 높이로 맞춤
            targetPosition.y =
                turret.transform.position.y;

            // FORGE3D 터렛에 조준점 전달
            turret.SetNewTarget(targetPosition);
        }

        private void HandleAutoFire()
        {
            float distance =
                Vector3.Distance(
                    transform.position,
                    target.position
                );

            // 사거리 밖이면 발사하지 않음
            if (distance > fireRange)
            {
                StopFire();
                return;
            }

            fireTimer += Time.deltaTime;

            // 일정 시간이 지나면 발사
            if (fireTimer >= fireInterval)
            {
                fireTimer = 0f;

                Fire();
            }
        }

        private void Fire()
        {
            if (fxController == null)
                return;

            fxController.Fire();
            isFiring = true;

            // 단발 발사이므로 바로 발사 상태 종료
            Invoke(nameof(StopFire), 0.1f);
        }

        private void StopFire()
        {
            if (!isFiring)
                return;

            if (fxController != null)
            {
                fxController.Stop();
            }

            isFiring = false;
        }
    }
}