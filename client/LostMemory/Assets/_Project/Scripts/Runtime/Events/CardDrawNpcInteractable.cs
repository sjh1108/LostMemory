using LostMemory.Stage;
using UnityEngine;

namespace LostMemory.Events
{
    /// <summary>
    /// CL-227 V2 — 카드 뽑기 NPC F 키 trigger.
    /// VendingMachineInteractable 패턴 복제 + **1회 가드** (`_consumed`).
    /// 카드 1번 뽑으면 NPC 영구 비활성 — 같은 방에서 재뽑기 불가.
    /// (Vending Machine 은 다회 구매 가능했음.)
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Events/Card Draw Npc Interactable")]
    [RequireComponent(typeof(Collider2D))]
    public sealed class CardDrawNpcInteractable : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("씬의 CardDrawController (싱글톤). null 이면 첫 F 키 시 FindFirstObjectByType.")]
        [SerializeField] private CardDrawController controller;
        [SerializeField] private CardDrawConfig config;
        [Tooltip("이 NPC 의 방 RoomEntryRuntimeController. null 이면 Awake 에서 GetComponentInParent.")]
        [SerializeField] private RoomEntryRuntimeController roomController;

        [Header("Config")]
        [SerializeField] private KeyCode interactKey = KeyCode.F;
        [SerializeField] private string playerTag = "Player";
        [Tooltip("(옵션) 'F 누르세요' placeholder. in-range 시 활성. null 허용.")]
        [SerializeField] private GameObject promptObject;

        [Header("Debug")]
        [SerializeField] private bool logInteraction = true;

        private bool _playerInRange;
        private bool _consumed;  // 1회 가드 — 카드 1번 뽑으면 영구 비활성

        private void Awake()
        {
            if (promptObject != null) promptObject.SetActive(false);

            // procedural: 부모 방 자동 탐색
            if (roomController == null)
            {
                roomController = GetComponentInParent<RoomEntryRuntimeController>();
                if (roomController != null && logInteraction)
                    Debug.Log($"[CardDrawNpc] roomController 자동 탐색 → '{roomController.name}'", this);
            }
        }

        private void Update()
        {
            if (!_playerInRange || _consumed) return;
            if (!Input.GetKeyDown(interactKey)) return;

            // lazy resolve controller
            if (controller == null)
            {
                controller = Object.FindFirstObjectByType<CardDrawController>();
                if (controller == null)
                {
                    Debug.LogError("[CardDrawNpc] 씬에 CardDrawController 가 없음. " +
                                   "메뉴 'LostMemory/Card Draw/Sync In Active Scene' 으로 생성하세요.", this);
                    return;
                }
                if (logInteraction)
                    Debug.Log($"[CardDrawNpc] controller lazy resolve → '{controller.name}'", this);
            }
            if (config == null)
            {
                Debug.LogError("[CardDrawNpc] config null. wiring 확인.", this);
                return;
            }

            // controller 열려있으면 닫기 (토글 호환). 단 닫는 거라 _consumed 영향 X.
            if (controller.IsOpen)
            {
                controller.Close();
                return;
            }

            // 1회 가드 — 여는 순간 활성. controller 닫혀도 다시 못 엶.
            _consumed = true;
            if (promptObject != null) promptObject.SetActive(false);

            if (logInteraction) Debug.Log($"[CardDrawNpc] '{interactKey}' → Open (consumed).");
            controller.Open(config, roomController);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;
            _playerInRange = true;
            if (promptObject != null && !_consumed) promptObject.SetActive(true);
            if (logInteraction) Debug.Log("[CardDrawNpc] Player in range.", this);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;
            _playerInRange = false;
            if (promptObject != null) promptObject.SetActive(false);
            if (logInteraction) Debug.Log("[CardDrawNpc] Player out of range.", this);
        }

        private bool IsPlayer(Collider2D other)
        {
            if (string.IsNullOrEmpty(playerTag)) return true;
            return other.CompareTag(playerTag);
        }
    }
}
