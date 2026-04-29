namespace LostMemory.Relics
{
    /// <summary>
    /// 유물/소모품 효과의 *기계 분류*. 후속 Wave 의 hook 들이 switch 로 분기.
    /// CL-106 Wave A 시점에는 MVP 10 + HealConsumablePercent 만 정의.
    /// 비-MVP / 랜덤박스 / 신규 stat 효과는 후속 Wave 에서 enum 값 추가.
    /// </summary>
    public enum RelicEffectType
    {
        None = 0,                         // 기본값. Wave A 시점 미구현 효과 (비-MVP 등)
        AttackPowerPercent,               // 전사의 끈 — 영구 +%
        AttackSpeedOnKillTimed,           // 붉은 송곳니 — 적 처치 시 임시 +%
        FinisherDamagePercent,            // 분쇄의 팔찌 — 영구 (3타에만 적용)
        AttackPowerConditional,           // 전투 북 — HP ≥ Threshold 일 때 +%
        MaxHealthPercent,                 // 수호의 파편 — 영구 +% (등록 시 1회 적용)
        ShieldOnParry,                    // 반격의 표식 — 패링 성공 시 보호막 (Magnitude=비율, Duration=지속)
        HealReceivedPercent,              // 철의 깃 — 영구 +%
        MoveSpeedPercent,                 // 바람 깃털 — 영구 +%
        DashCooldownPercent,              // 질풍 장화 — 영구 -% (음수 multiplier)
        MoveSpeedAfterDashTimed,          // 추적자의 망토 — 대시 종료 시 임시 +%
        HealConsumablePercent,            // 작은/큰 회복약 — Magnitude = 회복률 (0.25 / 0.5)
    }
}
