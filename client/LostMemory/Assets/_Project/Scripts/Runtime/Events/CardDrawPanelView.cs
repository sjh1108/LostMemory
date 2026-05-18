using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Events
{
    /// <summary>
    /// CL-227 V2 — 카드 뽑기 패널.
    /// 3장 카드에 셔플된 outcome 배정. 픽 시 자신 + 나머지 카드도 reveal.
    /// 골드 차감/입금은 OnCardPicked 이벤트 받은 controller 가 처리.
    /// </summary>
    public sealed class CardDrawPanelView : MonoBehaviour
    {
        [Header("슬롯 (씬에 미리 배치)")]
        [SerializeField] private CardDrawCardView[] cardViews;

        [Header("HUD")]
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI infoText;       // "100 G 를 걸고 카드 1장 선택" / "골드 부족" 등
        [SerializeField] private TextMeshProUGUI resultText;     // 픽 후 결과 토스트

        [Header("Buttons")]
        [SerializeField] private Button skipButton;
        [Tooltip("X 버튼 — 픽 전/후 어디서든 패널 닫기. ESC 와 동일 동작.")]
        [SerializeField] private Button closeButton;

        public event Action<CardDrawOutcomeEntry> OnCardPicked;
        public event Action OnSkipPressed;
        public event Action OnClosePressed;

        private CardDrawConfig _config;
        private int _gold;
        private CardDrawOutcomeEntry[] _shuffledOutcomes;   // 매 Init 시 셔플
        private bool _picked;

        private void Awake()
        {
            if (skipButton != null)
            {
                skipButton.onClick.RemoveAllListeners();
                skipButton.onClick.AddListener(() => OnSkipPressed?.Invoke());
            }
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(() => OnClosePressed?.Invoke());
            }
        }

        public void Init(CardDrawConfig config, int gold)
        {
            _config = config;
            _gold = gold;
            _picked = false;

            // 3 outcomes 고정 + 위치만 셔플 (Fisher-Yates)
            _shuffledOutcomes = config.GetAllOutcomes();
            ShuffleInPlace(_shuffledOutcomes);

            int slotCount = Mathf.Min(cardViews.Length, _shuffledOutcomes.Length);
            for (int i = 0; i < cardViews.Length; i++)
            {
                if (cardViews[i] == null) continue;

                if (i < slotCount)
                {
                    cardViews[i].gameObject.SetActive(true);
                    int captured = i;
                    cardViews[i].Init(config.EntryCost, _ => HandleCardClicked(captured));
                }
                else
                {
                    cardViews[i].gameObject.SetActive(false);
                }
            }

            if (resultText != null) resultText.text = "";

            UpdateGold(gold);
        }

        public void UpdateGold(int gold)
        {
            _gold = gold;
            if (goldText != null) goldText.text = $"보유 골드: {gold:N0}";
            RefreshAffordability();
            UpdateInfoText();
        }

        private void UpdateInfoText()
        {
            if (infoText == null || _config == null) return;
            if (_picked)
            {
                infoText.text = "결과 확인 — X 또는 ESC 로 닫기";
            }
            else if (_gold < _config.EntryCost)
            {
                infoText.text = $"골드 부족 ({_gold}/{_config.EntryCost}) — 건너뛰기만 가능";
            }
            else
            {
                infoText.text = $"{_config.EntryCost}G 를 걸고 카드 1장 선택";
            }
        }

        private void HandleCardClicked(int index)
        {
            if (_picked) return;
            if (_config == null || _shuffledOutcomes == null) return;
            if (index < 0 || index >= _shuffledOutcomes.Length) return;
            if (_gold < _config.EntryCost) return;
            if (cardViews[index] == null) return;

            _picked = true;
            CardDrawOutcomeEntry pickedOutcome = _shuffledOutcomes[index];

            // 1) 선택한 카드 reveal
            cardViews[index].RevealOutcome(pickedOutcome);

            // 2) 나머지 카드도 reveal (선택지 공개 — 도박 긴장감)
            for (int i = 0; i < cardViews.Length; i++)
            {
                if (i == index) continue;
                if (cardViews[i] == null || !cardViews[i].gameObject.activeSelf) continue;
                if (i < _shuffledOutcomes.Length) cardViews[i].RevealOutcome(_shuffledOutcomes[i]);
            }

            // 3) 결과 토스트
            if (resultText != null)
            {
                int net = pickedOutcome.ReturnGold - _config.EntryCost;
                string netStr = net >= 0 ? $"+{net}" : net.ToString();
                resultText.text = $"{pickedOutcome.DisplayLabel}  (Net {netStr}G)";
                resultText.color = pickedOutcome.LabelColor;
            }

            UpdateInfoText();

            // 4) controller 통보 — GoldWallet 처리
            OnCardPicked?.Invoke(pickedOutcome);
        }

        private void RefreshAffordability()
        {
            if (_config == null || cardViews == null) return;
            bool canAfford = !_picked && _gold >= _config.EntryCost;
            for (int i = 0; i < cardViews.Length; i++)
            {
                if (cardViews[i] == null || !cardViews[i].gameObject.activeSelf) continue;
                cardViews[i].SetAffordable(canAfford);
            }
        }

        private static void ShuffleInPlace<T>(T[] arr)
        {
            for (int i = arr.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
        }
    }
}
