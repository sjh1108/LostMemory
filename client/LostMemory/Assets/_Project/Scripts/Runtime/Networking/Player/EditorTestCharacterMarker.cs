using Unity.Netcode;
using UnityEngine;

namespace LostMemory.Networking.Player
{
    /// <summary>
    /// scene-placed 캐릭터에 부착 — NGO 활성 / 비활성 따라 자동 분기.
    ///
    /// - **NGO 활성 (정상 멀티 흐름 Lobby→Dungeon)**: NetworkManager 가 PlayerPrefab 으로 spawn 하므로
    ///   scene 의 본 캐릭터 제거 → 분신 (캐릭터 복제) 버그 회피
    /// - **NGO 비활성 (Editor 단일 씬 직접 Play)**: scene 캐릭터 그대로 보존 → 일상 개발/회귀 테스트 가능
    ///
    /// 사용: 던전 8개 씬 (`Dungeon.unity`, `Dungeon_1F_1R.unity` ~ `Dungeon_1F_Boss.unity`, `Dungeon_1F_Shop.unity`)
    /// 의 scene-placed `TestKhi_MinimalCharacter2D` 캐릭터 GameObject 에 본 컴포넌트만 부착하면 끝.
    ///
    /// 클라 회신 (`multi_session_design_reply.md` §6.5) 의 "Editor 단일 씬 테스트 워크플로우 보존" 정책 구현.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Networking/Editor Test Character Marker")]
    public sealed class EditorTestCharacterMarker : MonoBehaviour
    {
        [SerializeField, Tooltip("디버그 로그 출력 여부")]
        private bool verboseLog = true;

        private void Awake()
        {
            // NetworkManager 가 listening (host/client 중) = 정상 멀티 흐름
            // → NGO 가 PlayerPrefab spawn 할 거니까 scene 의 본 캐릭터 destroy
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                if (verboseLog)
                {
                    Debug.Log($"[EditorTestCharacterMarker] NGO 활성 — scene-placed 캐릭터 자동 Destroy: {gameObject.name}", this);
                }
                Destroy(gameObject);
                return;
            }

            // NGO 비활성 = Editor 단일 씬 직접 Play. scene 캐릭터 그대로 보존
            if (verboseLog)
            {
                Debug.Log($"[EditorTestCharacterMarker] NGO 비활성 — scene-placed 캐릭터 보존: {gameObject.name}", this);
            }
        }
    }
}
