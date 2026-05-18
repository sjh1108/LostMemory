using UnityEngine;
using UnityEngine.InputSystem;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// 화염방사기 시각 회전 컨트롤러. 두 가지 패턴 지원:
    /// 1) 자식 SpriteRenderer 모드 (Bow/Staff 와 동일): weaponSprite 슬롯에 자식 SpriteRenderer 드래그.
    ///    → weaponSprite 의 transform 을 orbit + 회전.
    /// 2) Self 모드 (FlamethrowerVisual 처럼 ParticleSystem 만 있는 GameObject): weaponSprite 비워둠.
    ///    → 본 컴포넌트가 부착된 GameObject 의 transform 을 직접 회전.
    ///      자식 ParticleSystem 들이 함께 회전 → 분사 방향이 마우스 따라감.
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Flamethrower Presenter")]
    public class KhiFlamethrowerPresenter : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("선택 — 자식 SpriteRenderer 가 있으면 그것을 회전시킴. 비어있으면 본 GameObject 의 transform 을 직접 회전 (FlamethrowerVisual 패턴).")]
        [SerializeField] private SpriteRenderer weaponSprite;
        [Tooltip("마우스 worldPos 변환용. 비어있으면 Camera.main.")]
        [SerializeField] private Camera aimCamera;

        [Header("Orbit (자식 SpriteRenderer 모드 전용)")]
        [SerializeField, Min(0f)] private float orbitRadius = 0.4f;
        [SerializeField] private Vector2 orbitCenter = new Vector2(0f, 0f);

        [Header("Orientation")]
        [Tooltip("ParticleSystem 또는 스프라이트의 기본 발사 방향 보정.\n" +
                 "- ParticleSystem 자식이 Transform Rotation Y=-90 으로 +X 분사하도록 세팅된 경우 → 0\n" +
                 "- 스프라이트가 위(+Y)를 향하면 → -90")]
        [SerializeField] private float spriteAngleOffsetDeg = 0f;
        [Tooltip("Aim 이 왼쪽일 때 Y 미러링 (스프라이트 위아래 뒤집힘 방지). ParticleSystem 모드에선 보통 OFF.")]
        [SerializeField] private bool flipYWhenAimLeft = false;

        private void Awake()
        {
            if (weaponSprite == null) weaponSprite = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            Vector2 aim = ComputeAimFromOrigin(transform.position);
            float aimAngleDeg = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;

            if (weaponSprite != null)
            {
                // 자식 SpriteRenderer 패턴 (Bow/Staff 와 동일)
                ApplyChildSpriteTransform(weaponSprite.transform, aim, aimAngleDeg);
            }
            else
            {
                // Self 모드: 본 GameObject 회전 — 자식 ParticleSystem 들이 함께 회전
                transform.localRotation = Quaternion.Euler(0f, 0f, aimAngleDeg + spriteAngleOffsetDeg);
                if (flipYWhenAimLeft)
                {
                    float flipMult = (aim.x < 0f) ? -1f : 1f;
                    Vector3 ls = transform.localScale;
                    ls.y = Mathf.Abs(ls.y) * flipMult;
                    transform.localScale = ls;
                }
            }
        }

        private void ApplyChildSpriteTransform(Transform target, Vector2 aim, float aimAngleDeg)
        {
            float rad = aimAngleDeg * Mathf.Deg2Rad;
            Vector3 localPos = new Vector3(
                orbitCenter.x + Mathf.Cos(rad) * orbitRadius,
                orbitCenter.y + Mathf.Sin(rad) * orbitRadius,
                0f);
            target.localPosition = localPos;
            target.localRotation = Quaternion.Euler(0f, 0f, aimAngleDeg + spriteAngleOffsetDeg);

            if (flipYWhenAimLeft)
            {
                float flipMult = (aim.x < 0f) ? -1f : 1f;
                Vector3 ls = target.localScale;
                ls.y = Mathf.Abs(ls.y) * flipMult;
                target.localScale = ls;
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
