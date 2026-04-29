using System;
using LostMemory.Relics;
using UnityEngine;

namespace LostMemory.Rewards
{
    /// <summary>
    /// 보상 3택 패널 전체를 담당하는 뷰 컴포넌트.
    /// Show()를 호출하면 RewardPool에서 3개를 추첨해 카드에 표시한다.
    /// 카드 선택 시 PlayerRelicInventory에 추가하고 패널을 닫는다.
    /// </summary>
    public class RewardPanelView : MonoBehaviour
    {
        [SerializeField] private RewardPool _rewardPool;
        [SerializeField] private PlayerRelicInventory _inventory;
        [SerializeField] private RewardCardView[] _cards;

        /// <summary>CL-110: 카드 선택 후 발화. RewardController 가 구독하여 문 열기 등 후속 처리.</summary>
        public event Action<RelicData> RewardSelected;

        /// <summary>
        /// 보상 패널을 열고 카드 3장을 추첨해 표시한다.
        /// </summary>
        /// <param name="inventory">현재 플레이어 인벤토리 (보유 유물 중복 제외용)</param>
        public void Show(PlayerRelicInventory inventory)
        {
            _inventory = inventory;

            var rewards = _rewardPool.DrawThree(inventory.GetOwnedNames());

            for (int i = 0; i < _cards.Length; i++)
            {
                if (i < rewards.Count)
                {
                    _cards[i].gameObject.SetActive(true);
                    _cards[i].Init(rewards[i], OnCardSelected);
                }
                else
                {
                    _cards[i].gameObject.SetActive(false);
                }
            }

            gameObject.SetActive(true);
        }

        private void OnCardSelected(RelicData selected)
        {
            _inventory.TryAdd(selected);
            gameObject.SetActive(false);
            // CL-110: RewardController 등 외부 구독자에게 선택 완료 알림.
            RewardSelected?.Invoke(selected);
        }
    }
}
