using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// CL-114 sprite flipX 바인더.
    /// <see cref="KhiPlayerAim"/> 의 aim 방향(마우스 → 캐릭터) X 부호로 좌/우 facing 을 결정해
    /// SpriteRenderer.flipX 에 매핑한다.
    ///
    /// Front 와 Back state 의 자연 facing 이 정반대 대각선이라 상태별 반대 룰 적용:
    /// - Front 계열 (Front_Idle / Front_Walk / Hurt / Head): 자연 ↙ → 우측 facing 시 미러 (flipX = facingRight)
    /// - Back  계열 (Back_Idle / Back_Walk):                자연 ↗ → 좌측 facing 시 미러 (flipX = !facingRight)
    ///
    /// LateUpdate 에서 갱신 — Animator 가 해당 프레임의 sprite 를 적용한 직후 flipX 결정.
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Sprite Flip Binder")]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Animator))]
    [DefaultExecutionOrder(320)]
    public class KhiSpriteFlipBinder : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("비우면 Awake 에서 GetComponentInParent<KhiPlayerAim>() 로 자동 탐색")]
        [SerializeField] private KhiPlayerAim aim;
        [SerializeField] private KhiDownController downController;

        [Header("Facing Detection")]
        [Tooltip("|aim.x| 가 이 값 이하이면 facing 갱신을 건너뛰고 직전 값을 유지 (마우스가 캐릭터 X 라인 근처일 때 떨림 방지)")]
        [SerializeField, Range(0f, 0.5f)] private float aimDeadZoneX = 0.05f;

        [Tooltip("초기 facing 방향 (true = 오른쪽)")]
        [SerializeField] private bool initialFacingRight = true;

        [Header("Back State Names (Animator State 와 일치)")]
        [Tooltip("이 state 들에 있을 때 flipX 룰이 반전된다")]
        [SerializeField] private string backIdleStateName = "Back_Idle";
        [SerializeField] private string backWalkStateName = "Back_Walk";

        [Header("Debug")]
        [SerializeField] private bool logEveryFrame = false;

        private SpriteRenderer _spriteRenderer;
        private Animator _animator;
        private int _backIdleHash;
        private int _backWalkHash;
        private bool _isFacingRight;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _animator = GetComponent<Animator>();

            if (aim == null)
            {
                aim = GetComponentInParent<KhiPlayerAim>();
            }

            if (downController == null)
            {
                KhiPlayerActionGate.TryResolveDownController(this, out downController);
            }

            _backIdleHash = Animator.StringToHash(backIdleStateName);
            _backWalkHash = Animator.StringToHash(backWalkStateName);
            _isFacingRight = initialFacingRight;

            if (aim == null)
            {
                Debug.LogError($"[{nameof(KhiSpriteFlipBinder)}] KhiPlayerAim 을 찾지 못함 — Aim 필드 수동 바인딩 필요", this);
            }
        }

        private void LateUpdate()
        {
            if (KhiPlayerActionGate.IsBlocked(downController))
            {
                return;
            }

            UpdateFacing();
            ApplyFlip();
        }

        private void UpdateFacing()
        {
            if (aim == null)
            {
                return;
            }

            Vector2 dir = aim.GetAimDirection();
            if (dir.x > aimDeadZoneX)
            {
                _isFacingRight = true;
            }
            else if (dir.x < -aimDeadZoneX)
            {
                _isFacingRight = false;
            }
            // else: dead zone — 직전 facing 유지
        }

        private void ApplyFlip()
        {
            var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            bool isBackState = stateInfo.shortNameHash == _backIdleHash
                            || stateInfo.shortNameHash == _backWalkHash;

            _spriteRenderer.flipX = isBackState
                ? !_isFacingRight
                :  _isFacingRight;

            if (logEveryFrame)
            {
                Debug.Log($"[{nameof(KhiSpriteFlipBinder)}] facingRight={_isFacingRight} isBack={isBackState} flipX={_spriteRenderer.flipX}", this);
            }
        }
    }
}
