using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// 스태프 sprite 회전. KhiBowPresenter 와 같은 패턴 — transform.position 기준 마우스 방향으로 회전.
    /// 스태프 시각이 들어있는 GameObject (StaffVisual) 에 부착. recoil 없이 단순.
    ///
    /// Multiplayer (Bug #28):
    ///   기존 구현은 Mouse.current 를 직접 읽어 non-owner 측에서 회전 0° 고정 (마우스가 owner 의 것).
    ///   픽스: KhiPlayerAim.GetAimDirection() 사용 — owner 는 마우스→NetworkVariable write,
    ///   non-owner 는 NetworkVariable read 로 owner 의 aim 받아 같은 회전 적용.
    ///   KhiWeaponPresenter (sword) 와 동일 패턴.
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Staff Presenter")]
    public class KhiStaffPresenter : MonoBehaviour
    {
        [Header("Refs (Awake 시 자동 resolve)")]
        [SerializeField] private SpriteRenderer staffSprite;
        [Tooltip("aim 방향 sync 소스. 비어있으면 부모 계층에서 자동 검색. owner/non-owner 동일 값 보장.")]
        [SerializeField] private KhiPlayerAim playerAim;

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
            if (playerAim == null) playerAim = GetComponentInParent<KhiPlayerAim>();
        }

        private void Update()
        {
            if (staffSprite == null) return;

            Vector2 aim = ComputeAim();
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

        /// <summary>
        /// KhiPlayerAim.GetAimDirection() 경유:
        ///   - owner: mouse 위치 기반 계산 + NetworkVariable write
        ///   - non-owner: NetworkVariable read (owner 가 write 한 값)
        ///   - playerAim null fallback: Vector2.right (회전 0° 고정 — 회귀 안전망)
        /// </summary>
        private Vector2 ComputeAim()
        {
            if (playerAim == null)
            {
                // 부모 계층 재검색 — 컴포넌트가 spawn 이후에 추가된 경우 안전망.
                playerAim = GetComponentInParent<KhiPlayerAim>();
                if (playerAim == null) return Vector2.right;
            }
            Vector2 dir = playerAim.GetAimDirection();
            return dir.sqrMagnitude > Mathf.Epsilon ? dir : Vector2.right;
        }
    }
}
