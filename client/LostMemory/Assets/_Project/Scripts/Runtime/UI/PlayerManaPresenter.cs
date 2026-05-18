using LostMemory.Combat;
using LostMemory.Networking.Player;
using LostMemory.TestKhi;
using MoreMountains.TopDownEngine;
using Unity.Netcode;
using UnityEngine;

namespace LostMemory.UI
{
    /// <summary>
    /// PlayerHUD 의 MP 바를 로컬 플레이어의 PlayerMana 와 연결한다.
    /// PlayerHUDPresenter(HP 담당) 옆에 함께 부착 — 같은 HealthBarView 를 공유한다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Player Mana Presenter")]
    public sealed class PlayerManaPresenter : MonoBehaviour
    {
        [SerializeField] private HealthBarView _healthBarView;
        [SerializeField] private PlayerMana _targetMana;
        [SerializeField] private bool _autoResolveLocalPlayer = true;
        [SerializeField] private string _fallbackPlayerId = "Player1";
        [SerializeField, Min(0.1f)] private float _resolveRetryInterval = 0.5f;

        private PlayerMana _subscribedMana;
        private float _nextResolveTime;

        private void Reset()
        {
            _healthBarView = GetComponent<HealthBarView>();
        }

        private void Awake()
        {
            ResolveView();
        }

        private void OnEnable()
        {
            ResolveView();
            LocalPlayerResolver.LocalPlayerReady += HandleLocalPlayerReady;

            if (_targetMana == null && _autoResolveLocalPlayer)
            {
                TryResolveLocalPlayerMana();
            }
            else
            {
                BindMana(_targetMana);
            }

            RefreshMP();
        }

        private void OnDisable()
        {
            LocalPlayerResolver.LocalPlayerReady -= HandleLocalPlayerReady;
            UnsubscribeManaCallbacks();
        }

        private void Update()
        {
            if (!_autoResolveLocalPlayer || _targetMana != null || Time.unscaledTime < _nextResolveTime)
            {
                return;
            }

            _nextResolveTime = Time.unscaledTime + _resolveRetryInterval;
            TryResolveLocalPlayerMana();
        }

        public void SetTarget(PlayerMana mana)
        {
            BindMana(mana);
            RefreshMP();
        }

        private void ResolveView()
        {
            if (_healthBarView == null)
            {
                _healthBarView = GetComponent<HealthBarView>();
            }
        }

        private void TryResolveLocalPlayerMana()
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

            BindMana(ResolveFallbackCharacterMana());
            RefreshMP();
        }

        private void HandleLocalPlayerReady(KhiPlayerStateAggregator localPlayer)
        {
            if (!_autoResolveLocalPlayer || localPlayer == null)
            {
                return;
            }

            PlayerMana mana = ResolveMana(localPlayer);
            if (mana != null)
            {
                BindMana(mana);
                RefreshMP();
            }
        }

        private void BindMana(PlayerMana mana)
        {
            if (_targetMana == mana)
            {
                SubscribeManaCallbacks();
                return;
            }

            UnsubscribeManaCallbacks();
            _targetMana = mana;
            SubscribeManaCallbacks();
        }

        private void SubscribeManaCallbacks()
        {
            if (_targetMana == null || _subscribedMana == _targetMana)
            {
                return;
            }

            UnsubscribeManaCallbacks();
            _subscribedMana = _targetMana;
            _subscribedMana.ManaChanged += HandleManaChanged;
        }

        private void UnsubscribeManaCallbacks()
        {
            if (_subscribedMana == null)
            {
                return;
            }

            _subscribedMana.ManaChanged -= HandleManaChanged;
            _subscribedMana = null;
        }

        private void HandleManaChanged(int current, int max)
        {
            if (_healthBarView == null) return;
            _healthBarView.UpdateMP(current, max);
        }

        private void RefreshMP()
        {
            if (_healthBarView == null) return;

            if (_targetMana == null)
            {
                _healthBarView.SetMPVisible(false);
                return;
            }

            _healthBarView.SetMPVisible(true);
            _healthBarView.UpdateMP(_targetMana.CurrentMana, _targetMana.MaxMana);
        }

        private PlayerMana ResolveFallbackCharacterMana()
        {
            Character[] characters = FindObjectsByType<Character>(FindObjectsSortMode.None);
            for (int i = 0; i < characters.Length; i++)
            {
                Character character = characters[i];
                if (character == null || character.CharacterType != Character.CharacterTypes.Player)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(_fallbackPlayerId) && character.PlayerID != _fallbackPlayerId)
                {
                    continue;
                }

                PlayerMana mana = ResolveMana(character);
                if (mana != null)
                {
                    return mana;
                }
            }

            return null;
        }

        private static PlayerMana ResolveMana(Component owner)
        {
            PlayerMana mana = owner.GetComponent<PlayerMana>();
            if (mana != null) return mana;

            mana = owner.GetComponentInChildren<PlayerMana>(true);
            if (mana != null) return mana;

            return owner.GetComponentInParent<PlayerMana>();
        }
    }
}
