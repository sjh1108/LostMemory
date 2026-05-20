using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Combat
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Combat Targetable")]
    public sealed class CombatTargetable : MonoBehaviour
    {
        private const string EnemyTag = "Enemy";
        private const string BossTag = "Boss";
        private const string EnemiesLayerName = "Enemies";

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

        public static bool CanBeAutoTargetedEnemy(Health health)
        {
            if (health == null || health.CurrentHealth <= 0f || !CanBeTargeted(health))
            {
                return false;
            }

            Character character = health.GetComponentInParent<Character>();
            if (character != null && character.CharacterType == Character.CharacterTypes.AI)
            {
                return true;
            }

            Transform target = health.transform;
            return HasTag(target, BossTag)
                   || HasTag(target, EnemyTag)
                   || IsOnLayer(target, EnemiesLayerName);
        }

        private static bool HasTag(Transform target, string tagName)
        {
            if (target == null)
            {
                return false;
            }

            Transform root = target.root;
            return target.CompareTag(tagName)
                   || (root != null && root.CompareTag(tagName));
        }

        private static bool IsOnLayer(Transform target, string layerName)
        {
            if (target == null)
            {
                return false;
            }

            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
            {
                return false;
            }

            Transform root = target.root;
            return target.gameObject.layer == layer
                   || (root != null && root.gameObject.layer == layer);
        }
    }
}
