using UnityEngine;
using UnityEngine.InputSystem;

namespace LostMemory.Networking.Session
{
    /// <summary>
    /// Test_Town 의 멀티 로비 입구 트리거.
    /// 플레이어가 trigger 진입 + interactKey 입력 시 세션 UI (RelayJoinCodeUI 부착된 GameObject)
    /// 를 활성화한다. 트리거 벗어나면 자동 비활성.
    ///
    /// 설계 의도:
    ///   - 정식 흐름: Test_Title → Test_Town → 본 트리거 → 세션 UI → [생성] / [참가] → Test_MultiLobby
    ///   - 호스트가 [생성] 누르면 RelayJoinCodeUI 가 NGO LoadScene 호출 → 호스트 + 게스트 자동 이동
    ///
    /// 부착:
    ///   - 본 컴포넌트가 부착된 GameObject 에 IsTrigger 인 Collider2D 필요
    ///   - `sessionUI` 슬롯에 RelayJoinCodeUI 부착된 Canvas (또는 그 자식 panel) 드래그
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("Lost Memory/Networking/Multi Lobby Portal Trigger")]
    public sealed class MultiLobbyPortalTrigger : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField, Tooltip("활성/비활성 토글할 세션 UI GameObject (RelayJoinCodeUI 부착된 Canvas 또는 panel)")]
        private GameObject sessionUI;

        [Header("Interaction")]
        [SerializeField, Tooltip("플레이어 식별 Tag. Test_shm root 의 Tag 가 'Player' 인 환경 기준.")]
        private string playerTag = "Player";

        [SerializeField, Tooltip("UI 토글 키 (Unity InputSystem Key enum).")]
        private Key interactKey = Key.E;

        [Header("Debug")]
        [SerializeField] private bool verboseLog = false;

        private bool _playerInside;

        private void Awake()
        {
            EnsureColliderIsTrigger();
            // 진입 전엔 UI 숨김 보장.
            if (sessionUI != null && sessionUI.activeSelf)
            {
                sessionUI.SetActive(false);
            }
        }

        private void EnsureColliderIsTrigger()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null && !col.isTrigger)
            {
                Debug.LogWarning($"[MultiLobbyPortalTrigger] Collider2D 가 isTrigger=false. 자동으로 true 로 전환.", this);
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;
            _playerInside = true;
            if (verboseLog) Debug.Log($"[MultiLobbyPortalTrigger] Player entered. Press {interactKey} to toggle session UI.", this);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;
            _playerInside = false;
            if (sessionUI != null && sessionUI.activeSelf)
            {
                sessionUI.SetActive(false);
                if (verboseLog) Debug.Log($"[MultiLobbyPortalTrigger] Player exited. Session UI closed.", this);
            }
        }

        private void Update()
        {
            if (!_playerInside || sessionUI == null) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard[interactKey].wasPressedThisFrame)
            {
                sessionUI.SetActive(!sessionUI.activeSelf);
                if (verboseLog) Debug.Log($"[MultiLobbyPortalTrigger] Session UI {(sessionUI.activeSelf ? "opened" : "closed")}.", this);
            }
        }
    }
}
