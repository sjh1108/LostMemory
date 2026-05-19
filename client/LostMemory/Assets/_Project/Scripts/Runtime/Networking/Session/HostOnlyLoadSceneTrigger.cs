using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostMemory.Networking.Session
{
    /// <summary>
    /// Test_MultiLobby (또는 다른 멀티 씬) 의 진입 포탈 트리거.
    /// 플레이어 trigger 진입 + interactKey 입력 시 **호스트만** NGO LoadScene 호출.
    /// 게스트는 NGO scene sync 로 자동 따라옴.
    ///
    /// 사용:
    ///   - 본 컴포넌트가 부착된 GameObject 에 IsTrigger 인 Collider2D 필요
    ///   - `targetSceneName` 슬롯에 NGO LoadScene 대상 씬 이름 (Build Settings 등록 필수)
    ///
    /// MultiLobbyPortalTrigger 와 차이:
    ///   - MultiLobbyPortalTrigger = 세션 UI 토글 (호스트/게스트 양쪽 가능)
    ///   - HostOnlyLoadSceneTrigger = NGO LoadScene (호스트만, 게스트는 자동 sync)
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("Lost Memory/Networking/Host Only Load Scene Trigger")]
    public sealed class HostOnlyLoadSceneTrigger : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField, Tooltip("NGO LoadScene 대상 씬 이름. Build Settings 에 enabled 등록 필수.")]
        private string targetSceneName = "Mob_Sync_Test";

        [SerializeField, Tooltip("LoadScene 모드.")]
        private UnityEngine.SceneManagement.LoadSceneMode loadSceneMode = UnityEngine.SceneManagement.LoadSceneMode.Single;

        [Header("Interaction")]
        [SerializeField, Tooltip("플레이어 식별 Tag.")]
        private string playerTag = "Player";

        [SerializeField, Tooltip("진입 키.")]
        private Key interactKey = Key.E;

        [Header("Debug")]
        [SerializeField] private bool verboseLog = false;

        private bool _playerInside;

        private void Awake()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null && !col.isTrigger)
            {
                Debug.LogWarning($"[HostOnlyLoadSceneTrigger] Collider2D 가 isTrigger=false. 자동 전환.", this);
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;
            _playerInside = true;
            if (verboseLog) Debug.Log($"[HostOnlyLoadSceneTrigger] Player entered. Host can press {interactKey} to load '{targetSceneName}'.", this);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;
            _playerInside = false;
        }

        private void Update()
        {
            if (!_playerInside) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (!keyboard[interactKey].wasPressedThisFrame) return;

            TryLoadScene();
        }

        private void TryLoadScene()
        {
            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                Debug.LogWarning($"[HostOnlyLoadSceneTrigger] targetSceneName 비어있음.", this);
                return;
            }

            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening)
            {
                Debug.LogWarning($"[HostOnlyLoadSceneTrigger] NetworkManager 비활성. 솔로 환경에선 LoadScene 안 함.", this);
                return;
            }

            if (!nm.IsServer)
            {
                // 게스트가 trigger 안에서 키 눌러도 무시 — 호스트만 씬 전환 가능. NGO sync 로 자동 따라옴.
                if (verboseLog) Debug.Log($"[HostOnlyLoadSceneTrigger] 게스트 입력 무시 — 호스트만 LoadScene 호출.", this);
                return;
            }

            if (verboseLog) Debug.Log($"[HostOnlyLoadSceneTrigger] 호스트 LoadScene: {targetSceneName}", this);
            nm.SceneManager.LoadScene(targetSceneName, loadSceneMode);
        }
    }
}
