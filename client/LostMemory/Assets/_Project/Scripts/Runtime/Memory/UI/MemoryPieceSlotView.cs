using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Memory.UI
{
    /// <summary>
    /// 해금 패널에서 조각(Piece) 1개를 나타내는 슬롯 뷰.
    /// MemoryUnlockPanelView 가 캔버스 수만큼 배열로 관리한다.
    ///
    /// Inspector 연결 항목:
    ///   _icon          — 조각 아이콘 Image
    ///   _nameText      — 조각 이름
    ///   _costText      — "파편 N개" 해금 비용
    ///   _rewardText    — 보상 설명 (RewardType 기반 자동 생성)
    ///   _unlockButton  — 해금 버튼 (파편 부족 시 interactable=false)
    ///   _emptyLabel    — 캔버스 조각 전부 해금 시 표시할 "완성" 오브젝트
    /// </summary>
    public sealed class MemoryPieceSlotView : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _costText;
        [SerializeField] private TextMeshProUGUI _rewardText;
        [SerializeField] private Button _unlockButton;
        [SerializeField] private GameObject _emptyLabel;

        private Action<MemoryFragmentData> _onUnlock;
        private MemoryFragmentData _piece;

        /// <summary>
        /// 해금 가능한 조각이 있을 때 슬롯을 초기화한다.
        /// </summary>
        /// <param name="piece">표시할 조각 데이터</param>
        /// <param name="canUnlock">현재 파편이 충분한지 여부 (버튼 활성 결정)</param>
        /// <param name="onUnlock">해금 버튼 클릭 시 호출할 콜백</param>
        public void Init(MemoryFragmentData piece, bool canUnlock, Action<MemoryFragmentData> onUnlock)
        {
            _piece = piece;
            _onUnlock = onUnlock;

            if (_emptyLabel != null) _emptyLabel.SetActive(false);
            gameObject.SetActive(true);

            if (_icon != null)
            {
                _icon.sprite = piece.Icon;
                _icon.enabled = piece.Icon != null;
            }

            if (_nameText != null) _nameText.text = piece.DisplayName;
            if (_costText != null) _costText.text = $"파편 {piece.ShardCost}개";
            if (_rewardText != null) _rewardText.text = BuildRewardText(piece);

            if (_unlockButton != null)
            {
                _unlockButton.interactable = canUnlock;
                _unlockButton.onClick.RemoveAllListeners();
                _unlockButton.onClick.AddListener(OnUnlockClicked);
            }
        }

        /// <summary>캔버스의 조각이 모두 해금되어 표시할 조각이 없을 때 호출.</summary>
        public void SetEmpty(string canvasName)
        {
            _piece = null;
            if (_emptyLabel != null)
            {
                _emptyLabel.SetActive(true);
                var label = _emptyLabel.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = $"{canvasName} 완성!";
            }
            if (_unlockButton != null) _unlockButton.interactable = false;
        }

        private void OnUnlockClicked()
        {
            if (_piece != null) _onUnlock?.Invoke(_piece);
        }

        private static string BuildRewardText(MemoryFragmentData piece)
        {
            return piece.RewardType switch
            {
                MemoryPieceRewardType.StatBoost        => $"{piece.RewardStat} +{piece.RewardMagnitude * 100f:F0}%",
                MemoryPieceRewardType.RelicSlotExpand  => $"유물 슬롯 +{(int)piece.RewardMagnitude}",
                MemoryPieceRewardType.StartingGold     => $"시작 골드 +{(int)piece.RewardMagnitude}",
                MemoryPieceRewardType.ShardDropBonus   => $"런당 파편 +{(int)piece.RewardMagnitude}",
                MemoryPieceRewardType.RewardSlotExpand => $"보상 선택지 +{(int)piece.RewardMagnitude}",
                MemoryPieceRewardType.ShopSlotExpand   => $"상점 슬롯 +{(int)piece.RewardMagnitude}",
                MemoryPieceRewardType.RewardRarityBoost=> $"보상 등급 확률 +{piece.RewardMagnitude * 100f:F0}%",
                MemoryPieceRewardType.ReviveOnce       => "부활 1회 해금",
                MemoryPieceRewardType.RoomSkip         => "방 건너뛰기 해금",
                MemoryPieceRewardType.RunStartRelic    => "시작 유물 1개 해금",
                _                                      => string.Empty,
            };
        }
    }
}
