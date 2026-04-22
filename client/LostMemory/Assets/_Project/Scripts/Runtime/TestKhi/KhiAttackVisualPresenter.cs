using UnityEngine;

namespace LostMemory.TestKhi
{
    public class KhiAttackVisualPresenter : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        private KhiMeleeComboController _comboController;

        private void Awake()
        {
            animator ??= GetComponentInChildren<Animator>();
            _comboController = GetComponent<KhiMeleeComboController>();
        }

        private void OnEnable()
        {
            _comboController ??= GetComponent<KhiMeleeComboController>();
            if (_comboController == null)
            {
                return;
            }

            _comboController.AttackStarted += HandleAttackStarted;
        }

        private void OnDisable()
        {
            if (_comboController == null)
            {
                return;
            }

            _comboController.AttackStarted -= HandleAttackStarted;
        }

        private void HandleAttackStarted(KhiAttackRequest request, KhiMeleeAttackStep step)
        {
            if (animator == null || string.IsNullOrEmpty(step.AnimatorTrigger))
            {
                return;
            }

            animator.ResetTrigger("Attack_1");
            animator.ResetTrigger("Attack_2");
            animator.ResetTrigger("Attack_3");
            animator.SetTrigger(step.AnimatorTrigger);
        }
    }
}
