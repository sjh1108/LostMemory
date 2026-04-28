using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.Boss.Bertha
{
    [AddComponentMenu("Lost Memory/Enemies/Boss/Bertha/Bertha Dash Start Action")]
    public sealed class BerthaDashStartAction : AIAction
    {
        [SerializeField] private CharacterDash2D dashAbility;
        [SerializeField] private bool overrideDashDuration;
        [SerializeField, Min(0.01f)] private float dashDuration = 0.32f;
        [SerializeField] private bool debugLogging;

        public override void Initialization()
        {
            if (!ShouldInitialize)
            {
                return;
            }

            base.Initialization();
            dashAbility ??= GetComponentInParent<CharacterDash2D>();
        }

        public override void OnEnterState()
        {
            base.OnEnterState();
            dashAbility ??= GetComponentInParent<CharacterDash2D>();

            if (dashAbility != null && overrideDashDuration)
            {
                dashAbility.DashDuration = dashDuration;
                dashAbility.Cooldown.ConsumptionDuration = dashDuration;
            }

            dashAbility?.DashStart();

            if (debugLogging)
            {
                Debug.Log("[BerthaDashStartAction] Dash started.", this);
            }
        }

        public override void PerformAction()
        {
        }

        public void Configure(
            CharacterDash2D configuredDashAbility,
            bool shouldOverrideDashDuration = false,
            float configuredDashDuration = 0.32f)
        {
            dashAbility = configuredDashAbility;
            overrideDashDuration = shouldOverrideDashDuration;
            dashDuration = Mathf.Max(0.01f, configuredDashDuration);
        }
    }
}
