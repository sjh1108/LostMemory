using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.AI
{
    [AddComponentMenu("Lost Memory/Enemies/AI/AI Decision Current Target Is Valid")]
    public sealed class AIDecisionCurrentTargetIsValid : AIDecision
    {
        public override bool Decide()
        {
            if (_brain == null || _brain.Target == null)
            {
                return false;
            }

            Character character = _brain.Target.GetComponentInParent<Character>();
            return character == null
                   || character.ConditionState.CurrentState != CharacterStates.CharacterConditions.Dead;
        }
    }
}
