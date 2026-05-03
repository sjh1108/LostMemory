using Unity.Netcode;

namespace LostMemory.Networking.Common
{
    /// <summary>
    /// 호스트 권위 게이트의 단일 진입점. 기존 IRelicEffectAuthority / RunManager.IsAuthority 등
    /// 분산된 권위 판정을 한 곳으로 모은다.
    ///
    /// 정책: NetworkManager 가 없거나(=싱글 실행) 호스트(=서버) 일 때 권위 보유.
    /// </summary>
    public static class HostAuthority
    {
        /// <summary>
        /// 네트워크 세션이 활성이면 호스트(서버) 인지 여부. 비활성이면 true(싱글 실행 = 권위 가진다).
        /// </summary>
        public static bool IsHost
        {
            get
            {
                NetworkManager nm = NetworkManager.Singleton;
                if (nm == null) return true;
                if (!nm.IsListening) return true;
                return nm.IsServer;
            }
        }

        /// <summary>
        /// 멀티 세션이 활성 중인지. 싱글 실행과 호스트 권위 분기를 구분할 때 쓴다.
        /// </summary>
        public static bool IsNetworkSessionActive
        {
            get
            {
                NetworkManager nm = NetworkManager.Singleton;
                return nm != null && nm.IsListening;
            }
        }
    }
}
