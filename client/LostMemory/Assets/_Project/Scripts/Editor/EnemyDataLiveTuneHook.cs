#if UNITY_EDITOR
using LostMemory.Editor.BalanceEditor;
using LostMemory.Enemies;
using LostMemory.Enemies.Boss.Bertha;
using UnityEditor;
using UnityEngine;

namespace LostMemory.EditorTools
{
    [InitializeOnLoad]
    public static class EnemyDataLiveTuneHook
    {
        static EnemyDataLiveTuneHook()
        {
            EnemyDataEvents.OnAssetSaved -= OnAssetSaved;
            EnemyDataEvents.OnAssetSaved += OnAssetSaved;
            BalanceEditorWindow.ScriptableObjectChanged -= OnBalanceEditorScriptableObjectChanged;
            BalanceEditorWindow.ScriptableObjectChanged += OnBalanceEditorScriptableObjectChanged;
        }

        private static void OnAssetSaved(EnemyData asset)
        {
            ApplyIfEnemyData(asset);
        }

        private static void OnBalanceEditorScriptableObjectChanged(ScriptableObject asset)
        {
            ApplyIfEnemyData(asset);
        }

        public static void ApplyIfEnemyData(ScriptableObject asset)
        {
            if (asset == null || !EditorApplication.isPlaying)
            {
                return;
            }

            if (asset is not EnemyData enemyData)
            {
                return;
            }

            EnemyDataRuntimeAdapter.ApplySavedAsset(enemyData);

            if (enemyData is BossData bossData)
            {
                BerthaLightAttack1Bootstrap.ApplySavedBossData(bossData);
            }
        }
    }
}
#endif
