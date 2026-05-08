using MoreMountains.TopDownEngine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostMemory.SceneFlow
{
    /// <summary>
    /// Carries one-shot spawn requests across a scene load and applies them when Town opens.
    /// </summary>
    public static class TownSpawnRouter
    {
        public const string DefaultTownSceneName = "Town";
        public const string TownSpawnPointName = "TownSpawnPoint";
        public const string TownReturnSpawnPointName = "TownReturnSpawnPoint";
        public const string DefaultPlayerId = "Player1";

        private static string pendingSceneName;
        private static string pendingSpawnPointName;
        private static string pendingPlayerId;
        private static bool registered;

        public static void RequestTownReturn(string townSceneName = DefaultTownSceneName, string playerId = DefaultPlayerId)
        {
            RequestSpawn(townSceneName, TownReturnSpawnPointName, playerId);
        }

        public static void RequestSpawn(string sceneName, string spawnPointName, string playerId = DefaultPlayerId)
        {
            if (string.IsNullOrWhiteSpace(sceneName) || string.IsNullOrWhiteSpace(spawnPointName))
            {
                return;
            }

            EnsureRegistered();
            pendingSceneName = sceneName;
            pendingSpawnPointName = spawnPointName;
            pendingPlayerId = playerId;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            pendingSceneName = null;
            pendingSpawnPointName = null;
            pendingPlayerId = null;
            registered = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            EnsureRegistered();
            Scene activeScene = SceneManager.GetActiveScene();
            if (!TryApplyPendingSpawn(activeScene))
            {
                TryApplyDefaultTownSpawn(activeScene);
            }
        }

        private static void EnsureRegistered()
        {
            if (registered)
            {
                return;
            }

            SceneManager.sceneLoaded += HandleSceneLoaded;
            registered = true;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!TryApplyPendingSpawn(scene))
            {
                TryApplyDefaultTownSpawn(scene);
            }
        }

        private static bool TryApplyPendingSpawn(Scene scene)
        {
            if (string.IsNullOrEmpty(pendingSceneName) ||
                string.IsNullOrEmpty(pendingSpawnPointName) ||
                !scene.IsValid() ||
                scene.name != pendingSceneName)
            {
                return false;
            }

            Transform spawnPoint = FindSceneTransform(scene, pendingSpawnPointName);
            if (spawnPoint == null)
            {
                Debug.LogWarning($"[TownSpawnRouter] Spawn point '{pendingSpawnPointName}' was not found in scene '{scene.name}'.");
                ClearPendingSpawn();
                return false;
            }

            Character player = FindPlayer(scene, pendingPlayerId);
            if (player == null)
            {
                Debug.LogWarning($"[TownSpawnRouter] Player '{pendingPlayerId}' was not found in scene '{scene.name}'.");
                ClearPendingSpawn();
                return false;
            }

            MovePlayer(player, spawnPoint.position);
            ClearPendingSpawn();
            return true;
        }

        private static void TryApplyDefaultTownSpawn(Scene scene)
        {
            if (!scene.IsValid() || scene.name != DefaultTownSceneName)
            {
                return;
            }

            Transform spawnPoint = FindSceneTransform(scene, TownSpawnPointName);
            if (spawnPoint == null)
            {
                Debug.LogWarning($"[TownSpawnRouter] Default spawn point '{TownSpawnPointName}' was not found in scene '{scene.name}'.");
                return;
            }

            Character player = FindPlayer(scene, DefaultPlayerId);
            if (player == null)
            {
                Debug.LogWarning($"[TownSpawnRouter] Player '{DefaultPlayerId}' was not found in scene '{scene.name}'.");
                return;
            }

            MovePlayer(player, spawnPoint.position);
        }

        private static Transform FindSceneTransform(Scene scene, string objectName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < transforms.Length; j++)
                {
                    if (transforms[j].name == objectName)
                    {
                        return transforms[j];
                    }
                }
            }

            return null;
        }

        private static Character FindPlayer(Scene scene, string playerId)
        {
            Character[] characters = Object.FindObjectsByType<Character>(FindObjectsSortMode.None);
            for (int i = 0; i < characters.Length; i++)
            {
                Character character = characters[i];
                if (character == null ||
                    character.gameObject.scene != scene ||
                    character.CharacterType != Character.CharacterTypes.Player)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(playerId) || character.PlayerID == playerId)
                {
                    return character;
                }
            }

            return null;
        }

        private static void MovePlayer(Character player, Vector3 position)
        {
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = position;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            player.transform.position = position;
        }

        private static void ClearPendingSpawn()
        {
            pendingSceneName = null;
            pendingSpawnPointName = null;
            pendingPlayerId = null;
        }
    }
}
