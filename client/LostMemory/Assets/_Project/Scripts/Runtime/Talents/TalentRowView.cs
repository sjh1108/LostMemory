using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Talents
{
    /// <summary>
    /// 재능 목록의 한 행(Row) UI를 담당하는 뷰 컴포넌트.
    /// TalentPanel 안의 각 TalentRow GameObject에 부착한다.
    /// </summary>
    public class TalentRowView : MonoBehaviour
    {
        [SerializeField] private TalentType _talentType;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _valueText;
        [SerializeField] private Button _plusButton;
        [SerializeField] private Button _minusButton;

        private int _maxLevel;

        /// <summary>이 행이 담당하는 재능 종류 (Inspector에서 설정)</summary>
        public TalentType TalentType => _talentType;

        /// <summary>TalentPanelView에서 onClick 이벤트 등록에 사용</summary>
        public Button PlusButton => _plusButton;
        public Button MinusButton => _minusButton;

        /// <summary>표시 이름 초기화 (TalentData.DisplayName을 넘긴다)</summary>
        public void Init(string displayName, int maxLevel)
        {
            _nameText.text = displayName;
            _maxLevel = maxLevel;
        }

        /// <summary>투자 수치와 버튼 활성 상태를 최신 모델 상태로 갱신</summary>
        public void Refresh(int invested, bool canAdd, bool canRemove)
        {
            _valueText.text = $"{invested}/{_maxLevel}";
            _plusButton.interactable = canAdd;
            _minusButton.interactable = canRemove;
        }
    }
}
