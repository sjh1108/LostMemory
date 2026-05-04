using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// CL-114 애니메이터 Front/Back 분기 + Speed 평활화 바인더.
    /// 두 가지를 매 프레임 Animator 에 set:
    /// 1) <c>MovementY</c> ← <see cref="KhiPlayerAim"/> 의 aim.y (마우스 위/아래 → Back/Front 분기)
    /// 2) <c>Speed</c>     ← Rigidbody2D.linearVelocity.magnitude 의 비대칭 평활값
    ///
    /// Speed 평활화는 좌→우 이동 전환 시 velocity 가 0 을 통과해 Walk↔Idle 깜빡이는 것을 방지:
    /// - 가속(올라갈 때): 즉시 따라감 (Walk 진입 즉시)
    /// - 감속(내려갈 때): <c>SpeedDecayTime</c> 시간상수로 느리게 따라감 (방향 전환 시 dip 무시)
    ///
    /// TDE CharacterMovement(UseDefaultMecanim) 도 Speed 를 set 하지만, 본 바인더가
    /// LateUpdate 에서 마지막에 덮어씀 (DefaultExecutionOrder = 320 + LateUpdate).
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Animator Movement Binder")]
    [RequireComponent(typeof(Animator))]
    [DefaultExecutionOrder(320)]
    public class KhiAnimatorMovementBinder : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("비우면 Awake 에서 GetComponentInParent<KhiPlayerAim>() 로 자동 탐색")]
        [SerializeField] private KhiPlayerAim aim;

        [Tooltip("비우면 Awake 에서 GetComponentInParent<Rigidbody2D>() 로 자동 탐색 (Speed 평활화에 사용)")]
        [SerializeField] private Rigidbody2D body;

        [Tooltip("비우면 Awake 에서 GetComponentInParent<KhiDownController>() 로 자동 탐색. Down/Defeated 시 파라미터 갱신 차단.")]
        [SerializeField] private KhiDownController downController;

        [Header("Animator Parameters")]
        [SerializeField] private string movementYParam = "MovementY";
        [SerializeField] private string speedParam = "Speed";

        [Header("Speed Smoothing (감속 dip 무시)")]
        [Tooltip("내려갈 때(감속) 시간상수 (초). 클수록 Idle 로 떨어지는 게 느려져 좌↔우 전환 dip 을 무시. 0 이면 평활화 비활성")]
        [SerializeField, Range(0f, 1f)] private float speedDecayTime = 0.15f;

        [Header("Debug")]
        [SerializeField] private bool logEveryFrame = false;

        private Animator _animator;
        private int _movementYHash;
        private int _speedHash;
        private float _smoothedSpeed;

        private void Awake()
        {
            _animator = GetComponent<Animator>();

            if (aim == null)
            {
                aim = GetComponentInParent<KhiPlayerAim>();
            }

            if (body == null)
            {
                body = GetComponentInParent<Rigidbody2D>();
            }

            if (downController == null)
            {
                downController = GetComponentInParent<KhiDownController>();
            }

            _movementYHash = Animator.StringToHash(movementYParam);
            _speedHash = Animator.StringToHash(speedParam);

            if (aim == null)
            {
                Debug.LogError($"[{nameof(KhiAnimatorMovementBinder)}] KhiPlayerAim 을 찾지 못함 — Aim 필드 수동 바인딩 필요", this);
            }
            if (body == null)
            {
                Debug.LogWarning($"[{nameof(KhiAnimatorMovementBinder)}] Rigidbody2D 를 찾지 못함 — Speed 평활화는 작동 안 함", this);
            }
        }

        private void LateUpdate()
        {
            // Down/Defeated 시 마우스·이동 입력으로 Animator 가 Idle/Walk 로 흔들리는 것 차단 — Dead 상태 유지
            if (downController != null && (downController.IsDown || downController.IsDefeated))
            {
                return;
            }

            // MovementY ← aim.y
            if (aim != null)
            {
                Vector2 dir = aim.GetAimDirection();
                _animator.SetFloat(_movementYHash, dir.y);
            }

            // Speed ← 비대칭 평활화된 velocity.magnitude
            if (body != null)
            {
                float instantSpeed = body.linearVelocity.magnitude;
                if (speedDecayTime <= Mathf.Epsilon)
                {
                    _smoothedSpeed = instantSpeed;
                }
                else if (instantSpeed >= _smoothedSpeed)
                {
                    _smoothedSpeed = instantSpeed; // 가속 — 즉시
                }
                else
                {
                    // 감속 — speedDecayTime 시간상수로 점진 하강
                    float t = Time.deltaTime / speedDecayTime;
                    _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, instantSpeed, t);
                }
                _animator.SetFloat(_speedHash, _smoothedSpeed);
            }

            if (logEveryFrame)
            {
                Vector2 d = aim != null ? aim.CurrentDirection : Vector2.zero;
                float v = body != null ? body.linearVelocity.magnitude : -1f;
                Debug.Log($"[{nameof(KhiAnimatorMovementBinder)}] aim={d} MovementY={d.y:F2} | rawSpeed={v:F2} smoothedSpeed={_smoothedSpeed:F2}", this);
            }
        }
    }
}
