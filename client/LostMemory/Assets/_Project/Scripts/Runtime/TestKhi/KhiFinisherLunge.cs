using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// CL-067: 지정된 콤보 step의 공격 active 시작 시 캐릭터를 aim 방향으로 짧게 lunge.
    /// KhiDashController.PerformLunge 사용 — Cooldown/Feedback side effect 없음.
    /// HLD 스타일 피니셔 연출.
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Finisher Lunge")]
    public class KhiFinisherLunge : MonoBehaviour
    {
        [Header("Refs (Awake에서 자동 resolve 가능)")]
        [SerializeField] private KhiMeleeComboController comboController;
        [SerializeField] private KhiDashController dashController;
        [SerializeField] private KhiPlayerAim playerAim;

        [Header("Trigger")]
        [SerializeField] private bool enabledToggle = true;
        [Tooltip("lunge를 발동할 콤보 step. 기본 [3] = 3타 finisher만. 여러 개 지정 가능 (예: [2, 3]).")]
        [SerializeField] private int[] targetComboSteps = new[] { 3 };

        [Header("Lunge Parameters")]
        [SerializeField, Range(0.1f, 2f)] private float lungeDistance = 0.8f;
        [SerializeField, Range(0.05f, 0.3f)] private float lungeDuration = 0.12f;

        private void Awake()
        {
            if (comboController == null)
            {
                comboController = GetComponent<KhiMeleeComboController>();
            }
            if (dashController == null)
            {
                dashController = GetComponent<KhiDashController>();
            }
            if (playerAim == null)
            {
                playerAim = GetComponent<KhiPlayerAim>();
            }
        }

        private void OnEnable()
        {
            if (comboController != null)
            {
                comboController.AttackActiveStarted += HandleAttackActiveStarted;
            }
        }

        private void OnDisable()
        {
            if (comboController != null)
            {
                comboController.AttackActiveStarted -= HandleAttackActiveStarted;
            }
        }

        private void HandleAttackActiveStarted(KhiAttackRequest request, KhiMeleeAttackStep step)
        {
            if (!enabledToggle || dashController == null || playerAim == null)
            {
                return;
            }

            if (!IsTargetStep(step.ComboStep))
            {
                return;
            }

            Vector2 direction = playerAim.GetAimDirection();
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                direction = Vector2.right;
            }

            dashController.PerformLunge(direction, lungeDistance, lungeDuration);
        }

        private bool IsTargetStep(int step)
        {
            if (targetComboSteps == null)
            {
                return false;
            }
            for (int i = 0; i < targetComboSteps.Length; i++)
            {
                if (targetComboSteps[i] == step)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
