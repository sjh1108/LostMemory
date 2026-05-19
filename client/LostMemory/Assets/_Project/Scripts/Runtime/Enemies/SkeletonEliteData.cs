using UnityEngine;

namespace LostMemory.Enemies
{
    [CreateAssetMenu(fileName = "SkeletonEliteData", menuName = "LostMemory/Enemies/Skeleton Elite Data", order = 11)]
    public sealed class SkeletonEliteData : EnemyData
    {
        [SerializeField, Min(0.01f)] private float _shieldHealth = 200f;

        public float ShieldHealth => Mathf.Max(0.01f, _shieldHealth);
    }
}
