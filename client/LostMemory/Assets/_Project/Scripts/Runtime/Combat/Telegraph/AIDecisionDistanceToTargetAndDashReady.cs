using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Combat.Telegraph
{
    [AddComponentMenu("Lost Memory/Combat/Telegraph/AI Decision Distance To Target And Dash Ready")]
    public class AIDecisionDistanceToTargetAndDashReady : AIDecision
    {
        public enum ComparisonModes
        {
            StrictlyLowerThan,
            LowerThan,
            Equals,
            GreaterThan,
            StrictlyGreaterThan
        }

        [SerializeField] private CharacterDash2D dashAbility;
        [SerializeField] private ComparisonModes comparisonMode = ComparisonModes.LowerThan;
        [SerializeField] private float distance = 4.5f;

        private void Reset()
        {
            dashAbility = GetComponent<CharacterDash2D>();
        }

        private void OnValidate()
        {
            dashAbility ??= GetComponent<CharacterDash2D>();
        }

        public override bool Decide()
        {
            if (_brain.Target == null || dashAbility == null || !dashAbility.Cooldown.Ready())
            {
                return false;
            }

            float targetDistance = Vector3.Distance(transform.position, _brain.Target.position);

            return comparisonMode switch
            {
                ComparisonModes.StrictlyLowerThan => targetDistance < distance,
                ComparisonModes.LowerThan => targetDistance <= distance,
                ComparisonModes.Equals => Mathf.Approximately(targetDistance, distance),
                ComparisonModes.GreaterThan => targetDistance >= distance,
                ComparisonModes.StrictlyGreaterThan => targetDistance > distance,
                _ => false
            };
        }
    }
}
