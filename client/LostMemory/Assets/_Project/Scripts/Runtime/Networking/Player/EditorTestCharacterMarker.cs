using Unity.Netcode;
using UnityEngine;

namespace LostMemory.Networking.Player
{
    /// <summary>
    /// scene-placed 캐릭터에 부착 — NGO 활성 / 비활성 따라 자동 분기.
    ///
    /// - **NGO 활성 시점에 진입** (멀티 흐름 Lobby→Dungeon): Awake 에서 즉시 Destroy → 분신 회피
    /// - **NGO 비활성 진입** (솔로 또는 Editor 단일 씬): scene 캐릭터 보존
    /// - **NGO 가 나중에 시작되는 케이스** (예: Town_solo 솔로 → [생성] → NGO StartHost):
    ///   `OnServerStarted` / `OnClientStarted` 이벤트 구독해서 동적 Destroy. 솔로→멀티 전환 시 분신 발생 차단
    ///
    /// 사용: 던전 8개 씬 + Town_solo 등 scene-placed Player 가 있는 씬의 캐릭터 GameObject 에 부착.
    ///
    /// 클라 회신 (`multi_session_design_reply.md` §6.5) 의 "Editor 단일 씬 테스트 워크플로우 보존" 정책 구현.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Networking/Editor Test Character Marker")]
    public sealed class EditorTestCharacterMarker : MonoBehaviour
    {
        [SerializeField, Tooltip("디버그 로그 출력 여부")]
        private bool verboseLog = true;

        private NetworkManager _subscribedManager;

        private void Awake()
        {
            NetworkManager nm = NetworkManager.Singleton;

            if (nm != null && nm.IsListening)
            {
                if (verboseLog)
                {
                    Debug.Log($"[EditorTestCharacterMarker] NGO 활성 — scene-placed 캐릭터 즉시 Destroy: {gameObject.name}", this);
                }
                Destroy(gameObject);
                return;
            }

            // NGO 비활성 = 솔로 또는 Editor 단일 씬. scene 캐릭터 보존.
            // 단, 향후 NGO 가 시작되면 (예: 솔로→멀티 전환) 동적 Destroy.
            if (nm != null)
            {
                nm.OnServerStarted += HandleNetworkStarted;
                nm.OnClientStarted += HandleNetworkStarted;
                _subscribedManager = nm;
            }

            if (verboseLog)
            {
                Debug.Log($"[EditorTestCharacterMarker] NGO 비활성 — scene-placed 캐릭터 보존 (NGO 시작 이벤트 대기): {gameObject.name}", this);
            }
        }

        private void HandleNetworkStarted()
        {
            if (verboseLog)
            {
                Debug.Log($"[EditorTestCharacterMarker] NGO 시작 감지 — scene-placed 캐릭터 동적 Destroy: {gameObject.name}", this);
            }
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_subscribedManager != null)
            {
                _subscribedManager.OnServerStarted -= HandleNetworkStarted;
                _subscribedManager.OnClientStarted -= HandleNetworkStarted;
                _subscribedManager = null;
            }
        }
    }
}
