using System;
using UnityEngine;

namespace LostMemory.Player
{
    /// <summary>
    /// 런 중 플레이어의 재화(골드 / 기억의 파편)를 관리하는 싱글턴 컴포넌트.
    ///
    /// 사용법:
    ///   - PlayerWallet.Instance.AddGold(50)          → 골드 50 추가
    ///   - PlayerWallet.Instance.SpendGold(30)         → 골드 30 차감 (부족하면 false 반환)
    ///   - PlayerWallet.Instance.OnGoldChanged        구독 → HUD 등에서 UI 갱신
    ///   - 런 종료 시 Clear() 호출 → 두 재화 모두 0 초기화
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Player/Player Wallet")]
    public sealed class PlayerWallet : MonoBehaviour
    {
        // ── 싱글턴 ──────────────────────────────────────────────────
        public static PlayerWallet Instance { get; private set; }

        // 어느 씬에서 단독 PlayMode 로 시작해도 지갑이 작동하도록 자동 부트스트랩.
        // Bootstrap 씬에 PlayerWallet GameObject 가 존재하면 그쪽이 Instance 가 되고 본 메서드는 no-op.
        // PlayerRunState / MemoryShardWallet 와 동일 패턴.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        public static PlayerWallet EnsureInstance()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("PlayerWallet");
            return go.AddComponent<PlayerWallet>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── 재화 프로퍼티 ────────────────────────────────────────────

        /// <summary>현재 골드 보유량</summary>
        public int Gold { get; private set; }

        /// <summary>현재 기억의 파편 보유량</summary>
        public int MemoryFragments { get; private set; }

        // ── 이벤트 ───────────────────────────────────────────────────

        /// <summary>골드가 바뀔 때마다 발생. 인자: 변경 후 골드 값</summary>
        public event Action<int> OnGoldChanged;

        /// <summary>기억의 파편이 바뀔 때마다 발생. 인자: 변경 후 파편 값</summary>
        public event Action<int> OnMemoryFragmentsChanged;

        // ── 골드 API ─────────────────────────────────────────────────

        /// <summary>골드를 추가한다.</summary>
        /// <param name="amount">추가할 양 (양수)</param>
        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            Gold += amount;
            OnGoldChanged?.Invoke(Gold);
        }

        /// <summary>
        /// 골드를 차감한다.
        /// </summary>
        /// <param name="amount">차감할 양 (양수)</param>
        /// <returns>골드가 충분하여 차감에 성공하면 <c>true</c>, 부족하면 <c>false</c></returns>
        public bool SpendGold(int amount)
        {
            if (amount <= 0) return true;
            if (Gold < amount) return false;

            Gold -= amount;
            OnGoldChanged?.Invoke(Gold);
            return true;
        }

        // ── 기억의 파편 API ──────────────────────────────────────────

        /// <summary>기억의 파편을 추가한다.</summary>
        /// <param name="amount">추가할 양 (양수)</param>
        public void AddMemoryFragments(int amount)
        {
            if (amount <= 0) return;
            MemoryFragments += amount;
            OnMemoryFragmentsChanged?.Invoke(MemoryFragments);
        }

        /// <summary>
        /// 기억의 파편을 차감한다.
        /// </summary>
        /// <param name="amount">차감할 양 (양수)</param>
        /// <returns>파편이 충분하여 차감에 성공하면 <c>true</c>, 부족하면 <c>false</c></returns>
        public bool SpendMemoryFragments(int amount)
        {
            if (amount <= 0) return true;
            if (MemoryFragments < amount) return false;

            MemoryFragments -= amount;
            OnMemoryFragmentsChanged?.Invoke(MemoryFragments);
            return true;
        }

        // ── 런 종료 ──────────────────────────────────────────────────

        /// <summary>
        /// 런 종료 시 호출. 두 재화를 모두 0으로 초기화하고 이벤트를 발생시킨다.
        /// </summary>
        public void Clear()
        {
            Gold            = 0;
            MemoryFragments = 0;
            OnGoldChanged?.Invoke(Gold);
            OnMemoryFragmentsChanged?.Invoke(MemoryFragments);
        }
    }
}
