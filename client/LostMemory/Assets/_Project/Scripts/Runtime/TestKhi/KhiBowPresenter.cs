using UnityEngine;
using UnityEngine.InputSystem;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// 활 sprite 의 회전·위치 표현. KhiWeaponPresenter 의 활 버전.
    /// 검은 swing 코루틴을 돌리지만 활은 조준 방향에 따른 단순 회전 + 발사 시 살짝 뒤로 당기는 recoil.
    /// 회전 기준은 본 컴포넌트(BowVisual) 의 transform.position. 캐릭터 중앙에 두면 자연스러운 회전 축이 됨.
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Bow Presenter")]
    public class KhiBowPresenter : MonoBehaviour
    {
        [Header("Refs (Awake 시 자동 resolve)")]
        [SerializeField] private SpriteRenderer bowSprite;
        [Tooltip("Fallback 용. 우선 transform.position → 마우스 worldPos 를 직접 계산하고, 카메라가 없을 때만 KhiPlayerAim 사용.")]
        [SerializeField] private KhiPlayerAim playerAim;
        [SerializeField] private KhiBowController bowController;
        [Tooltip("마우스 worldPos 변환용. 비어있으면 Camera.main.")]
        [SerializeField] private Camera aimCamera;

        [Header("Orbit (회전 축은 본 컴포넌트 transform.position)")]
        [SerializeField, Min(0f)] private float orbitRadius = 0.4f;
        [Tooltip("활 sprite 의 local 위치 보정. BowVisual 이 캐릭터 중앙에 있고 활을 그대로 그 주위로 돌리면 (0, 0). 위/아래로 살짝 띄우고 싶을 때 사용.")]
        [SerializeField] private Vector2 orbitCenter = new Vector2(0f, 0f);

        [Header("Sprite Orientation")]
        [Tooltip("활 sprite 가 기본 +X 방향이면 0. 위쪽(+Y)을 향하면 -90.")]
        [SerializeField] private float spriteAngleOffsetDeg = 0f;
        [SerializeField] private bool flipYWhenAimLeft = true;

        [Header("Fire Recoil (선택)")]
        [SerializeField, Min(0f)] private float recoilDistance = 0.15f;
        [SerializeField, Min(0.01f)] private float recoilDuration = 0.08f;

        private float _recoilStartedAt = -1f;

        private void Awake()
        {
            if (bowSprite == null) bowSprite = GetComponent<SpriteRenderer>();
            if (playerAim == null) playerAim = GetComponentInParent<KhiPlayerAim>();
            if (bowController == null) bowController = GetComponentInParent<KhiBowController>();
        }

        private void OnEnable()
        {
            if (bowController != null) bowController.ArrowFired += HandleArrowFired;
        }

        private void OnDisable()
        {
            if (bowController != null) bowController.ArrowFired -= HandleArrowFired;
        }

        private void HandleArrowFired(KhiAttackRequest _, bool __)
        {
            _recoilStartedAt = Time.time;
        }

        private void Update()
        {
            if (bowSprite == null) return;

            Vector2 aim = ComputeAimFromOrigin(transform.position);
            float aimAngleDeg = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;

            float recoilOffset = 0f;
            if (_recoilStartedAt >= 0f)
            {
                float t = (Time.time - _recoilStartedAt) / recoilDuration;
                if (t >= 1f)
                {
                    _recoilStartedAt = -1f;
                }
                else
                {
                    recoilOffset = -recoilDistance * Mathf.Sin(t * Mathf.PI);
                }
            }

            float effectiveRadius = orbitRadius + recoilOffset;
            float rad = aimAngleDeg * Mathf.Deg2Rad;
            Vector3 localPos = new Vector3(
                orbitCenter.x + Mathf.Cos(rad) * effectiveRadius,
                orbitCenter.y + Mathf.Sin(rad) * effectiveRadius,
                0f);
            bowSprite.transform.localPosition = localPos;
            bowSprite.transform.localRotation = Quaternion.Euler(0f, 0f, aimAngleDeg + spriteAngleOffsetDeg);

            if (flipYWhenAimLeft)
            {
                bowSprite.flipY = aim.x < 0f;
            }
        }

        /// <summary>
        /// 회전 기준 = origin → 마우스 worldPos. 카메라 없으면 KhiPlayerAim fallback.
        /// KhiBowController.ComputeAimFromOrigin 과 같은 패턴.
        /// </summary>
        private Vector2 ComputeAimFromOrigin(Vector3 origin)
        {
            Camera cam = aimCamera != null ? aimCamera : Camera.main;
            Mouse mouse = Mouse.current;
            if (cam == null || mouse == null)
            {
                return playerAim != null ? playerAim.GetAimDirection() : Vector2.right;
            }
            Vector2 screenPos = mouse.position.ReadValue();
            Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
            Vector2 dir = (Vector2)(worldPos - origin);
            return dir.sqrMagnitude > Mathf.Epsilon ? dir.normalized : Vector2.right;
        }
    }
}
