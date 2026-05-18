using MoreMountains.TopDownEngine;
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

        public static EnemyPlayerBodyCollisionIgnore EnsurePlayerBodyCollisionIgnore(GameObject enemyRoot)
        {
            if (enemyRoot == null)
            {
                return null;
            }

            EnemyPlayerBodyCollisionIgnore ignore = enemyRoot.GetComponent<EnemyPlayerBodyCollisionIgnore>();
            if (ignore == null)
            {
                ignore = enemyRoot.AddComponent<EnemyPlayerBodyCollisionIgnore>();
            }

            ignore.ApplyNow();
            return ignore;
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

    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Enemy Player Body Collision Ignore")]
    public sealed class EnemyPlayerBodyCollisionIgnore : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;

        private Collider2D[] _enemyBodyColliders = System.Array.Empty<Collider2D>();
        private float _nextRefreshTime;

        private void OnEnable()
        {
            RefreshEnemyBodyColliders();
            ApplyNow();
        }

        private void Update()
        {
            if (Time.time < _nextRefreshTime)
            {
                return;
            }

            ApplyNow();
        }

        public void ApplyNow()
        {
            _nextRefreshTime = Time.time + Mathf.Max(0.05f, refreshInterval);

            if (_enemyBodyColliders == null || _enemyBodyColliders.Length == 0)
            {
                RefreshEnemyBodyColliders();
            }

            Character[] characters = Object.FindObjectsByType<Character>(FindObjectsSortMode.None);
            for (int i = 0; i < characters.Length; i++)
            {
                Character character = characters[i];
                if (character == null || character.CharacterType != Character.CharacterTypes.Player)
                {
                    continue;
                }

                IgnoreBodyCollisions(character);
            }
        }

        private void RefreshEnemyBodyColliders()
        {
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(includeInactive: true);
            int bodyColliderCount = 0;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (IsBodyCollider(colliders[i]))
                {
                    bodyColliderCount++;
                }
            }

            _enemyBodyColliders = new Collider2D[bodyColliderCount];
            int writeIndex = 0;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (IsBodyCollider(collider))
                {
                    _enemyBodyColliders[writeIndex++] = collider;
                }
            }
        }

        private void IgnoreBodyCollisions(Character player)
        {
            Collider2D[] playerColliders = player.GetComponentsInChildren<Collider2D>(includeInactive: true);
            for (int i = 0; i < _enemyBodyColliders.Length; i++)
            {
                Collider2D enemyCollider = _enemyBodyColliders[i];
                if (!IsBodyCollider(enemyCollider))
                {
                    continue;
                }

                for (int j = 0; j < playerColliders.Length; j++)
                {
                    Collider2D playerCollider = playerColliders[j];
                    if (!IsBodyCollider(playerCollider))
                    {
                        continue;
                    }

                    Physics2D.IgnoreCollision(enemyCollider, playerCollider, true);
                }
            }
        }

        private static bool IsBodyCollider(Collider2D collider)
        {
            return collider != null && !collider.isTrigger;
        }
    }
}
