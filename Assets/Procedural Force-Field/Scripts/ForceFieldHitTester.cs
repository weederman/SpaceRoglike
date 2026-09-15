using UnityEngine;

namespace ProceduralForceField
{
    [DisallowMultipleComponent]
    public sealed class ForceFieldHitTester : MonoBehaviour
    {
        #region Properties
        [SerializeField] private Camera _camera;
        [SerializeField] private LayerMask _layerMask = ~0;
        [SerializeField, Min(0.01f)] private float _maxDistance = 250f;
        #endregion

        #region Initialization
        private void Awake()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }
        }
        #endregion

        private void Update()
        {
            if (_camera == null)
            {
                return;
            }

            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (!Physics.Raycast(ray, out hit, _maxDistance, _layerMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            ProceduralForceFieldOverlay overlay = hit.collider.GetComponentInParent<ProceduralForceFieldOverlay>();
            if (overlay != null)
            {
                overlay.Trigger(hit.point);
            }
        }
    }
}
