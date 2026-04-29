using System.Collections;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Stage
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Stage/Boss Defeat Room Clear Controller")]
    public sealed class BossDefeatRoomClearController : MonoBehaviour
    {
        [SerializeField] private Health bossHealth;
        [SerializeField] private RoomEntryRuntimeController roomController;
        [SerializeField, Min(0f)] private float clearDelaySeconds;
        [SerializeField] private bool debugLogging;

        private Coroutine clearRoutine;
        private bool clearRequested;

        private void Reset()
        {
            RefreshReferences();
        }

        private void OnValidate()
        {
            clearDelaySeconds = Mathf.Max(0f, clearDelaySeconds);
            RefreshReferences();
        }

        private void Awake()
        {
            RefreshReferences();
        }

        private void OnEnable()
        {
            RefreshReferences();

            if (bossHealth != null)
            {
                bossHealth.OnDeath += HandleBossDeath;
            }
        }

        private void OnDisable()
        {
            if (bossHealth != null)
            {
                bossHealth.OnDeath -= HandleBossDeath;
            }

            if (clearRoutine != null)
            {
                StopCoroutine(clearRoutine);
                clearRoutine = null;
            }
        }

        private void RefreshReferences()
        {
            bossHealth ??= GetComponent<Health>();
            roomController ??= GetComponentInParent<RoomEntryRuntimeController>();
            roomController ??= GetComponent<RoomEntryRuntimeController>();
        }

        private void HandleBossDeath()
        {
            if (clearRequested)
            {
                return;
            }

            if (roomController == null)
            {
                Debug.LogWarning("[BossDefeatRoomClearController] Room controller is missing. Cannot clear boss room.", this);
                return;
            }

            if (!roomController.IsAuthority)
            {
                return;
            }

            clearRequested = true;

            if (clearDelaySeconds <= 0f)
            {
                ClearRoom();
                return;
            }

            clearRoutine = StartCoroutine(ClearRoomAfterDelay());
        }

        private IEnumerator ClearRoomAfterDelay()
        {
            yield return new WaitForSeconds(clearDelaySeconds);
            ClearRoom();
        }

        private void ClearRoom()
        {
            clearRoutine = null;
            Log("Boss defeated. Request boss room clear.");
            roomController.NotifyCustomRoomCleared();
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[BossDefeatRoomClearController] " + message, this);
            }
        }
    }
}
