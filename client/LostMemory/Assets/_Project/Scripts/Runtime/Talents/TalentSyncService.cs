using System.Collections.Generic;
using System.Threading.Tasks;
using LostMemory.Data;
using LostMemory.Networking.Common;
using LostMemory.Networking.Session;

namespace LostMemory.Talents
{
    /// <summary>
    /// 백엔드 user_talent_allocations 와 클라 TalentModel 양방향 매핑.
    ///
    /// 정책: 재능 slot 4개만 매핑 (CriticalRate / AttackSpeed / Defense / MaxHealth).
    ///   - 백엔드는 4 영역 값만 저장/조회. 총량 / 잔여는 백엔드 미관리.
    ///   - MoveSpeed 는 재능 분배에서 빠짐 — TalentType.MoveSpeed enum 자체는 인게임 이동속도 스탯에 사용 (24+ 파일).
    ///
    /// 호출 흐름:
    ///   - 마을 진입 시 1회: <see cref="PullAsync"/> → 서버 값 + 클라 보유 총량으로 새 TalentModel 인스턴스 반환
    ///   - 장착 버튼 클릭: <see cref="PushAsync"/> → 모델의 현재 4 slot 값 통째로 서버 저장
    /// </summary>
    public static class TalentSyncService
    {
        /// <summary>
        /// 마을 진입 시 1회. 서버의 4 slot 값을 받아 새 TalentModel 을 생성해 반환.
        /// 실패 시 null 반환 — 호출자가 fallback (예: PlayerPrefs 또는 모두 0) 처리.
        /// </summary>
        /// <param name="talentDatas">재능 4종 설정 데이터 (ScriptableObject 목록)</param>
        /// <param name="totalPoints">클라 측 보유 총량 (회원가입 직후 5 + 액자 보너스 누적)</param>
        public static async Task<TalentModel> PullAsync(IEnumerable<TalentData> talentDatas, int totalPoints)
        {
            var data = await SessionApiClient.GetMyTalentsAsync();
            if (data == null)
            {
                NetLog.Warn("Talent", "PullAsync 실패 — null 반환");
                return null;
            }

            var saved = new Dictionary<TalentType, int>
            {
                [TalentType.CriticalRate] = data.critRatePoints,
                [TalentType.AttackSpeed]  = data.attackSpeedPoints,
                [TalentType.Defense]      = data.defensePoints,
                [TalentType.MaxHealth]    = data.maxHpPoints,
                // TalentType.MoveSpeed 는 서버 미저장 — 0 으로 시작 (TalentModel 생성자가 기본 0 처리)
            };

            NetLog.Info("Talent", $"PullAsync OK — crit={data.critRatePoints}, atk={data.attackSpeedPoints}, "
                                  + $"def={data.defensePoints}, hp={data.maxHpPoints}");
            return new TalentModel(talentDatas, totalPoints, saved);
        }

        /// <summary>
        /// 장착 버튼 클릭 시 — 모델의 현재 4 slot 값을 서버에 통째로 저장.
        /// 성공 시 true. 실패 시 false (호출자가 토스트 등 처리).
        /// </summary>
        public static async Task<bool> PushAsync(TalentModel model)
        {
            if (model == null) return false;

            var body = new SessionApiClient.TalentSaveBody
            {
                critRatePoints     = model.GetInvested(TalentType.CriticalRate),
                attackSpeedPoints  = model.GetInvested(TalentType.AttackSpeed),
                defensePoints      = model.GetInvested(TalentType.Defense),
                maxHpPoints        = model.GetInvested(TalentType.MaxHealth),
            };

            var data = await SessionApiClient.SaveTalentsAsync(body);
            if (data == null)
            {
                NetLog.Warn("Talent", "PushAsync 실패");
                return false;
            }

            NetLog.Info("Talent", $"PushAsync OK — crit={body.critRatePoints}, atk={body.attackSpeedPoints}, "
                                  + $"def={body.defensePoints}, hp={body.maxHpPoints}");
            return true;
        }
    }
}
