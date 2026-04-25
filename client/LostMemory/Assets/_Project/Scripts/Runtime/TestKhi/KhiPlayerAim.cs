using UnityEngine;
using UnityEngine.InputSystem;

namespace LostMemory.TestKhi
{
    public class KhiPlayerAim : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Vector2 fallbackDirection = Vector2.right;

        private Vector2 _lastDirection = Vector2.right;

        public Vector2 CurrentDirection => _lastDirection;

        public Vector2 GetAimDirection()
        {
            Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
            Mouse mouse = Mouse.current;
            if (cameraToUse == null || mouse == null)
            {
                return _lastDirection.sqrMagnitude > Mathf.Epsilon ? _lastDirection : fallbackDirection.normalized;
            }

            Vector2 screenPosition = mouse.position.ReadValue();
            Vector3 worldPosition = cameraToUse.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -cameraToUse.transform.position.z));
            Vector2 direction = (Vector2)(worldPosition - transform.position);
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return _lastDirection.sqrMagnitude > Mathf.Epsilon ? _lastDirection : fallbackDirection.normalized;
            }

            _lastDirection = direction.normalized;
            return _lastDirection;
        }
    }
}
