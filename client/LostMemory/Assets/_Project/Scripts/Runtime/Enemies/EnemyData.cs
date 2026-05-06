using System;
using UnityEngine;

namespace LostMemory.Enemies
{
    /// <summary>
    /// 본 namespace 외부에서 EnemyData/BossData 라이프사이클을 hook 하기 위한 정적 이벤트.
    /// CL-180 어댑터(추후) 가 OnAssetSaved 구독해 런타임 반영. Runtime → Editor 어셈블리 직접 참조 회피용.
    /// </summary>
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
        [SerializeField, Min(0)] private int _expReward;
        [SerializeField, Range(0f, 1f)] private float _dropWeight = 1f;

        public string DisplayName => _displayName;
        public float MaxHealth => _maxHealth;
        public float MoveSpeed => _moveSpeed;
        public int ExpReward => _expReward;
        public float DropWeight => _dropWeight;

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
