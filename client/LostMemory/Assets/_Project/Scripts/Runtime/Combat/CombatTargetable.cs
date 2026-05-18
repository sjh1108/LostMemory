using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Combat
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Combat Targetable")]
    public sealed class CombatTargetable : MonoBehaviour
    {
        [SerializeField] private bool isTargetable = true;

        public bool IsTargetable
        {
            get => isTargetable;
            set => isTargetable = value;
        }

        public static bool CanBeTargeted(Health health)
        {
            if (health == null)
            {
                return false;
            }

            CombatTargetable targetable = health.GetComponentInParent<CombatTargetable>();
            return targetable == null || targetable.IsTargetable;
        }
    }
}
