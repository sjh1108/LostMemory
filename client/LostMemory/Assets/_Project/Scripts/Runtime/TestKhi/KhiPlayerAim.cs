using UnityEngine;
using UnityEngine.InputSystem;

namespace LostMemory.TestKhi
{
    public class KhiPlayerAim : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Vector2 fallbackDirection = Vector2.right;
        [SerializeField] private KhiDownController downController;

        private Vector2 _lastDirection = Vector2.right;

        public Vector2 CurrentDirection => _lastDirection;

        private void Awake()
        {
            if (downController == null)
            {
                KhiPlayerActionGate.TryResolveDownController(this, out downController);
            }
        }

        public Vector2 GetAimDirection()
        {
            if (KhiPlayerActionGate.IsBlocked(downController))
            {
                return GetCurrentOrFallbackDirection();
            }

            Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
            Mouse mouse = Mouse.current;
            if (cameraToUse == null || mouse == null)
            {
                return GetCurrentOrFallbackDirection();
            }

            Vector2 screenPosition = mouse.position.ReadValue();
            Vector3 worldPosition = cameraToUse.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -cameraToUse.transform.position.z));
            Vector2 direction = (Vector2)(worldPosition - transform.position);
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return GetCurrentOrFallbackDirection();
            }

            _lastDirection = direction.normalized;
            return _lastDirection;
        }

        private Vector2 GetCurrentOrFallbackDirection()
        {
            return _lastDirection.sqrMagnitude > Mathf.Epsilon ? _lastDirection : fallbackDirection.normalized;
        }
    }
}
