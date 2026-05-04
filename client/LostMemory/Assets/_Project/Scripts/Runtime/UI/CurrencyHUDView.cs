using LostMemory.Player;
using TMPro;
using UnityEngine;

namespace LostMemory.UI
{
    /// <summary>
    /// 우하단 재화 표시 패널을 담당하는 뷰 컴포넌트.
    /// PlayerWallet 이벤트를 구독해 골드 / 기억의 파편 텍스트를 자동으로 갱신한다.
    /// </summary>
    [AddComponentMenu("Lost Memory/UI/Currency HUD View")]
    public class CurrencyHUDView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _goldText;
        [SerializeField] private TextMeshProUGUI _memoryFragmentsText;

        private void Start()
        {
            if (PlayerWallet.Instance != null)
            {
                PlayerWallet.Instance.OnGoldChanged            += RefreshGold;
                PlayerWallet.Instance.OnMemoryFragmentsChanged += RefreshMemoryFragments;

                // 현재 값으로 즉시 갱신
                RefreshGold(PlayerWallet.Instance.Gold);
                RefreshMemoryFragments(PlayerWallet.Instance.MemoryFragments);
            }
            else
            {
                RefreshGold(0);
                RefreshMemoryFragments(0);
                Debug.LogWarning("[CurrencyHUDView] PlayerWallet.Instance가 없습니다. " +
                                 "씬에 PlayerWallet 오브젝트를 배치했는지 확인하세요.");
            }
        }

        private void OnDestroy()
        {
            if (PlayerWallet.Instance != null)
            {
                PlayerWallet.Instance.OnGoldChanged            -= RefreshGold;
                PlayerWallet.Instance.OnMemoryFragmentsChanged -= RefreshMemoryFragments;
            }
        }

        private void RefreshGold(int amount)
        {
            if (_goldText != null)
                _goldText.text = amount.ToString("N0");
        }

        private void RefreshMemoryFragments(int amount)
        {
            if (_memoryFragmentsText != null)
                _memoryFragmentsText.text = amount.ToString();
        }
    }
}
