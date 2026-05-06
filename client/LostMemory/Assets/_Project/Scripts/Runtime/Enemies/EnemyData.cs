using System;
using UnityEngine;

namespace LostMemory.Enemies
{
    public enum EnemyAttackType
    {
        Melee,
        Charge,
        Projectile,
        Slam
    }

    [Serializable]
    public struct EnemyAttackDamage
    {
        [SerializeField] private EnemyAttackType _attackType;
        [SerializeField, Min(0f)] private float _damage;

        public EnemyAttackType AttackType => _attackType;
        public float Damage => _damage;
    }

    public static class EnemyDataEvents
    {
        public static event Action<EnemyData> OnAssetSaved;
        internal static void RaiseAssetSaved(EnemyData asset) => OnAssetSaved?.Invoke(asset);
    }

    [CreateAssetMenu(fileName = "EnemyData", menuName = "LostMemory/Enemies/Enemy Data", order = 10)]
    public class EnemyData : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField, Min(0f)] private float _maxHealth = 100f;
        [SerializeField, Min(0f)] private float _moveSpeed = 5f;
        [SerializeField] private EnemyAttackDamage[] _attackDamages = Array.Empty<EnemyAttackDamage>();
        [SerializeField, Min(0)] private int _expReward;
        [SerializeField, Range(0f, 1f)] private float _dropWeight = 1f;

        public string DisplayName => _displayName;
        public float MaxHealth => _maxHealth;
        public float MoveSpeed => _moveSpeed;
        public int ExpReward => _expReward;
        public float DropWeight => _dropWeight;

        public bool TryGetAttackDamage(EnemyAttackType attackType, out float damage)
        {
            if (_attackDamages != null)
            {
                for (int i = 0; i < _attackDamages.Length; i++)
                {
                    EnemyAttackDamage attackDamage = _attackDamages[i];
                    if (attackDamage.AttackType == attackType)
                    {
                        damage = Mathf.Max(0f, attackDamage.Damage);
                        return true;
                    }
                }
            }

            damage = 0f;
            return false;
        }

#if UNITY_EDITOR
        [ContextMenu("Save Current Values")]
        private void SaveCurrentValues()
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
            EnemyDataEvents.RaiseAssetSaved(this);
            Debug.Log($"[EnemyData] Saved: {name}");
        }
#endif
    }
}
