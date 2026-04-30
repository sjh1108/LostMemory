using LostMemory.Stage;
using UnityEngine;

namespace LostMemory.Shop
{
    /// <summary>
    /// CL-113 — Shop 방 출구 trigger. 플레이어가 진입하면 `RoomEntryRuntimeController.NotifyCustomRoomCleared()` 발화.
    ///
    /// Shop 방은 *적 처치 클리어 조건* 이 없어 `RoomClearConditionType.Custom` 으로 두고
    /// 본 trigger 가 *수동* 클리어 발화 책임. 한 번 발화 후 self-disable (재진입 무시).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Shop/Shop Exit Trigger")]
    [RequireComponent(typeof(Collider2D))]
    public sealed class ShopExitTrigger : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("CL-113: 클리어 발화 대상 controller. 보통 같은 방 prefab 의 root 에 부착.")]
        [SerializeField] private RoomEntryRuntimeController roomController;

        [Header("Config")]
        [Tooltip("플레이어 검증용 tag. 비워두면 모든 trigger 인식.")]
        [SerializeField] private string playerTag = "Player";

        [Header("Debug")]
        [SerializeField] private bool logTrigger = true;

        private bool _fired;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_fired) return;
            if (!IsPlayer(other)) return;
            if (roomController == null)
            {
                Debug.LogError("[ShopExitTrigger] roomController 가 null. wiring 확인.", this);
                return;
            }

            _fired = true;
            if (logTrigger) Debug.Log($"[ShopExitTrigger] Player 출구 진입 → NotifyCustomRoomCleared.", this);
            roomController.NotifyCustomRoomCleared();

            // 재발화 방지를 위해 비활성화 — 다시 trigger 들어와도 OnTriggerEnter2D 안 돈다.
            enabled = false;
        }

        private bool IsPlayer(Collider2D other)
        {
            if (string.IsNullOrEmpty(playerTag)) return true;
            return other.CompareTag(playerTag);
        }
    }
}
