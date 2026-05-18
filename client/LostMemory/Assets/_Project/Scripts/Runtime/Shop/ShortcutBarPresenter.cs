using LostMemory.Combat;
using LostMemory.Networking.Player;
using LostMemory.Relics;
using LostMemory.TestKhi;
using Unity.Netcode;
using UnityEngine;

namespace LostMemory.Shop
{
    /// <summary>
    /// ShortcutBarView를 로컬 플레이어의 소모품 인벤토리와 숫자키 입력에 연결한다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Shop/Shortcut Bar Presenter")]
    public sealed class ShortcutBarPresenter : MonoBehaviour
    {
        [SerializeField] private ShortcutBarView _view;
        [SerializeField] private PlayerConsumableInventory _inventory;
        [SerializeField] private PlayerHealing _playerHealing;
        [SerializeField] private KhiDownController _downController;
        [SerializeField] private bool _enableNumberHotkeys = true;
        [SerializeField, Min(0.1f)] private float _resolveRetryInterval = 0.5f;

        private readonly KeyCode[] _hotkeys =
        {
            KeyCode.Alpha1,
            KeyCode.Alpha2,
            KeyCode.Alpha3,
            KeyCode.Alpha4,
        };

        private readonly KeyCode[] _keypadHotkeys =
        {
            KeyCode.Keypad1,
            KeyCode.Keypad2,
            KeyCode.Keypad3,
            KeyCode.Keypad4,
        };

        private PlayerConsumableInventory _subscribedInventory;
        private float _nextResolveTime;

        private void Reset()
        {
            _view = GetComponent<ShortcutBarView>();
        }

        private void OnEnable()
        {
            ResolveView();
            LocalPlayerResolver.LocalPlayerReady += HandleLocalPlayerReady;
            TryResolveLocalPlayer();
            BindView();
        }

        private void OnDisable()
        {
            LocalPlayerResolver.LocalPlayerReady -= HandleLocalPlayerReady;
            UnsubscribeInventory();
        }

        private void Update()
        {
            if ((_inventory == null || _playerHealing == null) && Time.unscaledTime >= _nextResolveTime)
            {
                _nextResolveTime = Time.unscaledTime + _resolveRetryInterval;
                TryResolveLocalPlayer();
                BindView();
            }

            if (!_enableNumberHotkeys
                || _inventory == null
                || _playerHealing == null
                || Time.timeScale <= 0f
                || IsPlayerActionBlocked())
            {
                return;
            }

            for (int i = 0; i < _hotkeys.Length; i++)
            {
                if (Input.GetKeyDown(_hotkeys[i]) || Input.GetKeyDown(_keypadHotkeys[i]))
                {
                    TryUseSlot(i);
                }
            }
        }

        private void ResolveView()
        {
            if (_view == null)
            {
                _view = GetComponent<ShortcutBarView>();
            }
        }

        private void TryResolveLocalPlayer()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                if (LocalPlayerResolver.TryGetRegisteredLocalPlayer(out KhiPlayerStateAggregator registeredPlayer))
                {
                    HandleLocalPlayerReady(registeredPlayer);
                }

                return;
            }

            KhiPlayerStateAggregator localPlayer = LocalPlayerResolver.LocalPlayer;
            if (localPlayer != null)
            {
                HandleLocalPlayerReady(localPlayer);
                return;
            }

            ResolveFallbackComponents();
        }

        private void HandleLocalPlayerReady(KhiPlayerStateAggregator localPlayer)
        {
            if (localPlayer == null)
            {
                return;
            }

            _inventory = ResolveComponent<PlayerConsumableInventory>(localPlayer);
            if (_inventory == null)
            {
                _inventory = localPlayer.gameObject.AddComponent<PlayerConsumableInventory>();
            }

            _playerHealing = ResolveComponent<PlayerHealing>(localPlayer);
            _downController = ResolveComponent<KhiDownController>(localPlayer);
            BindView();
        }

        private void ResolveFallbackComponents()
        {
            if (_inventory == null)
            {
                _inventory = FindFirstObjectByType<PlayerConsumableInventory>();
            }

            if (_playerHealing == null)
            {
                _playerHealing = FindFirstObjectByType<PlayerHealing>();
            }

            if (_downController == null)
            {
                _downController = FindFirstObjectByType<KhiDownController>();
            }
        }

        private void BindView()
        {
            if (_view == null || _inventory == null)
            {
                return;
            }

            _view.Init(_inventory);
            SubscribeInventory();
        }

        private void SubscribeInventory()
        {
            if (_subscribedInventory == _inventory)
            {
                return;
            }

            UnsubscribeInventory();
            _subscribedInventory = _inventory;
            _subscribedInventory.Changed += HandleInventoryChanged;
        }

        private void UnsubscribeInventory()
        {
            if (_subscribedInventory == null)
            {
                return;
            }

            _subscribedInventory.Changed -= HandleInventoryChanged;
            _subscribedInventory = null;
        }

        private void HandleInventoryChanged()
        {
            if (_view != null)
            {
                _view.Refresh();
            }
        }

        private void TryUseSlot(int slotIndex)
        {
            if (IsPlayerActionBlocked())
            {
                return;
            }

            RelicData consumable = _inventory.Get(slotIndex);
            if (consumable == null)
            {
                return;
            }

            if (!_playerHealing.TryUseConsumable(consumable))
            {
                Debug.LogWarning($"[ShortcutBarPresenter] Consumable '{consumable.DisplayName}' is not usable yet.", this);
                return;
            }

            _inventory.Remove(slotIndex);
        }

        private bool IsPlayerActionBlocked()
        {
            return KhiPlayerActionGate.IsBlocked(_downController);
        }

        private static T ResolveComponent<T>(Component owner) where T : Component
        {
            T component = owner.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            component = owner.GetComponentInChildren<T>(true);
            if (component != null)
            {
                return component;
            }

            return owner.GetComponentInParent<T>();
        }
    }
}
