namespace LostMemory.Networking.Session
{
    /// <summary>
    /// #144 talent / weapon API 의 백엔드 ErrorCode → 사용자 메시지 매핑.
    /// <see cref="SessionErrorPolicy"/> 의 세션 흐름과 분리 — 도메인 분리 + 발표 D-1 의 최소 변경.
    ///
    /// 사용:
    ///   - <see cref="WeaponInventoryCache.UnlockOutcome.ErrorCode"/> / <see cref="WeaponInventoryCache.SelectOutcome.ErrorCode"/> 받아서 호출
    ///   - TalentSyncService 측은 현재 (성공/실패 bool) 만 반환 — 백엔드 ErrorCode 까지 흘리려면 SaveTalentsAsync 가 envelope 반환하도록 확장 필요 (발표 후 정리)
    /// </summary>
    public static class TalentWeaponErrorPolicy
    {
        public static string ToUserMessage(string code) => code switch
        {
            null                            => "알 수 없는 오류가 발생했습니다.",
            "COMMON_INVALID_INPUT"          => "잘못된 입력입니다.",
            "COMMON_UNAUTHORIZED"           => "로그인이 필요합니다.",
            "AUTH_TOKEN_EXPIRED"            => "세션이 만료되었습니다. 다시 로그인해 주세요.",
            "AUTH_TOKEN_INVALID"            => "인증에 실패했습니다.",
            "USER_NOT_FOUND"                => "사용자 정보를 찾을 수 없습니다.",
            "WEAPON_NOT_FOUND"              => "존재하지 않는 무기입니다.",
            "WEAPON_PARENT_NOT_UNLOCKED"    => "상위 무기를 먼저 해금해 주세요.",
            "WEAPON_SHARDS_INSUFFICIENT"    => "기억의 파편이 부족합니다.",
            "WEAPON_NOT_UNLOCKED"           => "해금하지 않은 무기는 장착할 수 없어요.",
            "TALENT_POINTS_SUM_MISMATCH"    => "재능 분배 합이 맞지 않습니다.",  // 현재 백엔드 미사용 — 향후 정책 복귀 대비
            // Memory (#138/#142)
            "MEMORY_FRAME_NOT_FOUND"        => "존재하지 않는 기억 액자입니다.",
            "MEMORY_SLOT_INDEX_OUT_OF_RANGE" => "잘못된 슬롯 위치입니다.",
            "MEMORY_SHARDS_INSUFFICIENT"    => "기억의 파편이 부족합니다.",
            _                               => "알 수 없는 오류가 발생했습니다."
        };
    }
}
