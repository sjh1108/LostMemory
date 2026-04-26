using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.UI
{
    /// <summary>
    /// HP / MP 바 UI를 업데이트하는 뷰 컴포넌트.
    /// PlayerHUD GameObject에 부착한다.
    /// </summary>
    public class HealthBarView : MonoBehaviour
    {
        [Header("HP")]
        [SerializeField] private Image _hpFill;
        [SerializeField] private TextMeshProUGUI _hpText;

        [Header("MP")]
        [SerializeField] private Image _mpFill;
        [SerializeField] private TextMeshProUGUI _mpText;

        /// <summary>HP 바와 텍스트를 갱신한다.</summary>
        public void UpdateHP(int current, int max)
        {
            _hpFill.fillAmount = (float)current / max;
            _hpText.text = $"{current}/{max}";
        }

        /// <summary>MP 바와 텍스트를 갱신한다.</summary>
        public void UpdateMP(int current, int max)
        {
            _mpFill.fillAmount = (float)current / max;
            _mpText.text = $"{current}/{max}";
        }
    }
}
