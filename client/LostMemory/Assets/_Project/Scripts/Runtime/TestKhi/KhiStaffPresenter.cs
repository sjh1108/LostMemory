using UnityEngine;
using UnityEngine.InputSystem;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// 스태프 sprite 회전. KhiBowPresenter 와 같은 패턴 — transform.position 기준 마우스 방향으로 회전.
    /// 스태프 시각이 들어있는 GameObject (StaffVisual) 에 부착. recoil 없이 단순.
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Staff Presenter")]
    public class KhiStaffPresenter : MonoBehaviour
    {
        [Header("Refs (Awake 시 자동 resolve)")]
        [SerializeField] private SpriteRenderer staffSprite;
        [Tooltip("마우스 worldPos 변환용. 비어있으면 Camera.main.")]
        [SerializeField] private Camera aimCamera;

        [Header("Orbit (회전 축은 본 컴포넌트 transform.position)")]
        [SerializeField, Min(0f)] private float orbitRadius = 0.4f;
        [Tooltip("스태프 sprite 의 local 위치 보정.")]
        [SerializeField] private Vector2 orbitCenter = new Vector2(0f, 0f);

        [Header("Sprite Orientation")]
        [Tooltip("스태프 sprite 가 기본 +X 방향이면 0. 위쪽(+Y) 향하면 -90.")]
        [SerializeField] private float spriteAngleOffsetDeg = 0f;
        [SerializeField] private bool flipYWhenAimLeft = true;

        private void Awake()
        {
            if (staffSprite == null) staffSprite = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (staffSprite == null) return;

            Vector2 aim = ComputeAimFromOrigin(transform.position);
            float aimAngleDeg = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;

            float rad = aimAngleDeg * Mathf.Deg2Rad;
            Vector3 localPos = new Vector3(
                orbitCenter.x + Mathf.Cos(rad) * orbitRadius,
                orbitCenter.y + Mathf.Sin(rad) * orbitRadius,
                0f);
            staffSprite.transform.localPosition = localPos;
            staffSprite.transform.localRotation = Quaternion.Euler(0f, 0f, aimAngleDeg + spriteAngleOffsetDeg);

            // aim 왼쪽 반면일 때 GameObject 통째 Y 미러 (자식 Tip 도 따라감).
            // SpriteRenderer.flipY 는 자식에 영향 안 주므로 Transform.localScale.y 부호 반전 사용.
            if (flipYWhenAimLeft)
            {
                float flipMult = (aim.x < 0f) ? -1f : 1f;
                Vector3 ls = staffSprite.transform.localScale;
                ls.y = Mathf.Abs(ls.y) * flipMult;
                staffSprite.transform.localScale = ls;
            }
        }

        private Vector2 ComputeAimFromOrigin(Vector3 origin)
        {
            Camera cam = aimCamera != null ? aimCamera : Camera.main;
            Mouse mouse = Mouse.current;
            if (cam == null || mouse == null) return Vector2.right;
            Vector2 screenPos = mouse.position.ReadValue();
            Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
            Vector2 dir = (Vector2)(worldPos - origin);
            return dir.sqrMagnitude > Mathf.Epsilon ? dir.normalized : Vector2.right;
        }
    }
}
