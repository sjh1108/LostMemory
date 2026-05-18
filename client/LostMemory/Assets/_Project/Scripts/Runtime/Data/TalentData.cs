using UnityEngine;
using LostMemory.Talents;

namespace LostMemory.Data
{
    /// <summary>
    /// 재능 한 종류의 설정값을 담는 ScriptableObject
    /// Assets/_Project/ScriptableObjects/Talents/ 아래에 .asset 파일로 저장한다.
    /// </summary>
    [CreateAssetMenu(fileName = "TalentData_New", menuName = "LostMemory/Talents/TalentData")]
    public class TalentData : ScriptableObject
    {
        [SerializeField] private TalentType _talentType;
        [SerializeField] private string _displayName;
        [SerializeField] private int _maxLevel = 20;
        [SerializeField][Min(0f)] private float _increasePerPoint = 1f;

        /// <summary>재능 종류 식별자</summary>
        public TalentType TalentType => _talentType;

        /// <summary>UI에 표시할 한글 이름</summary>
        public string DisplayName => _displayName;

        /// <summary>최대 투자 가능 레벨 (기본 20, 밸런스 패치 시 조정)</summary>
        public int MaxLevel => _maxLevel;

        /// <summary>포인트 1당 스탯 상승량 (밸런스 패치 시 조정)</summary>
        public float IncreasePerPoint => _increasePerPoint;
    }
}
