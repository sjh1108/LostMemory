using UnityEngine;

namespace LostMemory.Enemies
{
    public static class EnemyCollisionPolicy
    {
        private const string EnemyLayerName = "Enemies";
        private static bool _missingLayerWarningLogged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyOnSceneLoad()
        {
            Apply();
        }

        public static void Apply()
        {
            int enemyLayer = LayerMask.NameToLayer(EnemyLayerName);
            if (enemyLayer < 0)
            {
                LogMissingLayerWarning();
                return;
            }

            Physics2D.IgnoreLayerCollision(enemyLayer, enemyLayer, true);
            Physics.IgnoreLayerCollision(enemyLayer, enemyLayer, true);
        }

        private static void LogMissingLayerWarning()
        {
            if (_missingLayerWarningLogged)
            {
                return;
            }

            _missingLayerWarningLogged = true;
            Debug.LogWarning($"[EnemyCollisionPolicy] Layer '{EnemyLayerName}' was not found. Enemy self-collision remains enabled.");
        }
    }
}
