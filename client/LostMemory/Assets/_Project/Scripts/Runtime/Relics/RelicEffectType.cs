namespace LostMemory.Relics
{
    /// <summary>
    /// 유물/소모품/세트 효과의 *기계 분류*. RelicEffectRegistry / BuildManager 의 switch 가 분기.
    /// CL-106 Wave A 시점: MVP 10 + HealConsumablePercent.
    /// CL-138 추가: 세트 효과(BuildSetData.SetTier) 용 19개 값.
    /// </summary>
    public enum RelicEffectType
    {
        None = 0,                         // 기본값. 미구현 효과 (비-MVP 등)

        // CL-106 Wave A — 단일 RelicData 효과
        AttackPowerPercent,               // 전사의 끈 — 영구 +%
        AttackSpeedOnKillTimed,           // 붉은 송곳니 — 적 처치 시 임시 +%
        FinisherDamagePercent,            // 분쇄의 팔찌 — 영구 (3타에만 적용)
        AttackPowerConditional,           // 전투 북 — HP ≥ Threshold 일 때 +%
        MaxHealthPercent,                 // 수호의 파편 — 영구 +% (등록 시 1회 적용)
        ShieldOnParry,                    // 반격의 표식 — 패링 성공 시 보호막
        HealReceivedPercent,              // 철의 깃 — 영구 +%
        MoveSpeedPercent,                 // 바람 깃털 — 영구 +%
        DashCooldownPercent,              // 질풍 장화 — 영구 -% (음수 multiplier)
        MoveSpeedAfterDashTimed,          // 추적자의 망토 — 대시 종료 시 임시 +%
        HealConsumablePercent,            // 작은/큰 회복약 — Magnitude = 회복률

        // CL-138 추가 — 세트 효과 (BuildSetData.SetTier 에서 사용)
        CriticalChancePercent,            // 치명타
        CooldownReductionPercent,         // 쿨감
        AttackRangePercent,               // 범위
        DodgeChancePercent,               // 회피
        DefenseFlat,                      // 방어력 (+4, +8 flat)
        GoldGainPercent,                  // 탐욕
        LuckPoints,                       // 행운 (스택 수)
        BurnOnHit,                        // 불 도트 (on-hit 트리거)
        SlowOnHit,                        // 얼음 슬로우
        FreezeOnHit,                      // 얼음 빙결 (3티어)
        ChainOnHit,                       // 전기 체인
        WindAOE,                          // 바람 광역
        MagicalGirlSummon,                // 미소녀 1~4 (entity)
        MagicalGirlFusion,                // 미소녀 5합체 (entity, 특수)
        MagicalGirlElementalAttack,       // 미소녀 속성 공격 (38~41 일반)
        MagicalGirlElementalEnhanced,     // 미소녀 강화 속성 공격 (45, 47 등)
        TarotProc,                        // 타로 발동
        LuckSlotExpand,                   // 행운 3스택 슬롯+1
        LuckLegendaryGuarantee,           // 행운 7스택 무조건 전설

        // CL-141 추가 — items_draft 의 "공속 +N%" 효과 표현용 (10+ 아이템)
        AttackSpeedPercent,               // 공속 영구 % (StatId.AttackSpeed 라우팅)

        // CL-147 추가 — 타로 카드 효과 강화 multiplier (TarotSystem.SetEffectMultiplier 라우팅)
        TarotEffectMultiplier,            // 0.0 = base 효과, 1.0 = 효과 2배
    }
}
