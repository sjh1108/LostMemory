using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Memory.UI
{
    /// <summary>
    /// 해금 패널에서 조각(Piece) 1개를 나타내는 슬롯 뷰.
    /// 슬롯 GameObject 자체에 Button 컴포넌트가 붙어있으며,
    /// 클릭 시 해금 요청 콜백을 호출한다.
    ///
    /// Inspector 연결 항목:
    ///   _background  — 슬롯 배경 Image (해금 상태에 따라 색상 변경)
    ///   _lockIcon    — 잠금 아이콘 (잠금 상태일 때만 표시)
    ///   _icon        — 조각 아이콘 Image
    ///   _costText    — "파편 N개" 해금 비용
    ///   _rewardText  — 보상 설명 (RewardType 기반 자동 생성)
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class MemoryPieceSlotView : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Image _background;
        [SerializeField] private Image _lockIcon;
        [SerializeField] private Image _icon;
        [SerializeField] private GameObject _costArea;
        [SerializeField] private TextMeshProUGUI _costText;
        [SerializeField] private TextMeshProUGUI _rewardText;

        [Header("색상")]
        [SerializeField] private Color _unlockedColor   = Color.white;
        [SerializeField] private Color _unlockableColor = new Color(1f, 1f, 0.7f, 1f);
        [SerializeField] private Color _lockedColor     = new Color(0.4f, 0.4f, 0.4f, 1f);

        private Action<MemoryFragmentData> _onUnlock;
        private MemoryFragmentData _piece;
        private Button _button;

        private void Awake()
        {
            EnsureButton();
        }

        private void EnsureButton()
        {
            if (_button != null) return;
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnSlotClicked);
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnSlotClicked);
        }

        // ── 공개 API ──────────────────────────────────────────

        /// <summary>이미 해금된 조각을 표시한다. 클릭해도 반응 없음.</summary>
        public void InitUnlocked(MemoryFragmentData piece)
        {
            EnsureButton();
            _piece    = piece;
            _onUnlock = null;
            ShowContent(piece);
            SetVisualState(SlotState.Unlocked);
            _button.interactable = false;
        }

        /// <summary>잠긴 조각을 표시한다. canUnlock 에 따라 클릭 가능 여부 결정.</summary>
        public void InitLocked(MemoryFragmentData piece, bool canUnlock,
                               Action<MemoryFragmentData> onUnlock)
        {
            EnsureButton();
            _piece    = piece;
            _onUnlock = onUnlock;
            ShowContent(piece);
            SetVisualState(canUnlock ? SlotState.Unlockable : SlotState.Locked);
            _button.interactable = canUnlock;
        }

        /// <summary>기존 호환용. InitLocked 와 동일.</summary>
        public void Init(MemoryFragmentData piece, bool canUnlock,
                         Action<MemoryFragmentData> onUnlock)
            => InitLocked(piece, canUnlock, onUnlock);

        /// <summary>슬롯에 표시할 조각 데이터가 없을 때 호출.</summary>
        public void SetEmpty()
        {
            EnsureButton();
            _piece    = null;
            _onUnlock = null;
            _button.interactable = false;
            if (_lockIcon != null)   _lockIcon.gameObject.SetActive(false);
            if (_icon != null)       _icon.gameObject.SetActive(false);
            if (_costArea != null)   _costArea.SetActive(false);
            if (_costText != null)   _costText.text   = string.Empty;
            if (_rewardText != null) _rewardText.text = string.Empty;
            if (_background != null) _background.color = _lockedColor;
        }

        // ── 내부 ──────────────────────────────────────────────

        private void ShowContent(MemoryFragmentData piece)
        {
            if (_icon != null)
            {
                // piece에 아이콘이 지정된 경우에만 덮어쓴다.
                // null이면 프리팹의 기본 스프라이트를 그대로 유지.
                if (piece.Icon != null) _icon.sprite = piece.Icon;
                _icon.enabled = true; // 이전에 비활성화된 Image 컴포넌트를 복구
            }
            if (_costText != null)   _costText.text   = piece.ShardCost.ToString();
            if (_rewardText != null) _rewardText.text = BuildRewardText(piece);
        }

        private void SetVisualState(SlotState state)
        {
            Color color = state switch
            {
                SlotState.Unlocked   => _unlockedColor,
                SlotState.Unlockable => _unlockableColor,
                SlotState.Locked     => _lockedColor,
                _                    => _lockedColor,
            };

            if (_background != null) _background.color = color;

            bool isUnlocked = state == SlotState.Unlocked;

            // 해금 완료: LockIcon 숨기고 Icon 표시
            // 잠금/해금가능: LockIcon 표시하고 Icon 숨김
            if (_lockIcon != null) _lockIcon.gameObject.SetActive(!isUnlocked);
            if (_icon     != null) _icon.gameObject.SetActive(isUnlocked);

            // 해금 완료 시 CostArea 전체 숨김
            if (_costArea != null) _costArea.SetActive(!isUnlocked);
            else if (_costText != null) _costText.gameObject.SetActive(!isUnlocked);
        }

        private void OnSlotClicked()
        {
            if (_piece != null) _onUnlock?.Invoke(_piece);
        }

        public static string BuildRewardText(MemoryFragmentData piece)
        {
            return piece.RewardType switch
            {
                MemoryPieceRewardType.StatBoost         => $"{StatIdToKorean(piece.RewardStat)} +{piece.RewardMagnitude * 100f:F0}%",
                MemoryPieceRewardType.RelicSlotExpand   => $"유물 슬롯 +{(int)piece.RewardMagnitude}",
                MemoryPieceRewardType.StartingGold      => $"시작 골드 +{(int)piece.RewardMagnitude}",
                MemoryPieceRewardType.ShardDropBonus    => $"런당 파편 +{(int)piece.RewardMagnitude}",
                MemoryPieceRewardType.RewardSlotExpand  => $"보상 선택지 +{(int)piece.RewardMagnitude}",
                MemoryPieceRewardType.ShopSlotExpand    => $"상점 슬롯 +{(int)piece.RewardMagnitude}",
                MemoryPieceRewardType.RewardRarityBoost => $"보상 등급 확률 +{piece.RewardMagnitude * 100f:F0}%",
                MemoryPieceRewardType.ReviveOnce        => "부활 1회 해금",
                MemoryPieceRewardType.RoomSkip          => "방 건너뛰기 해금",
                MemoryPieceRewardType.RunStartRelic     => "시작 유물 1개 해금",
                MemoryPieceRewardType.TalentPointsBonus => $"재능 포인트 +{(int)piece.RewardMagnitude}",
                MemoryPieceRewardType.StartingRelicCount => $"시작 유물 +{(int)piece.RewardMagnitude}",
                _                                       => string.Empty,
            };
        }

        private static string StatIdToKorean(LostMemory.Combat.StatId stat)
        {
            return stat switch
            {
                LostMemory.Combat.StatId.AttackPower    => "공격력",
                LostMemory.Combat.StatId.AttackSpeed    => "공격 속도",
                LostMemory.Combat.StatId.MoveSpeed      => "이동 속도",
                LostMemory.Combat.StatId.MaxHealth      => "최대 체력",
                LostMemory.Combat.StatId.FinisherDamage => "마무리 피해",
                LostMemory.Combat.StatId.DashCooldown   => "대시 쿨타임",
                LostMemory.Combat.StatId.HealReceived   => "회복량",
                LostMemory.Combat.StatId.Critical       => "치명타 확률",
                LostMemory.Combat.StatId.Cooldown       => "쿨타임",
                LostMemory.Combat.StatId.Range          => "공격 범위",
                LostMemory.Combat.StatId.Dodge          => "회피율",
                LostMemory.Combat.StatId.Defense        => "방어력",
                LostMemory.Combat.StatId.CriticalDamage => "치명타 피해",
                LostMemory.Combat.StatId.ManaRegen      => "마나 회복",
                _                                       => stat.ToString(),
            };
        }

        private enum SlotState { Unlocked, Unlockable, Locked }
    }
}
