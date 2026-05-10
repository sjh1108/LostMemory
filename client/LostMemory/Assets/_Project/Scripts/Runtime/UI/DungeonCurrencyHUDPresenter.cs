using LostMemory.Memory;
using LostMemory.Stage;
using UnityEngine;

namespace LostMemory.UI
{
    /// <summary>
    /// 던전 CurrencyHUD에 이번 런 골드와 영구 기억 파편 잔액을 연결한다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Dungeon Currency HUD Presenter")]
    public sealed class DungeonCurrencyHUDPresenter : MonoBehaviour
    {
        [SerializeField] private CurrencyHUDView _view;
        [SerializeField] private GoldWallet _goldWallet;
        [SerializeField] private MemoryShardWallet _memoryShardWallet;
        [SerializeField, Min(0.1f)] private float _resolveRetryInterval = 0.5f;

        private GoldWallet _subscribedGoldWallet;
        private MemoryShardWallet _subscribedMemoryShardWallet;
        private float _nextResolveTime;

        private void Reset()
        {
            _view = GetComponent<CurrencyHUDView>();
        }

        private void OnEnable()
        {
            ResolveView();
            ResolveSources();
            SubscribeSources();
            RefreshAll();
        }

        private void OnDisable()
        {
            UnsubscribeSources();
        }

        private void Update()
        {
            if (_goldWallet != null || Time.unscaledTime < _nextResolveTime)
            {
                return;
            }

            _nextResolveTime = Time.unscaledTime + _resolveRetryInterval;
            ResolveSources();
            SubscribeSources();
            RefreshAll();
        }

        private void ResolveView()
        {
            if (_view == null)
            {
                _view = GetComponent<CurrencyHUDView>();
            }
        }

        private void ResolveSources()
        {
            if (_goldWallet == null)
            {
                RunManager runManager = RunManager.Instance;
                if (runManager != null)
                {
                    _goldWallet = runManager.GetComponent<GoldWallet>();
                }
            }

            if (_goldWallet == null)
            {
                _goldWallet = FindFirstObjectByType<GoldWallet>();
            }

            if (_memoryShardWallet == null)
            {
                _memoryShardWallet = MemoryShardWallet.EnsureInstance();
            }
        }

        private void SubscribeSources()
        {
            if (_subscribedGoldWallet != _goldWallet)
            {
                if (_subscribedGoldWallet != null)
                {
                    _subscribedGoldWallet.Changed -= HandleGoldChanged;
                }

                _subscribedGoldWallet = _goldWallet;
                if (_subscribedGoldWallet != null)
                {
                    _subscribedGoldWallet.Changed += HandleGoldChanged;
                }
            }

            if (_subscribedMemoryShardWallet != _memoryShardWallet)
            {
                if (_subscribedMemoryShardWallet != null)
                {
                    _subscribedMemoryShardWallet.ShardsChanged -= HandleMemoryShardsChanged;
                }

                _subscribedMemoryShardWallet = _memoryShardWallet;
                if (_subscribedMemoryShardWallet != null)
                {
                    _subscribedMemoryShardWallet.ShardsChanged += HandleMemoryShardsChanged;
                }
            }
        }

        private void UnsubscribeSources()
        {
            if (_subscribedGoldWallet != null)
            {
                _subscribedGoldWallet.Changed -= HandleGoldChanged;
                _subscribedGoldWallet = null;
            }

            if (_subscribedMemoryShardWallet != null)
            {
                _subscribedMemoryShardWallet.ShardsChanged -= HandleMemoryShardsChanged;
                _subscribedMemoryShardWallet = null;
            }
        }

        private void RefreshAll()
        {
            if (_view == null)
            {
                return;
            }

            _view.SetGoldVisible(true);
            _view.SetMemoryFragmentsVisible(true);
            _view.SetGold(_goldWallet != null ? _goldWallet.Current : 0);
            _view.SetMemoryFragments(_memoryShardWallet != null ? _memoryShardWallet.CurrentShards : 0);
        }

        private void HandleGoldChanged(int amount)
        {
            if (_view != null)
            {
                _view.SetGold(amount);
            }
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
