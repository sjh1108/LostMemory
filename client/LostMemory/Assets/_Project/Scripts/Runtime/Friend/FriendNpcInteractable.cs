using LostMemory.TestKhi;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Friend
{
    /// <summary>
    /// 소꿉친구 NPC 의 F 키 상호작용 트리거. NPC GameObject 에 부착.
    ///
    /// 책임:
    ///   1. 플레이어 trigger zone 진입/이탈 추적 (OnTriggerEnter2D / OnTriggerExit2D)
    ///   2. in-range + F 키 입력 → FriendChatController.Toggle() 호출
    ///   3. (선택) 'F 누르세요' promptObject 활성/비활성
    ///
    /// [초심자 설명]
    ///   이 컴포넌트는 NPC가 "말 걸 수 있는 범위"를 담당합니다.
    ///   플레이어가 범위에 들어오면 안내 오브젝트를 켜고, F를 누르면
    ///   FriendChatController에게 "채팅창 열어줘"라고 위임합니다.
    ///   ShopNpcInteractable 과 동일한 패턴.
    ///
    /// 주의: NPC GameObject 에 붙는 컴포넌트 (플레이어 가 아님).
    ///       OnTriggerEnter2D 의 other = 플레이어.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Friend/Friend Npc Interactable")]
    [RequireComponent(typeof(Collider2D))]
    public sealed class FriendNpcInteractable : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private FriendChatController chatController;

        [Header("Config")]
        [SerializeField] private KeyCode interactKey = KeyCode.F;
        [Tooltip("플레이어 검증용 tag. 비워두면 모든 trigger 인식.")]
        [SerializeField] private string playerTag = "Player";
        [Tooltip("(선택) 'F 누르세요' placeholder GameObject. in-range 시 활성, 이탈 시 비활성. null 허용.")]
        [SerializeField] private GameObject promptObject;

        [Header("Debug")]
        [SerializeField] private bool logInteraction = true;

        private bool _playerInRange;
        private Character _playerInRangeCharacter;

        private void Awake()
        {
            if (promptObject != null) promptObject.SetActive(false);
        }

        private void Update()
        {
            if (!_playerInRange) return;
            if (KhiPlayerActionGate.IsBlocked(_playerInRangeCharacter)) return;
            if (!Input.GetKeyDown(interactKey)) return;

            if (chatController == null)
            {
                Debug.LogError("[FriendNpcInteractable] chatController 가 null. Inspector 에서 연결하세요.", this);
                return;
            }

            if (logInteraction) Debug.Log("[FriendNpcInteractable] F 입력 → FriendChatController.Toggle.");
            chatController.Toggle();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;
            _playerInRange = true;
            _playerInRangeCharacter = other.GetComponentInParent<Character>();
            if (promptObject != null) promptObject.SetActive(true);
            if (logInteraction) Debug.Log("[FriendNpcInteractable] 플레이어 범위 진입.", this);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;
            _playerInRange = false;
            if (_playerInRangeCharacter == other.GetComponentInParent<Character>())
                _playerInRangeCharacter = null;
            if (promptObject != null) promptObject.SetActive(false);
            if (logInteraction) Debug.Log("[FriendNpcInteractable] 플레이어 범위 이탈.", this);

            // 채팅창이 열려 있는 상태에서 범위를 벗어나면 강제 닫기
            if (chatController != null && chatController.IsOpen)
                chatController.Close();
        }

        private bool IsPlayer(Collider2D other)
        {
            if (string.IsNullOrEmpty(playerTag)) return true;
            return other.CompareTag(playerTag);
        }
    }
}
