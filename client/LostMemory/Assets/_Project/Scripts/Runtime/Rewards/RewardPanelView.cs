using System;
using System.Collections;
using System.Collections.Generic;
using LostMemory.Networking.Player;
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
        [Tooltip("패널이 열린 후 카드를 선택 가능해지기까지의 지연 시간 (초, realtime). 0 이면 즉시 선택 가능.")]
        [SerializeField, Min(0f)] private float _selectionDelay = 1f;

        /// <summary>CL-110: 카드 선택 후 발화. RewardController 가 구독하여 문 열기 등 후속 처리.</summary>
        public event Action<RelicData> RewardSelected;

        // CL-146 다중 픽 상태
        private int _picksMade;
        private int _picksAllowed = 1;

        // CL-147 타로 재추첨 — 다음 Show 호출 시 N회 재추첨 적용 후 마지막 결과 표시
        private int _pendingRerolls;

        // 선택 지연 코루틴 핸들 — 패널 닫힐 때 안전하게 stop.
        private Coroutine _enableSelectionCoroutine;

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
        /// (기존 호환 오버로드 — relicOnly=false, rarityBoostPercent=0)
        /// </summary>
        public void Show(PlayerRelicInventory inventory, int count, int picksAllowed,
                         int luckPoints, bool forceLegendary)
            => Show(inventory, count, picksAllowed, luckPoints, forceLegendary, relicOnly: false, rarityBoostPercent: 0f);

        /// <summary>
        /// 기억 시스템 '시작 유물 +N' 용: relicOnly=true 면 소모품 제외하고 유물만 추첨.
        /// (기존 호환 오버로드 — rarityBoostPercent=0)
        /// </summary>
        public void Show(PlayerRelicInventory inventory, int count, int picksAllowed,
                         int luckPoints, bool forceLegendary, bool relicOnly)
            => Show(inventory, count, picksAllowed, luckPoints, forceLegendary, relicOnly, rarityBoostPercent: 0f);

        /// <summary>
        /// 기억 시스템 RewardRarityBoost 보상 반영용: rarityBoostPercent &gt; 0 면 N 확률로 한 단계 상위 등급 카드로 교체.
        /// </summary>
        public void Show(PlayerRelicInventory inventory, int count, int picksAllowed,
                         int luckPoints, bool forceLegendary, bool relicOnly, float rarityBoostPercent)
        {
            // [DiagPhaseF] Log 6a — Show 진입 시점 + 활성 상태 (SetActive 호출 전).
            var nmLog6a = Unity.Netcode.NetworkManager.Singleton;
            Debug.Log($"[RewardPanelView] Show ENTER localId={nmLog6a?.LocalClientId} " +
                      $"inventory={(inventory != null ? inventory.gameObject.name : "NULL")} " +
                      $"count={count} picks={picksAllowed} " +
                      $"beforeActive={gameObject.activeSelf} hier={gameObject.activeInHierarchy}", this);

            // Phase E: inventory 인자가 null 인 경우 (호출자 wiring 실패 등) LocalPlayer 측에서 fallback.
            // RewardController 가 이미 LocalPlayerResolver 로 resolve 하므로 보통은 non-null 이지만 안전망.
            if (inventory == null)
            {
                inventory = LocalPlayerResolver.GetComponentOnLocalPlayer<PlayerRelicInventory>();
                Debug.LogWarning($"[RewardPanelView] Show: inventory 인자 null → LocalPlayerResolver fallback. resolved={(inventory != null ? inventory.gameObject.name : "still-null")}", this);
            }
            _inventory = inventory;
            _picksMade = 0;
            _picksAllowed = Mathf.Max(1, picksAllowed);

            count = Mathf.Clamp(count, 1, _cards != null ? _cards.Length : 1);

            var rewards = _rewardPool.DrawCount(
                count, inventory.GetOwnedNames(), luckPoints, forceLegendary, relicOnly, rarityBoostPercent);

            // CL-147: 타로 재추첨 적용 — 마지막 결과만 표시
            if (_pendingRerolls > 0)
            {
                int reroll = _pendingRerolls;
                _pendingRerolls = 0;
                for (int r = 0; r < reroll; r++)
                {
                    rewards = _rewardPool.DrawCount(
                        count, inventory.GetOwnedNames(), luckPoints, forceLegendary, relicOnly, rarityBoostPercent);
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

            // [DiagPhaseF] Log 6b — SetActive(true) 직후 hierarchy 실제 active 상태. hier=false 면 부모 Canvas/parent 비활성 의심.
            var nmLog6b = Unity.Netcode.NetworkManager.Singleton;
            Debug.Log($"[RewardPanelView] Show EXIT — SetActive(true) called. " +
                      $"afterActive={gameObject.activeSelf} hier={gameObject.activeInHierarchy} " +
                      $"localId={nmLog6b?.LocalClientId}", this);

            // 즉시 선택 방지 — 활성 카드 비활성화 후 _selectionDelay 뒤 다시 활성화.
            if (_selectionDelay > 0f)
            {
                for (int i = 0; i < _cards.Length; i++)
                {
                    if (_cards[i] != null && _cards[i].gameObject.activeSelf)
                        _cards[i].SetInteractable(false);
                }
                if (_enableSelectionCoroutine != null) StopCoroutine(_enableSelectionCoroutine);
                _enableSelectionCoroutine = StartCoroutine(EnableSelectionAfterDelay());
            }

            Debug.Log($"[RewardPanel] Show — count={count}, picksAllowed={_picksAllowed}, luckPoints={luckPoints}, forceLegendary={forceLegendary}");
        }

        private IEnumerator EnableSelectionAfterDelay()
        {
            yield return new WaitForSecondsRealtime(_selectionDelay);
            for (int i = 0; i < _cards.Length; i++)
            {
                if (_cards[i] != null && _cards[i].gameObject.activeSelf)
                    _cards[i].SetInteractable(true);
            }
            _enableSelectionCoroutine = null;
        }

        private void OnDisable()
        {
            // 다중 픽 도중 패널이 닫히거나 씬 전환 시 코루틴 누수 방지.
            if (_enableSelectionCoroutine != null)
            {
                StopCoroutine(_enableSelectionCoroutine);
                _enableSelectionCoroutine = null;
            }
        }

        private void OnCardSelected(RelicData selected)
        {
            // Phase C 검증 로그 — 멀티에서 본인 인벤토리에 정확히 추가되는지 확인.
            // host/guest 각자 자기 클릭에 대해서만 자기 PlayerRelicInventory.TryAdd 호출되어야 정상.
            string inventoryHostName = _inventory != null ? _inventory.gameObject.name : "null";
            Debug.Log($"[RewardPanelView] OnCardSelected '{selected?.DisplayName}' → TryAdd on inventory host='{inventoryHostName}'", this);

            // Phase E: _inventory 가 어떤 이유로 null 이면 LocalPlayer 측 fallback.
            if (_inventory == null)
            {
                _inventory = LocalPlayerResolver.GetComponentOnLocalPlayer<PlayerRelicInventory>();
                Debug.LogWarning($"[RewardPanelView] OnCardSelected fallback resolve → {(_inventory != null ? _inventory.gameObject.name : "still-null")}", this);
                if (_inventory == null) return;
            }
            _inventory.TryAdd(selected);
            _picksMade++;

            // CL-152 fix: SetActive / DisableSelectedCard 를 RewardSelected.Invoke *전* 에 처리.
            // RewardController.HandleRewardSelected 가 rewardPanelView.gameObject.activeSelf 로
            // "패널 닫혔는지" 판단하므로, 닫는 작업이 먼저 완료되어야 timeScale 복구 정확히 분기.
            // (CL-146 검증 시 picksAllowed=1 케이스 누락 — 이번에 발견)
            bool willClose = _picksMade >= _picksAllowed;
            if (willClose)
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
