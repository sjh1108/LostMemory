using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.Boss.Bertha
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Boss/Bertha/Bertha Dash Start Action")]
    public sealed class BerthaDashStartAction : AIAction
    {
        [SerializeField] private CharacterDash2D dashAbility;
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
            dashAbility?.DashStart();

            if (debugLogging)
            {
                Debug.Log("[BerthaDashStartAction] Dash started.", this);
            }
        }

        public override void PerformAction()
        {
        }

        public void Configure(CharacterDash2D configuredDashAbility)
        {
            dashAbility = configuredDashAbility;
        }
    }
}
