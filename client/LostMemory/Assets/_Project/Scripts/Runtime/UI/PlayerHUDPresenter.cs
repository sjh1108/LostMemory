using LostMemory.Player;
using LostMemory.Networking.Player;
using LostMemory.TestKhi;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using Unity.Netcode;
using UnityEngine;

namespace LostMemory.UI
{
    /// <summary>
    /// PlayerHUD를 로컬 플레이어의 TopDownEngine Health와 연결한다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Player HUD Presenter")]
    public class PlayerHUDPresenter : MonoBehaviour, MMEventListener<HealthChangeEvent>
    {
        [SerializeField] private HealthBarView _healthBarView;
        [SerializeField] private Health _targetHealth;
        [SerializeField] private bool _autoResolveLocalPlayer = true;
        [SerializeField] private string _fallbackPlayerId = "Player1";
        [SerializeField, Min(0.1f)] private float _resolveRetryInterval = 0.5f;

        private Health _subscribedHealth;
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
            this.MMEventStartListening<HealthChangeEvent>();
            PlayerHealthSnapshotter.SnapshotRestoreCompleted += HandleHealthSnapshotRestoreCompleted;
            LocalPlayerResolver.LocalPlayerReady += HandleLocalPlayerReady;

            if (_targetHealth == null && _autoResolveLocalPlayer)
            {
                TryResolveLocalPlayerHealth();
            }
            else
            {
                BindHealth(_targetHealth);
            }

            RefreshAll();
        }

        private void OnDisable()
        {
            LocalPlayerResolver.LocalPlayerReady -= HandleLocalPlayerReady;
            PlayerHealthSnapshotter.SnapshotRestoreCompleted -= HandleHealthSnapshotRestoreCompleted;
            this.MMEventStopListening<HealthChangeEvent>();
            UnsubscribeHealthCallbacks();
        }

        private void Update()
        {
            if (!_autoResolveLocalPlayer || _targetHealth != null || Time.unscaledTime < _nextResolveTime)
            {
                return;
            }

            _nextResolveTime = Time.unscaledTime + _resolveRetryInterval;
            TryResolveLocalPlayerHealth();
        }

        public void SetTarget(Health health)
        {
            BindHealth(health);
            RefreshAll();
        }

        public void OnMMEvent(HealthChangeEvent healthChangeEvent)
        {
            if (_targetHealth == null || healthChangeEvent.AffectedHealth != _targetHealth)
            {
                return;
            }

            RefreshHP();
        }

        private void ResolveView()
        {
            if (_healthBarView == null)
            {
                _healthBarView = GetComponent<HealthBarView>();
            }
        }

        private void TryResolveLocalPlayerHealth()
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

            BindHealth(ResolveFallbackCharacterHealth());
        }

        private void HandleLocalPlayerReady(KhiPlayerStateAggregator localPlayer)
        {
            if (!_autoResolveLocalPlayer || localPlayer == null)
            {
                return;
            }

            Health health = ResolveHealth(localPlayer);
            if (health != null)
            {
                BindHealth(health);
                RefreshAll();
            }
        }

        private void BindHealth(Health health)
        {
            Health effectiveHealth = health != null && health.MasterHealth != null
                ? health.MasterHealth
                : health;

            if (_targetHealth == effectiveHealth)
            {
                SubscribeHealthCallbacks();
                return;
            }

            UnsubscribeHealthCallbacks();
            _targetHealth = effectiveHealth;
            SubscribeHealthCallbacks();
        }

        private void SubscribeHealthCallbacks()
        {
            if (_targetHealth == null || _subscribedHealth == _targetHealth)
            {
                return;
            }

            UnsubscribeHealthCallbacks();
            _subscribedHealth = _targetHealth;
            _subscribedHealth.OnDeath += RefreshHP;
            _subscribedHealth.OnRevive += RefreshHP;
        }

        private void UnsubscribeHealthCallbacks()
        {
            if (_subscribedHealth == null)
            {
                return;
            }

            _subscribedHealth.OnDeath -= RefreshHP;
            _subscribedHealth.OnRevive -= RefreshHP;
            _subscribedHealth = null;
        }

        private void RefreshAll()
        {
            RefreshHP();
        }

        private void RefreshHP()
        {
            if (_healthBarView == null)
            {
                return;
            }

            if (_targetHealth == null)
            {
                _healthBarView.UpdateHP(0, 0);
                return;
            }

            if (PlayerHealthSnapshotter.TryGetPendingSnapshot(_targetHealth, out PlayerSnapshot snapshot))
            {
                float snapshotMax = snapshot.MaximumHealth > 0f ? snapshot.MaximumHealth : _targetHealth.MaximumHealth;
                int snapshotCurrent = Mathf.RoundToInt(Mathf.Clamp(snapshot.CurrentHealth, 0f, snapshotMax));
                _healthBarView.UpdateHP(snapshotCurrent, Mathf.RoundToInt(snapshotMax));
                return;
            }

            int current = Mathf.RoundToInt(_targetHealth.CurrentHealth);
            int max = Mathf.RoundToInt(_targetHealth.MaximumHealth);
            _healthBarView.UpdateHP(current, max);
        }

        private void HandleHealthSnapshotRestoreCompleted(Health restoredHealth)
        {
            if (_targetHealth == null || restoredHealth == null)
            {
                return;
            }

            Health effectiveHealth = restoredHealth.MasterHealth != null
                ? restoredHealth.MasterHealth
                : restoredHealth;

            if (_targetHealth == effectiveHealth)
            {
                RefreshHP();
            }
        }

        private Health ResolveFallbackCharacterHealth()
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

                Health health = ResolveHealth(character);
                if (health != null)
                {
                    return health;
                }
            }

            return null;
        }

        private static Health ResolveHealth(Component owner)
        {
            Health health = owner.GetComponent<Health>();
            if (health != null)
            {
                return health;
            }

            health = owner.GetComponentInChildren<Health>(true);
            if (health != null)
            {
                return health;
            }

            return owner.GetComponentInParent<Health>();
        }
    }
}
