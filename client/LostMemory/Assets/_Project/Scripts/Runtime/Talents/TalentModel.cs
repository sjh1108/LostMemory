using System.Collections.Generic;
using System.Linq;
using LostMemory.Data;

namespace LostMemory.Talents
{
    /// <summary>
    /// 한 런(run) 동안의 재능 포인트 분배 상태를 관리하는 로컬 모델.
    /// ScriptableObject가 아닌 순수 C# 클래스 — new TalentModel()로 생성하고 런 종료 시 폐기한다.
    /// </summary>
    public class TalentModel
    {
        // 재능 종류 -> 설정값 (TalentData) 연결표
        private readonly Dictionary<TalentType, TalentData> _dataMap;
        // 재능 종류 -> 현재 투자 포인트
        private readonly Dictionary<TalentType, int> _invested;
        // 남은 포인트
        private int _remainingPoints;

        /// <summary>현재 남은 투자 가능 포인트</summary>
        public int RemainingPoints => _remainingPoints;

        /// <param name="talentDatas">재능 5종의 설정 데이터 (ScriptableObject)</param>
        /// <param name="totalPoints">이번 런에서 사용할 수 있는 총 포인트</param>
        /// <param name="savedInvestments">저장된 투자값 (TalentSaveService.LoadInvested()). null이면 전부 0으로 초기화</param>
        public TalentModel(IEnumerable<TalentData> talentDatas, int totalPoints,
            Dictionary<TalentType, int> savedInvestments = null)
        {
            _dataMap = new Dictionary<TalentType, TalentData>();
            _invested = new Dictionary<TalentType, int>();

            foreach (var data in talentDatas)
            {
                _dataMap[data.TalentType] = data;
                var initial = savedInvestments != null && savedInvestments.TryGetValue(data.TalentType, out var v) ? v : 0;
                _invested[data.TalentType] = initial;
            }

            // 저장된 값이 있으면 이미 쓴 포인트를 차감
            var spent = 0;
            foreach (var v in _invested.Values) spent += v;
            _remainingPoints = totalPoints - spent;
        }

        /// <summary>특정 재능에 현재 투자된 포인트 반환</summary>
        public int GetInvested(TalentType type)
        {
            return _invested.TryGetValue(type, out var val) ? val : 0;
        }

        /// <summary>포인트를 1 추가할 수 있는지 검사 (포인트 부족 또는 최대치 도달 시 false)</summary>
        public bool CanAdd(TalentType type)
        {
            if (_remainingPoints <= 0) return false;
            if (!_dataMap.TryGetValue(type, out var data)) return false;
            return _invested[type] < data.MaxLevel;
        }

        /// <summary>포인트를 1 추가. 성공하면 true, 실패하면 false 반환</summary>
        public bool TryAdd(TalentType type)
        {
            if (!CanAdd(type)) return false;
            _invested[type]++;
            _remainingPoints--;
            return true;
        }

        /// <summary>포인트를 1 회수할 수 있는지 검사 (투자된 값이 0이면 false)</summary>
        public bool CanRemove(TalentType type)
        {
            return _invested.TryGetValue(type, out var val) && val > 0;
        }

        /// <summary>포인트를 1 회수. 성공하면 true, 실패하면 false 반환</summary>
        public bool TryRemove(TalentType type)
        {
            if (!CanRemove(type)) return false;
            _invested[type]--;
            _remainingPoints++;
            return true;
        }

        /// <summary>모든 투자를 초기화하고 포인트를 전액 환불</summary>
        public void Reset()
        {
            // ToList()로 키를 미리 복사해야 순회 중 Dictionary 수정 예외를 피할 수 있다.
            foreach (var type in _invested.Keys.ToList())
            {
                _remainingPoints += _invested[type];
                _invested[type] = 0;
            }
        }
    }
}
