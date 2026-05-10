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
        [SerializeField] private GameObject _mpRoot;

        /// <summary>HP 바와 텍스트를 갱신한다.</summary>
        public void UpdateHP(int current, int max)
        {
            UpdateResource(_hpFill, _hpText, current, max);
        }

        /// <summary>MP 바와 텍스트를 갱신한다.</summary>
        public void UpdateMP(int current, int max)
        {
            UpdateResource(_mpFill, _mpText, current, max);
        }

        public void SetMPVisible(bool visible)
        {
            ResolveMPRoot();

            if (_mpRoot != null)
            {
                _mpRoot.SetActive(visible);
                return;
            }

            if (_mpFill != null)
            {
                _mpFill.gameObject.SetActive(visible);
            }

            if (_mpText != null)
            {
                _mpText.gameObject.SetActive(visible);
            }
        }

        private void UpdateResource(Image fill, TextMeshProUGUI text, int current, int max)
        {
            int normalizedMax = Mathf.Max(0, max);
            int normalizedCurrent = Mathf.Clamp(current, 0, normalizedMax);

            if (fill != null)
            {
                fill.fillAmount = normalizedMax > 0 ? (float)normalizedCurrent / normalizedMax : 0f;
            }

            if (text != null)
            {
                text.text = normalizedMax > 0 ? $"{normalizedCurrent}/{normalizedMax}" : "--/--";
            }
        }

        private void ResolveMPRoot()
        {
            if (_mpRoot != null || _mpText == null)
            {
                return;
            }

            Transform cursor = _mpText.transform;
            while (cursor != null && cursor != transform)
            {
                if (cursor.name == "MPRow")
                {
                    _mpRoot = cursor.gameObject;
                    return;
                }

                cursor = cursor.parent;
            }

            if (_mpText.transform.parent != null)
            {
                _mpRoot = _mpText.transform.parent.gameObject;
            }
        }
    }
}
