using LostMemory.Networking.Player;
using LostMemory.TestKhi;
using Unity.Netcode;
using UnityEngine;

namespace LostMemory.UI
{
    /// <summary>
    /// 로컬 플레이어의 KhiParryController 를 구독해 CooldownIndicatorView 를 갱신.
    /// PlayerHUDPresenter 와 동일한 LocalPlayerResolver 패턴.
    ///
    /// 표시 규칙:
    ///   - Idle / ParryWindow : Ready 표시 (fill=1, 아이콘 풀 컬러)
    ///   - FailureRecovery / Cooldown : 진행률 + 남은 시간 표시
    /// 진행률은 두 단계를 합쳐 단일 게이지로 표현 (사용자 결정).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Parry Cooldown Presenter")]
    public class ParryCooldownPresenter : MonoBehaviour
    {
        [SerializeField] private CooldownIndicatorView view;
        [SerializeField] private KhiParryController parry;
        [SerializeField] private bool autoResolveLocalPlayer = true;
        [SerializeField, Min(0.1f)] private float resolveRetryInterval = 0.5f;

        private float _nextResolveTime;
        // FailureRecovery 시작 시 캡처한 총 길이 (Recovery + Cooldown). 단일 게이지 시간축.
        private float _activeTotalDuration;

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

            if (parry == null && autoResolveLocalPlayer)
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
            if (parry == null)
            {
                if (autoResolveLocalPlayer && Time.unscaledTime >= _nextResolveTime)
                {
                    _nextResolveTime = Time.unscaledTime + resolveRetryInterval;
                    TryResolveLocalPlayer();
                }
                return;
            }

            // 표시 상태 결정: FailureRecovery / Cooldown 이면 진행 중.
            KhiParryState state = parry.CurrentState;
            bool onCooldown = state == KhiParryState.FailureRecovery || state == KhiParryState.Cooldown;

            if (!onCooldown)
            {
                ApplyReady();
                _activeTotalDuration = 0f;
                return;
            }

            // 진행률 계산:
            //   FailureRecovery 시 총 길이 = Recovery + Cooldown (합산 게이지)
            //   Cooldown(성공 후 즉시 진입) 시 총 길이 = Cooldown 만
            // 상태 진입 순간을 정확히 잡기 어려우니, 현재 상태 기준으로 max 누적값을 사용.
            float recovery = parry.ParryFailureRecoveryDuration;
            float cooldown = parry.ParryCooldownDuration;
            float remaining = Mathf.Max(0f, parry.CurrentStateRemaining);

            if (state == KhiParryState.FailureRecovery)
            {
                // Recovery 중: 남은 시간 = Recovery 잔량 + 향후 Cooldown 전체.
                _activeTotalDuration = recovery + cooldown;
                float remainingTotal = remaining + cooldown;
                float progress = _activeTotalDuration > Mathf.Epsilon
                    ? 1f - remainingTotal / _activeTotalDuration
                    : 1f;
                view.SetCooldown(progress, remainingTotal);
            }
            else // Cooldown
            {
                // Cooldown 중. FailureRecovery 를 거쳐왔으면 _activeTotalDuration 이 이미 합산값.
                // 성공 → 곧바로 Cooldown 케이스는 _activeTotalDuration 이 0 이므로 cooldown 으로 초기화.
                if (_activeTotalDuration <= Mathf.Epsilon)
                {
                    _activeTotalDuration = cooldown;
                }

                float progress = _activeTotalDuration > Mathf.Epsilon
                    ? 1f - remaining / _activeTotalDuration
                    : 1f;
                view.SetCooldown(progress, remaining);
            }
        }

        public void SetTarget(KhiParryController controller)
        {
            parry = controller;
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

            KhiParryController resolved = localPlayer.GetComponent<KhiParryController>();
            if (resolved == null)
            {
                resolved = localPlayer.GetComponentInChildren<KhiParryController>(true);
            }
            if (resolved == null)
            {
                resolved = localPlayer.GetComponentInParent<KhiParryController>();
            }

            if (resolved != null)
            {
                parry = resolved;
            }
        }
    }
}
