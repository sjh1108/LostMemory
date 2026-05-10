using TMPro;
using UnityEngine;

namespace LostMemory.UI
{
    /// <summary>
    /// 우하단 재화 표시 패널의 텍스트/영역 표시만 담당하는 뷰 컴포넌트.
    /// 데이터 소스 연결은 던전/마을 Presenter 가 담당한다.
    /// </summary>
    [AddComponentMenu("Lost Memory/UI/Currency HUD View")]
    public class CurrencyHUDView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _goldText;
        [SerializeField] private TextMeshProUGUI _memoryFragmentsText;
        [SerializeField] private GameObject _goldRoot;
        [SerializeField] private GameObject _memoryFragmentsRoot;

        public void SetGold(int amount)
        {
            if (_goldText != null)
            {
                _goldText.text = amount.ToString("N0");
            }
        }

        public void SetMemoryFragments(int amount)
        {
            if (_memoryFragmentsText != null)
            {
                _memoryFragmentsText.text = amount.ToString("N0");
            }
        }

        public void SetGoldVisible(bool visible)
        {
            SetRootVisible(ref _goldRoot, _goldText, "GoldArea", visible);
        }

        public void SetMemoryFragmentsVisible(bool visible)
        {
            SetRootVisible(ref _memoryFragmentsRoot, _memoryFragmentsText, "MemoryFragmentsArea", visible);
        }

        private void SetRootVisible(ref GameObject root, Component textComponent, string preferredName, bool visible)
        {
            if (root == null)
            {
                root = ResolveRoot(textComponent, preferredName);
            }

            if (root != null)
            {
                root.SetActive(visible);
            }
        }

        private GameObject ResolveRoot(Component textComponent, string preferredName)
        {
            if (textComponent == null)
            {
                return null;
            }

            Transform cursor = textComponent.transform;
            while (cursor != null && cursor != transform)
            {
                if (cursor.name == preferredName)
                {
                    return cursor.gameObject;
                }

                cursor = cursor.parent;
            }

            return textComponent.transform.parent != null
                ? textComponent.transform.parent.gameObject
                : textComponent.gameObject;
        }
    }
}
