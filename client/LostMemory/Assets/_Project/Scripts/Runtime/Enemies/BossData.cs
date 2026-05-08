using UnityEngine;

namespace LostMemory.Enemies
{
    [CreateAssetMenu(fileName = "BossData", menuName = "LostMemory/Enemies/Boss Data", order = 11)]
    public class BossData : EnemyData
    {
        [SerializeField, Range(0f, 1f)] private float _phase2ThresholdNormalized = 0.7f;
        [SerializeField, Range(0f, 1f)] private float _phase3ThresholdNormalized = 0.3f;

        public float Phase2ThresholdNormalized => _phase2ThresholdNormalized;
        public float Phase3ThresholdNormalized => _phase3ThresholdNormalized;

        private void OnValidate()
        {
            _phase2ThresholdNormalized = Mathf.Clamp01(_phase2ThresholdNormalized);
            _phase3ThresholdNormalized = Mathf.Clamp01(_phase3ThresholdNormalized);
            _phase3ThresholdNormalized = Mathf.Min(_phase3ThresholdNormalized, _phase2ThresholdNormalized);
        }

#if UNITY_EDITOR
        [ContextMenu("Save Current Values")]
        private void SaveBossCurrentValues()
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
            EnemyDataEvents.RaiseAssetSaved(this);
            Debug.Log($"[BossData] Saved: {name}");
        }
#endif
    }
}
