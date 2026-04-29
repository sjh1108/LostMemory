namespace LostMemory.Relics
{
    /// <summary>
    /// CL-109: 유물 효과 적용 권위. future-proof 패턴 — RunManager.IsAuthority 와 동일 시그니처.
    /// 현재 single-player: <see cref="LocalRelicEffectAuthority"/> (항상 true).
    /// 미래 멀티 도입: NetworkRelicEffectAuthority — host 만 true.
    /// RelicEffectRegistry 의 effect 적용 단일 진입점에서 게이트로 사용.
    /// </summary>
    public interface IRelicEffectAuthority
    {
        bool IsAuthority { get; }
    }
}
