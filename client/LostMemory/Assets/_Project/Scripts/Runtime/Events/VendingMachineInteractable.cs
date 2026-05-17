using LostMemory.Stage;
using UnityEngine;

namespace LostMemory.Events
{
    /// <summary>
    /// CL-228 (A 옵션) — Vending machine NPC F 키 trigger.
    /// 단순화: ShopData 동적 generation 없이 config 만 controller 에 전달.
    ///
    /// procedural 스폰 지원:
    ///   - controller ref 가 null 이면 첫 F 키 시 FindFirstObjectByType 으로 lazy resolve
    ///   - roomController ref 가 null 이면 Awake 에서 GetComponentInParent 로 자동 탐색
    ///     → NPC 가 방 prefab 의 자식이면 자동으로 *자기 방* 의 RoomEntryRuntimeController 매핑
    ///   - 결과: NPC prefab 만들어두면 어떤 방에서 instantiate 되어도 알아서 작동
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Events/Vending Machine Interactable")]
    [RequireComponent(typeof(Collider2D))]
    public sealed class VendingMachineInteractable : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("씬에 있는 VendingMachineController (싱글톤). null 이면 첫 F 키 시 FindFirstObjectByType 으로 lazy resolve.")]
        [SerializeField] private VendingMachineController controller;
        [SerializeField] private VendingMachineConfig config;
        [Tooltip("이 NPC 가 속한 방의 RoomEntryRuntimeController. null 이면 Awake 에서 GetComponentInParent 로 자동 탐색. " +
                 "방 prefab 의 자식으로 NPC 가 박혀있으면 자동으로 매핑됨.")]
        [SerializeField] private RoomEntryRuntimeController roomController;

        [Header("Config")]
        [SerializeField] private KeyCode interactKey = KeyCode.F;
        [SerializeField] private string playerTag = "Player";
        [Tooltip("(옵션) 'F 누르세요' placeholder. in-range 시 활성. null 허용.")]
        [SerializeField] private GameObject promptObject;

        [Header("Debug")]
        [SerializeField] private bool logInteraction = true;

        private bool _playerInRange;

        private void Awake()
        {
            if (promptObject != null) promptObject.SetActive(false);

            // procedural 지원: NPC 가 방 prefab 자식이면 자동으로 자기 방 매핑.
            // 인스펙터에서 수동 wire 한 경우 그 값 우선.
            if (roomController == null)
            {
                roomController = GetComponentInParent<RoomEntryRuntimeController>();
                if (roomController != null && logInteraction)
                    Debug.Log($"[VendingMachineNpc] roomController 자동 탐색 → '{roomController.name}'", this);
            }
        }

        private void Update()
        {
            if (!_playerInRange) return;
            if (!Input.GetKeyDown(interactKey)) return;

            // procedural 지원: 인스펙터 ref 가 비어있으면 lazy resolve.
            if (controller == null)
            {
                controller = Object.FindFirstObjectByType<VendingMachineController>();
                if (controller == null)
                {
                    Debug.LogError("[VendingMachineNpc] 씬에 VendingMachineController 가 없음. " +
                                   "메뉴 'LostMemory/Vending Machine/Sync In Active Scene' 으로 생성하세요.", this);
                    return;
                }
                if (logInteraction)
                    Debug.Log($"[VendingMachineNpc] controller lazy resolve → '{controller.name}'", this);
            }
            if (config == null)
            {
                Debug.LogError("[VendingMachineNpc] config null. wiring 확인.", this);
                return;
            }

            if (logInteraction) Debug.Log($"[VendingMachineNpc] '{interactKey}' → Toggle (room='{roomController?.name ?? "(none)"}').");
            controller.Toggle(config, roomController);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;
            _playerInRange = true;
            if (promptObject != null) promptObject.SetActive(true);
            if (logInteraction) Debug.Log("[VendingMachineNpc] Player in range.", this);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;
            _playerInRange = false;
            if (promptObject != null) promptObject.SetActive(false);
            if (logInteraction) Debug.Log("[VendingMachineNpc] Player out of range.", this);
        }

        private bool IsPlayer(Collider2D other)
        {
            if (string.IsNullOrEmpty(playerTag)) return true;
            return other.CompareTag(playerTag);
        }
    }
}
