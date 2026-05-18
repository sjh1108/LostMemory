using LostMemory.TestKhi;
using UnityEngine;

namespace LostMemory.MagicalGirl
{
    /// <summary>
    /// CL-204 (Follow Polish): 미소녀가 Player anchor 를 *부드럽게 lag* 하며 따라오는 컴포넌트.
    /// 부모-자식 parenting 대신 world-space 위치를 매 LateUpdate 마다 SmoothDamp 로 보간.
    ///
    /// 동작:
    /// 1. anchor.position + facing-rotated baseOffset = 목표 world 위치
    /// 2. baseOffset.x 는 KhiPlayerAim.GetAimDirection().x 부호 따라 mirror (왼쪽 조준 = X 양수, 오른쪽 = X 음수)
    /// 3. Y 축에 sin 기반 bob (위아래 부유)
    /// 4. transform.position = SmoothDamp(현재, 목표, smoothTime)
    ///
    /// MagicalGirlSpawner.AddGirlByVisual() 가 부착 + Init(anchor, playerAim, offset) 호출.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MagicalGirlFollower : MonoBehaviour
    {
        [Tooltip("Player transform — facing direction 기준점이자 따라가는 anchor.")]
        [SerializeField] private Transform anchor;

        [Tooltip("Player aim 컴포넌트. facing direction 검출용 (.x 부호). null 이면 항상 facing-right.")]
        [SerializeField] private KhiPlayerAim playerAim;

        [Tooltip("facing-right 기준 local offset. facing-left 시 .x 자동 mirror.")]
        [SerializeField] private Vector2 baseOffset;

        [Tooltip("SmoothDamp 의 시간 상수 (초). 작을수록 빠르게 따라옴, 클수록 lag 큼.")]
        [SerializeField, Min(0.01f)] private float smoothTime = 0.3f;

        [Tooltip("위아래 부유 진폭 (유닛). 0 = 부유 없음.")]
        [SerializeField, Min(0f)] private float bobAmplitude = 0.1f;

        [Tooltip("부유 속도 (사이클/초). 1.5 = 1초에 1.5번 호흡.")]
        [SerializeField, Min(0.01f)] private float bobSpeed = 1.5f;

        [Tooltip("부유 phase offset (0~1). 같은 위치에 spawn 된 여러 개를 다른 박자로 흔들기 위해 random 부여.")]
        [SerializeField, Range(0f, 1f)] private float bobPhaseOffset = 0f;

        private Vector3 _vel;

        /// <summary>Spawner 가 외부에서 tuning 할 수 있게 expose.</summary>
        public float SmoothTime { get => smoothTime; set => smoothTime = Mathf.Max(0.01f, value); }
        public float BobAmplitude { get => bobAmplitude; set => bobAmplitude = Mathf.Max(0f, value); }
        public float BobSpeed { get => bobSpeed; set => bobSpeed = Mathf.Max(0.01f, value); }

        /// <summary>Live tuning — Spawner 가 formation offset 재적용 시.</summary>
        public void SetBaseOffset(Vector2 offset) => baseOffset = offset;

        /// <summary>
        /// Spawner 가 spawn 직후 호출. 매번 다른 phase 로 random 화 (자연스러운 박자 분산).
        /// </summary>
        public void Init(Transform a, KhiPlayerAim aim, Vector2 offset)
        {
            anchor = a;
            playerAim = aim;
            baseOffset = offset;
            bobPhaseOffset = Random.value;
            // 첫 프레임에 anchor + mirror-applied offset 으로 즉시 점프 — SmoothDamp 가 0,0 에서 시작해 어색하게 점프하는 것 방지.
            // CL-204 후속: mirror 도 적용해서 LateUpdate 첫 frame 의 visual snap 방지. fusion 처럼 짧게 사는 entity 에 특히 중요.
            if (anchor != null)
            {
                float facingX = 1f;
                if (playerAim != null && playerAim.GetAimDirection().x < 0f)
                    facingX = -1f;
                Vector2 mirroredOffset = new Vector2(offset.x * facingX, offset.y);
                transform.position = anchor.position + (Vector3)mirroredOffset;
            }
            _vel = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (anchor == null) return;

            // facing 검출 — KhiPlayerAim 우선. 없으면 facing-right 기본.
            float facingX = 1f;
            if (playerAim != null)
            {
                Vector2 aim = playerAim.GetAimDirection();
                if (aim.x < 0f) facingX = -1f;
            }

            // facing 에 따라 X 만 mirror, Y 는 그대로
            Vector2 offset = baseOffset;
            offset.x *= facingX;

            // bob (Y sin) — phase offset 으로 미소녀마다 다른 박자
            if (bobAmplitude > 0f)
            {
                float t = Time.time * bobSpeed + bobPhaseOffset * 10f;
                offset.y += Mathf.Sin(t * Mathf.PI * 2f) * bobAmplitude;
            }

            Vector3 target = anchor.position + (Vector3)offset;
            transform.position = Vector3.SmoothDamp(transform.position, target, ref _vel, smoothTime);
        }
    }
}
