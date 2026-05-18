using System.Collections;
using UnityEngine;

namespace LostMemory.Stage
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Stage/Boss Scene Auto Starter")]
    public sealed class BossSceneAutoStarter : MonoBehaviour
    {
        [SerializeField] private BossRoomLocalTransitionDriver transitionDriver;
        [SerializeField] private bool autoStartOnEnable = true;
        [SerializeField, Min(0f)] private float startDelaySeconds = 0.5f;
        [SerializeField, Min(1)] private int maxStartAttempts = 5;
        [SerializeField, Min(0.05f)] private float retryIntervalSeconds = 0.25f;
        [SerializeField] private string bossRoomId = "Boss";
        [SerializeField] private string bossEntryPointId = "Boss";
        [SerializeField] private string bossSceneName = string.Empty;
        [SerializeField] private bool debugLogging;

        private Coroutine _startRoutine;
        private bool _started;

        private void Reset()
        {
            ResolveTransitionDriver();
        }

        private void OnEnable()
        {
            if (autoStartOnEnable)
            {
                StartBossFromSceneEntry();
            }
        }

        private void OnDisable()
        {
            if (_startRoutine != null)
            {
                StopCoroutine(_startRoutine);
                _startRoutine = null;
            }
        }

        public void StartBossFromSceneEntry()
        {
            if (_started || _startRoutine != null)
            {
                return;
            }

            _startRoutine = StartCoroutine(StartRoutine());
        }

        private IEnumerator StartRoutine()
        {
            if (startDelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(startDelaySeconds);
            }
            else
            {
                yield return null;
            }

            int attempts = Mathf.Max(1, maxStartAttempts);
            for (int i = 0; i < attempts; i++)
            {
                if (TryStartOnce())
                {
                    _started = true;
                    _startRoutine = null;
                    yield break;
                }

                if (i + 1 < attempts)
                {
                    yield return new WaitForSecondsRealtime(retryIntervalSeconds);
                }
            }

            Debug.LogWarning("[BossSceneAutoStarter] Failed to start boss encounter from scene entry.", this);
            _startRoutine = null;
        }

        private bool TryStartOnce()
        {
            ResolveTransitionDriver();
            if (transitionDriver == null)
            {
                Log("No BossRoomLocalTransitionDriver found yet.");
                return false;
            }

            bool started = transitionDriver.TryStartRouteEntry(bossRoomId, bossEntryPointId, bossSceneName);
            if (started)
            {
                Log("Boss encounter route entry started.");
            }

            return started;
        }

        private void ResolveTransitionDriver()
        {
            if (transitionDriver != null)
            {
                return;
            }

            transitionDriver = GetComponent<BossRoomLocalTransitionDriver>();
            if (transitionDriver != null)
            {
                return;
            }

            transitionDriver = GetComponentInChildren<BossRoomLocalTransitionDriver>(includeInactive: true);
            if (transitionDriver != null)
            {
                return;
            }

            BossRoomLocalTransitionDriver[] drivers =
                FindObjectsByType<BossRoomLocalTransitionDriver>(FindObjectsSortMode.None);
            if (drivers.Length > 0)
            {
                transitionDriver = drivers[0];
            }
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[BossSceneAutoStarter] " + message, this);
            }
        }
    }
}
