using LostMemory.Memory;
using UnityEngine;

namespace LostMemory.UI
{
    /// <summary>
    /// 마을 TownCurrencyHUD에 영구 기억 파편 잔액을 연결한다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Town Currency HUD Presenter")]
    public sealed class TownCurrencyHUDPresenter : MonoBehaviour
    {
        [SerializeField] private CurrencyHUDView _view;
        // Inspector 연결 불필요 — 런타임에 MemoryShardWallet.Instance 로 자동 탐색
        [SerializeField] private MemoryShardWallet _memoryShardWallet;

        private MemoryShardWallet _subscribedMemoryShardWallet;

        private void Reset()
        {
            _view = GetComponent<CurrencyHUDView>();
        }

        private void OnEnable()
        {
            ResolveView();
            ResolveWallet();
            SubscribeWallet();
            Refresh();
        }

        private void OnDisable()
        {
            UnsubscribeWallet();
        }

        private void ResolveView()
        {
            if (_view == null)
            {
                _view = GetComponent<CurrencyHUDView>();
            }
        }

        private void ResolveWallet()
        {
            // Inspector 연결값이 있어도 항상 싱글톤으로 덮어써서
            // 중복 인스턴스 문제로 인한 이벤트 미수신을 방지한다.
            _memoryShardWallet = MemoryShardWallet.Instance ?? MemoryShardWallet.EnsureInstance();
        }

        private void SubscribeWallet()
        {
            if (_subscribedMemoryShardWallet == _memoryShardWallet)
            {
                return;
            }

            UnsubscribeWallet();
            _subscribedMemoryShardWallet = _memoryShardWallet;
            if (_subscribedMemoryShardWallet != null)
            {
                _subscribedMemoryShardWallet.ShardsChanged += HandleMemoryShardsChanged;
            }
        }

        private void UnsubscribeWallet()
        {
            if (_subscribedMemoryShardWallet == null)
            {
                return;
            }

            _subscribedMemoryShardWallet.ShardsChanged -= HandleMemoryShardsChanged;
            _subscribedMemoryShardWallet = null;
        }

        private void Refresh()
        {
            if (_view == null)
            {
                return;
            }

            _view.SetGoldVisible(false);
            _view.SetMemoryFragmentsVisible(true);
            _view.SetMemoryFragments(_memoryShardWallet != null ? _memoryShardWallet.CurrentShards : 0);
        }

        private void HandleMemoryShardsChanged(int amount)
        {
            if (_view != null)
            {
                _view.SetMemoryFragments(amount);
            }
        }
    }
}
