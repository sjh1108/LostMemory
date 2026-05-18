using UnityEngine;

namespace LostMemory.Combat.Telegraph
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Telegraph/Telegraphed Attack Area Effect Player")]
    public sealed class TelegraphedAttackAreaEffectPlayer : MonoBehaviour
    {
        [SerializeField] private TelegraphedAreaAttackController attackController;
        [SerializeField] private AreaAttackEffectPlayer effectPlayer;
        [SerializeField] private bool playOnPrimaryImpact = true;

        private void Reset()
        {
            RefreshReferences();
        }

        private void Awake()
        {
            RefreshReferences();
        }

        private void OnEnable()
        {
            RefreshReferences();
            if (attackController != null)
            {
                attackController.PrimaryImpactExecuted += HandlePrimaryImpactExecuted;
            }
        }

        private void OnDisable()
        {
            if (attackController != null)
            {
                attackController.PrimaryImpactExecuted -= HandlePrimaryImpactExecuted;
            }
        }

        public void RefreshReferences()
        {
            attackController ??= GetComponent<TelegraphedAreaAttackController>();
            effectPlayer ??= GetComponent<AreaAttackEffectPlayer>();
        }

        private void HandlePrimaryImpactExecuted(
            TelegraphedAreaAttackController source,
            Vector2 center,
            Vector2 size)
        {
            if (!playOnPrimaryImpact || effectPlayer == null)
            {
                return;
            }

            effectPlayer.PlayOnce(center, size);
        }
    }
}
