using System;
using UnityEngine;

namespace LostMemory.Stage
{
    /// <summary>
    /// CL-113 골드 시스템 최소 — Run 한정 지갑.
    ///
    /// 책임:
    ///   1. 현재 골드 보유량 (Current) 추적
    ///   2. Add(int) — 보상 골드 / 픽업 (Combat 클리어 시 RunManager 가 +50)
    ///   3. Spend(int) — 상점 구매 (ShopController 가 sync). 부족 시 false
    ///   4. Reset() — Run 종료 시 초기값 복원 (RunManager.CloseResulting 에서 호출)
    ///   5. Changed 이벤트 — UI sync (ShopPanelView.UpdateGold 등)
    ///
    /// Run 한정 — 메타 누적 / 영구 골드 / 마을 환전 등은 후속 CL-115.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Stage/Gold Wallet")]
    public sealed class GoldWallet : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField, Tooltip("Run 시작 시 / Reset 시 복원될 초기 골드.")]
        private int initialGold = 0;

        [Header("Debug")]
        [SerializeField, Tooltip("골드 변경 시 console 로그.")]
        private bool logChanges = true;

        public int Current { get; private set; }

        /// <summary>골드 변경 시 새 Current 값을 인자로 발화. UI sync 용.</summary>
        public event Action<int> Changed;

        private void Awake()
        {
            Current = Mathf.Max(0, initialGold);
            if (logChanges)
            {
                Debug.Log($"[GoldWallet] Initialized. Current={Current}", this);
            }
            Changed?.Invoke(Current);
        }

        public void Add(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[GoldWallet] Add ignored — amount must be positive. amount={amount}", this);
                return;
            }
            Current += amount;
            if (logChanges)
            {
                Debug.Log($"[GoldWallet] Add(+{amount}) -> Current={Current}", this);
            }
            Changed?.Invoke(Current);
        }

        /// <summary>부족 시 false 반환 (차감 X). 성공 시 차감 + Changed 발화.</summary>
        public bool Spend(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[GoldWallet] Spend ignored — amount must be positive. amount={amount}", this);
                return false;
            }
            if (Current < amount)
            {
                if (logChanges)
                {
                    Debug.Log($"[GoldWallet] Spend({amount}) refused — insufficient. Current={Current}", this);
                }
                return false;
            }
            Current -= amount;
            if (logChanges)
            {
                Debug.Log($"[GoldWallet] Spend(-{amount}) -> Current={Current}", this);
            }
            Changed?.Invoke(Current);
            return true;
        }

        /// <summary>Run 종료 시 RunManager.CloseResulting 에서 호출. initialGold 로 복원.</summary>
        public void ResetToInitial()
        {
            Current = Mathf.Max(0, initialGold);
            if (logChanges)
            {
                Debug.Log($"[GoldWallet] ResetToInitial -> Current={Current}", this);
            }
            Changed?.Invoke(Current);
        }
    }
}
