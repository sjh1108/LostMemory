using UnityEngine;

namespace LostMemory.Dialogue
{
    /// <summary>
    /// 범용 대화 트리거. NPC GameObject 또는 트리거 영역 Collider 에 부착.
    ///
    /// 3가지 모드:
    ///   - OnTriggerEnter: 플레이어가 Collider 안에 들어오는 순간 자동 발화
    ///   - OnInteract: 플레이어가 범위 안일 때 interactKey(F) 누르면 발화. (기존 ShopNpcInteractable 패턴)
    ///   - Manual: 외부 스크립트가 Trigger() 를 호출할 때만 발화
    ///
    /// oneShot=true: 한 번 발화 후 비활성. 게임 진행 상 다시 안 보여줄 대화에 사용.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Dialogue/Dialogue Trigger")]
    [RequireComponent(typeof(Collider2D))]
    public sealed class DialogueTrigger : MonoBehaviour
    {
        public enum TriggerMode
        {
            OnTriggerEnter,
            OnInteract,
            Manual,
        }

        [Header("Dialogue")]
        [Tooltip("DialogueDatabase 의 groupId. 예: 'boss_intro', 'shopkeeper_greeting'")]
        [SerializeField] private string dialogueGroupId;
        [Tooltip("이 trigger 발화 시 사용할 skin. null 이면 controller 의 defaultSkin 유지.")]
        [SerializeField] private DialogueSkin skinOverride;

        [Header("Mode")]
        [SerializeField] private TriggerMode triggerMode = TriggerMode.OnInteract;
        [Tooltip("한 번만 발화. true → 종료 후 자동 비활성.")]
        [SerializeField] private bool oneShot = true;

        [Header("Detection")]
        [Tooltip("플레이어 검증용 tag. 비우면 모든 trigger 인식.")]
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private KeyCode interactKey = KeyCode.F;
        [Tooltip("(선택) 'F 누르세요' placeholder GameObject. in-range 시 활성, 이탈 시 비활성.")]
        [SerializeField] private GameObject promptObject;

        [Header("Debug")]
        [SerializeField] private bool debugLogging;

        private bool _playerInRange;
        private bool _hasFired;

        private void Awake()
        {
            if (promptObject != null) promptObject.SetActive(false);
        }

        private void Update()
        {
            if (triggerMode != TriggerMode.OnInteract) return;
            if (!_playerInRange) return;
            if (_hasFired && oneShot) return;
            if (Input.GetKeyDown(interactKey))
            {
                Fire();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;
            _playerInRange = true;
            if (promptObject != null && (triggerMode == TriggerMode.OnInteract) && !(oneShot && _hasFired))
            {
                promptObject.SetActive(true);
            }
            if (debugLogging) Debug.Log($"[DialogueTrigger:{dialogueGroupId}] Player in range.", this);

            if (triggerMode == TriggerMode.OnTriggerEnter && !(oneShot && _hasFired))
            {
                Fire();
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;
            _playerInRange = false;
            if (promptObject != null) promptObject.SetActive(false);
            if (debugLogging) Debug.Log($"[DialogueTrigger:{dialogueGroupId}] Player out of range.", this);
        }

        /// <summary>
        /// 외부에서 직접 호출 (Manual mode 또는 cutscene 진입 등).
        /// </summary>
        public void Trigger() => Fire();

        private void Fire()
        {
            if (string.IsNullOrEmpty(dialogueGroupId))
            {
                Debug.LogWarning("[DialogueTrigger] dialogueGroupId 가 비어 있음.", this);
                return;
            }

            DialogueController controller = DialogueController.Instance;
            if (controller == null)
            {
                Debug.LogError("[DialogueTrigger] Scene 에 DialogueController 가 없음. DialoguePanel.prefab 을 Canvas 에 배치하세요.", this);
                return;
            }

            if (skinOverride != null) controller.ApplySkin(skinOverride);

            _hasFired = true;
            if (promptObject != null) promptObject.SetActive(false);

            if (debugLogging) Debug.Log($"[DialogueTrigger] Fire '{dialogueGroupId}'.", this);
            controller.Show(dialogueGroupId, onGroupFinished: () =>
            {
                if (oneShot)
                {
                    gameObject.SetActive(false);
                }
            });
        }

        private bool IsPlayer(Collider2D other)
        {
            if (string.IsNullOrEmpty(playerTag)) return true;
            return other.CompareTag(playerTag);
        }
    }
}
