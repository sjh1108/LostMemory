using System;
using LostMemory.Enemies;
using UnityEngine;

namespace LostMemory.Enemies.Boss.Rena
{
    [CreateAssetMenu(fileName = "Rena_Boss_Balance", menuName = "LostMemory/Enemies/Rena Boss Balance", order = 12)]
    public sealed class RenaBossBalanceData : BossData
    {
        [Header("Spell Damage")]
        [SerializeField, Min(0f)] private float fireballDamage = 10f;
        [SerializeField, Min(0f)] private float infernoDamage = 16f;
        [SerializeField, Min(0f)] private float iceSweepDamage = 18f;
        [SerializeField, Min(0f)] private float thunderStrikeDamage = 13f;
        [SerializeField, Min(0f)] private float thunderboltDamage = 6f;

        public float FireballDamage => fireballDamage;
        public float InfernoDamage => infernoDamage;
        public float IceSweepDamage => iceSweepDamage;
        public float ThunderStrikeDamage => thunderStrikeDamage;
        public float ThunderboltDamage => thunderboltDamage;

        public static event Action<RenaBossBalanceData> ValuesChanged;

        private void OnValidate()
        {
            fireballDamage = Mathf.Max(0f, fireballDamage);
            infernoDamage = Mathf.Max(0f, infernoDamage);
            iceSweepDamage = Mathf.Max(0f, iceSweepDamage);
            thunderStrikeDamage = Mathf.Max(0f, thunderStrikeDamage);
            thunderboltDamage = Mathf.Max(0f, thunderboltDamage);
            ValuesChanged?.Invoke(this);
        }
    }
}
