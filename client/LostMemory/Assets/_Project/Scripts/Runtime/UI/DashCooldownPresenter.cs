using LostMemory.Networking.Player;
using LostMemory.TestKhi;
using MoreMountains.Tools;
using Unity.Netcode;
using UnityEngine;

namespace LostMemory.UI
{
    /// <summary>
    /// 로컬 플레이어의 KhiDashController.Cooldown(MMCooldown) 상태를 구독해
    /// CooldownIndicatorView 를 갱신. PlayerHUDPresenter / ParryCooldownPresenter 와 동일 패턴.
    ///
    /// MMCooldown 의 함정:
    ///   - MMCooldown.Progress 는 Refilling 상태에서만 유의미 (다른 상태에선 0).
    ///   - "쿨다운이 0→1 로 차오르는" 시각화는 Refilling 의 CurrentDurationLeft 로 직접 계산.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Dash Cooldown Presenter")]
    public class DashCooldownPresenter : MonoBehaviour
    {
        [SerializeField] private CooldownIndicatorView view;
        [SerializeField] private KhiDashController dash;
        [SerializeField] private bool autoResolveLocalPlayer = true;
        [SerializeField, Min(0.1f)] private float resolveRetryInterval = 0.5f;

        private float _nextResolveTime;

        private void Reset()
        {
            view = GetComponent<CooldownIndicatorView>();
        }

        private void Awake()
        {
            if (view == null)
            {
                view = GetComponent<CooldownIndicatorView>();
            }
        }

        private void OnEnable()
        {
            LocalPlayerResolver.LocalPlayerReady += HandleLocalPlayerReady;

            if (dash == null && autoResolveLocalPlayer)
            {
                TryResolveLocalPlayer();
            }

            ApplyReady();
        }

        private void OnDisable()
        {
            LocalPlayerResolver.LocalPlayerReady -= HandleLocalPlayerReady;
        }

        private void Update()
        {
            if (dash == null || dash.Cooldown == null)
            {
                if (autoResolveLocalPlayer && Time.unscaledTime >= _nextResolveTime)
                {
                    _nextResolveTime = Time.unscaledTime + resolveRetryInterval;
                    TryResolveLocalPlayer();
                }
                return;
            }

            MMCooldown cd = dash.Cooldown;
            float total = Mathf.Max(cd.ConsumptionDuration, 0.0001f);
            float remaining = Mathf.Max(0f, cd.CurrentDurationLeft);

            // MMCooldown 상태 → 표시 분기.
            // Idle (Ready) : 사용 가능
            // Consuming    : 대시 사용 직후, 보통 매우 짧음 (한 프레임). remaining 거의 ConsumptionDuration 에 가까움.
            // Stopped      : (PauseOnExit 등) 정지 상태.
            // Refilling    : 사용 후 회복 중.
            //
            // 표시상으로는 Consuming/Stopped/Refilling 을 통합해 "쿨다운 중" 으로 묶음.
            if (cd.CooldownState == MMCooldown.CooldownStates.Idle)
            {
                ApplyReady();
                return;
            }

            float progress = 1f - remaining / total;
            view.SetCooldown(progress, remaining);
        }

        public void SetTarget(KhiDashController controller)
        {
            dash = controller;
            ApplyReady();
        }

        private void ApplyReady()
        {
            if (view != null)
            {
                view.SetReady();
            }
        }

        private void TryResolveLocalPlayer()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                if (LocalPlayerResolver.TryGetRegisteredLocalPlayer(out KhiPlayerStateAggregator registered))
                {
                    HandleLocalPlayerReady(registered);
                }
                return;
            }

            KhiPlayerStateAggregator local = LocalPlayerResolver.LocalPlayer;
            if (local != null)
            {
                HandleLocalPlayerReady(local);
            }
        }

        private void HandleLocalPlayerReady(KhiPlayerStateAggregator localPlayer)
        {
            if (!autoResolveLocalPlayer || localPlayer == null)
            {
                return;
            }

            KhiDashController resolved = localPlayer.GetComponent<KhiDashController>();
            if (resolved == null)
            {
                resolved = localPlayer.GetComponentInChildren<KhiDashController>(true);
            }
            if (resolved == null)
            {
                resolved = localPlayer.GetComponentInParent<KhiDashController>();
            }

            if (resolved != null)
            {
                dash = resolved;
            }
        }
    }
}
