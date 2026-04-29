using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Combat
{
    /// <summary>
    /// CL-107: 매 LateUpdate 마다 PlayerStatModifierContainer 의 MoveSpeed multiplier 를
    /// TDE CharacterMovement.MovementSpeedMultiplier 에 반영.
    /// CharacterMovement 원본 수정 없이 어댑터로 동작 (CL-107 결정 #2).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Player Movement Stat Applier")]
    public sealed class PlayerMovementStatApplier : MonoBehaviour
    {
        [SerializeField] private CharacterMovement characterMovement;
        [SerializeField] private PlayerStatModifierContainer container;

        private void LateUpdate()
        {
            if (characterMovement == null || container == null) return;
            characterMovement.MovementSpeedMultiplier = container.GetTotalMultiplier(StatId.MoveSpeed);
        }
    }
}
