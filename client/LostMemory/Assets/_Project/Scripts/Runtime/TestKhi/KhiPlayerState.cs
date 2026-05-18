namespace LostMemory.TestKhi
{
    /// <summary>
    /// CL-015 플레이어 단일 상태 enum. Aggregator가 매 프레임 재계산해 노출한다.
    /// Parry Cooldown과 HitStun PostHitIFrame은 입력 가능 시간이므로 Idle/Move 등으로 폴백된다.
    /// </summary>
    public enum KhiPlayerState
    {
        Idle,
        Move,
        Attack,
        Dash,
        Parry,
        Hurt,
        Down,
        Defeated
    }
}
