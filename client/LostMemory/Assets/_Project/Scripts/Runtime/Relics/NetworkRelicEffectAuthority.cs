using LostMemory.Networking.Common;

namespace LostMemory.Relics
{
    /// <summary>
    /// 멀티 환경에서의 유물 효과 적용 권위. 호스트 권위 모델(docs/04_multiplayer.md)에 따라
    /// *호스트 1명만* 효과를 실제로 주입하고, 클라이언트는 Health/Stat 동기화로 결과만 받는다.
    ///
    /// 동작:
    ///   - 싱글 실행 (NetworkManager 비활성) → true (LocalRelicEffectAuthority 와 동일 동작)
    ///   - 멀티 호스트 → true
    ///   - 멀티 일반 클라이언트 → false (효과 주입 skip)
    ///
    /// 본 권위는 <see cref="HostAuthority.IsHost"/> 와 동일 정책. 추후 권위 모델이 바뀌면
    /// 여기 한 줄만 변경.
    /// </summary>
    public sealed class NetworkRelicEffectAuthority : IRelicEffectAuthority
    {
        public bool IsAuthority => HostAuthority.IsHost;
    }
}
