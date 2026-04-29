namespace LostMemory.Relics
{
    /// <summary>
    /// CL-109: 단일 플레이어 환경의 기본 IRelicEffectAuthority 구현. 항상 true.
    /// 미래 멀티 framework (Mirror/Netcode/Photon 등) 도입 시
    /// NetworkRelicEffectAuthority 를 신설하여 RelicEffectRegistry 의 _authority 만 교체.
    /// </summary>
    public sealed class LocalRelicEffectAuthority : IRelicEffectAuthority
    {
        public bool IsAuthority => true;
    }
}
