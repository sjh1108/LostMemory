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

        // CL-146: 탐욕 (GoldGainPercent) 적용용 multiplier. SetGainMultiplier 로 설정.
        // Add(amount) 시 amount × _gainMultiplier 적용.
        private float _gainMultiplier = 1f;

        /// <summary>골드 변경 시 새 Current 값을 인자로 발화. UI sync 용.</summary>
        public event Action<int> Changed;

        /// <summary>
        /// CL-146 후속: 어떤 GoldWallet 인스턴스든 Awake 가 끝날 때 1회 발화.
        /// SetEffectApplicator 처럼 player prefab 단계에서 wiring 불가능한
        /// 늦은 구독자가 wallet 의 등장 시점을 잡기 위해 사용.
        ///
        /// 구독자 책임: 자기 OnDisable/destroy 에서 unsubscribe.
        /// 1회 호출이라 누적 race 없음.
        /// </summary>
        public static event Action<GoldWallet> Spawned;

        /// <summary>CL-146: 탐욕 set 효과 — Add 시 amount 에 곱해질 multiplier (1.0 = 기본, 1.3 = +30% 획득).</summary>
        public void SetGainMultiplier(float mul)
        {
            _gainMultiplier = Mathf.Max(0f, mul);
            if (logChanges)
            {
                Debug.Log($"[GoldWallet] SetGainMultiplier → {_gainMultiplier:F2}", this);
            }
        }

        private void Awake()
        {
            Current = Mathf.Max(0, initialGold);
            if (logChanges)
            {
                Debug.Log($"[GoldWallet] Initialized. Current={Current}", this);
            }
            Changed?.Invoke(Current);

            // CL-146 후속: 늦게 들어온 구독자(SetEffectApplicator 등)에게 등장 통지.
            // 가입 시점이 Awake 이후라도 lazy-resolve 로직이 따로 잡아주므로
            // 여기서 1회만 broadcast 하면 충분.
            try { Spawned?.Invoke(this); }
            catch (Exception e)
            {
                Debug.LogError($"[GoldWallet] Spawned 구독자 예외: {e}", this);
            }
        }

        public void Add(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[GoldWallet] Add ignored — amount must be positive. amount={amount}", this);
                return;
            }
            // CL-146: 탐욕 multiplier 적용
            int actual = Mathf.RoundToInt(amount * _gainMultiplier);
            Current += actual;
            if (logChanges)
            {
                if (Mathf.Abs(_gainMultiplier - 1f) > 0.001f)
                    Debug.Log($"[GoldWallet] Add({amount} × {_gainMultiplier:F2} = {actual}) -> Current={Current}", this);
                else
                    Debug.Log($"[GoldWallet] Add(+{actual}) -> Current={Current}", this);
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
