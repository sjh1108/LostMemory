using System;
using System.Collections.Generic;
using LostMemory.Relics;
using UnityEngine;

namespace LostMemory.Rewards
{
    /// <summary>
    /// 보상 패널 뷰 — RewardController 가 Show 호출 시 RewardPool 에서 추첨.
    ///
    /// CL-146 (Phase 3 마무리) 변경:
    /// - 카드 배열 동적 (3장 또는 5장) — `_cards` 인스펙터 5장 등록 권장
    /// - `Show(inventory, count, picksAllowed, luckPoints, forceLegendary)` 시그니처 확장
    /// - 다중 픽 처리: picksAllowed 도달 시 패널 닫음, 그 전엔 선택된 카드만 disable
    /// - LuckPoints 가중치 + ForceLegendary RewardPool 옵션 전달
    ///
    /// 호환: 기존 `Show(inventory)` 시그니처 유지 → count=3, picksAllowed=1 default.
    /// </summary>
    public class RewardPanelView : MonoBehaviour
    {
        [SerializeField] private RewardPool _rewardPool;
        [SerializeField] private PlayerRelicInventory _inventory;
        [Tooltip("카드 슬롯 배열 — 5장 권장 (행운 5스택 시 5장 표시).")]
        [SerializeField] private RewardCardView[] _cards;

        /// <summary>CL-110: 카드 선택 후 발화. RewardController 가 구독하여 문 열기 등 후속 처리.</summary>
        public event Action<RelicData> RewardSelected;

        // CL-146 다중 픽 상태
        private int _picksMade;
        private int _picksAllowed = 1;

        // CL-147 타로 재추첨 — 다음 Show 호출 시 N회 재추첨 적용 후 마지막 결과 표시
        private int _pendingRerolls;

        /// <summary>CL-147: 타로 RerollCard 가 호출. 다음 Show 시 N회 추가 재추첨.</summary>
        public void RequestReroll(int additionalDraws)
        {
            _pendingRerolls += Mathf.Max(0, additionalDraws);
            Debug.Log($"[RewardPanel] 재추첨 예약 누적 = {_pendingRerolls}");
        }

        /// <summary>
        /// CL-110 호환: 기존 3장 / 1픽 시그니처. 내부적으로 확장 시그니처 호출.
        /// </summary>
        public void Show(PlayerRelicInventory inventory)
            => Show(inventory, count: 3, picksAllowed: 1, luckPoints: 0, forceLegendary: false);

        /// <summary>
        /// CL-146: 보상 패널 열기. 행운 set tier 에 따라 5장 + 2픽 + 가중치 + forceLegendary.
        /// </summary>
        public void Show(PlayerRelicInventory inventory, int count, int picksAllowed,
                         int luckPoints, bool forceLegendary)
        {
            _inventory = inventory;
            _picksMade = 0;
            _picksAllowed = Mathf.Max(1, picksAllowed);

            count = Mathf.Clamp(count, 1, _cards != null ? _cards.Length : 1);

            var rewards = _rewardPool.DrawCount(
                count, inventory.GetOwnedNames(), luckPoints, forceLegendary);

            // CL-147: 타로 재추첨 적용 — 마지막 결과만 표시
            if (_pendingRerolls > 0)
            {
                int reroll = _pendingRerolls;
                _pendingRerolls = 0;
                for (int r = 0; r < reroll; r++)
                {
                    rewards = _rewardPool.DrawCount(
                        count, inventory.GetOwnedNames(), luckPoints, forceLegendary);
                }
                Debug.Log($"[RewardPanel] {reroll}회 재추첨 적용 — 최종 결과만 표시");
            }

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
            Debug.Log($"[RewardPanel] Show — count={count}, picksAllowed={_picksAllowed}, luckPoints={luckPoints}, forceLegendary={forceLegendary}");
        }

        private void OnCardSelected(RelicData selected)
        {
            _inventory.TryAdd(selected);
            _picksMade++;

            if (_picksMade >= _picksAllowed)
            {
                gameObject.SetActive(false);
                RewardSelected?.Invoke(selected);
                return;
            }

            // 다중 픽 모드 — 선택된 카드만 비활성, 나머지 선택 대기
            DisableSelectedCard(selected);
            RewardSelected?.Invoke(selected);
            Debug.Log($"[RewardPanel] Pick {_picksMade}/{_picksAllowed} — {selected?.DisplayName} 선택됨, 추가 선택 대기");
        }

        private void DisableSelectedCard(RelicData selected)
        {
            for (int i = 0; i < _cards.Length; i++)
            {
                if (_cards[i] == null) continue;
                if (!_cards[i].gameObject.activeSelf) continue;
                if (_cards[i].Data == selected)
                {
                    _cards[i].gameObject.SetActive(false);
                    break;
                }
            }
        }
    }
}
