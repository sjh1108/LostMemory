using LostMemory.MagicalGirl;
using UnityEngine;

namespace LostMemory.UI
{
    /// <summary>
    /// 미소녀 5인 합체 궁극기(T 키) 쿨다운 HUD presenter.
    /// `DashCooldownPresenter` 와 동일 패턴 — `CooldownIndicatorView` 의 SetReady/SetCooldown/SetVisible 활용.
    ///
    /// 표시 정책:
    ///   - 5인 미만 (SetBonus off)   → SetVisible(false) — HUD에서 사라짐
    ///   - 5인 + Ready                → SetReady() — fill 숨김, 아이콘 풀 컬러
    ///   - 5인 + 발동/쿨다운            → SetCooldown(progress, remaining) — fill 차오름 + 카운트다운
    ///
    /// MagicalGirlSpawner.IsSetBonusActive / IsUltimateReady / FusionCooldownEndsAt 폴링.
    /// 상태 변화 시에만 setter 호출 — 매 프레임 동일 값 호출 방지.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Ultimate Cooldown Presenter")]
    public sealed class UltimateCooldownPresenter : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Reset/Awake 에서 같은 GameObject 의 CooldownIndicatorView 자동 wiring.")]
        [SerializeField] private CooldownIndicatorView view;
        [Tooltip("비워두면 FindAnyObjectByType 로 자동 탐색.")]
        [SerializeField] private MagicalGirlSpawner spawner;

        [Header("Cooldown")]
        [Tooltip("궁극기 총 쿨다운 시간 (초). MagicalGirlFusion 의 burstDuration+cooldown 합과 일치해야 progress 가 정확.")]
        [SerializeField, Min(1f)] private float ultimateCooldownDuration = 25f;

        [Header("Audio (선택)")]
        [Tooltip("Ready 진입 순간 1회 재생. null 이면 사운드 생략.")]
        [SerializeField] private AudioClip readyChimeClip;
        [SerializeField, Range(0f, 1f)] private float readyChimeVolume = 0.8f;

        [Header("Debug")]
        [SerializeField] private bool logTransitions = false;

        private bool _wasReady;
        private bool _wasVisible;
        private AudioSource _audioSource;

        private void Reset()
        {
            view = GetComponent<CooldownIndicatorView>();
        }

        private void Awake()
        {
            if (view == null) view = GetComponent<CooldownIndicatorView>();
            if (readyChimeClip != null)
            {
                _audioSource = GetComponent<AudioSource>();
                if (_audioSource == null)
                {
                    _audioSource = gameObject.AddComponent<AudioSource>();
                    _audioSource.playOnAwake = false;
                }
            }
            // 시작 시 HUD 숨김 — 5인 합체 전엔 표시 안 함.
            if (view != null) view.SetVisible(false);
        }

        private void Start()
        {
            ResolveSpawnerIfNeeded();
        }

        private void Update()
        {
            ResolveSpawnerIfNeeded();
            if (spawner == null || view == null) return;

            bool setBonus = spawner.IsSetBonusActive;
            if (!setBonus)
            {
                if (_wasVisible)
                {
                    view.SetVisible(false);
                    _wasVisible = false;
                    _wasReady = false;
                    if (logTransitions) Debug.Log("[UltimateCooldown] Hidden (set bonus off)", this);
                }
                return;
            }

            // 5인 활성 — 표시 켜기
            if (!_wasVisible)
            {
                view.SetVisible(true);
                _wasVisible = true;
                if (logTransitions) Debug.Log("[UltimateCooldown] Visible (set bonus on)", this);
            }

            bool ready = spawner.IsUltimateReady;
            if (ready)
            {
                if (!_wasReady)
                {
                    view.SetReady();
                    _wasReady = true;
                    if (readyChimeClip != null && _audioSource != null)
                        _audioSource.PlayOneShot(readyChimeClip, readyChimeVolume);
                    if (logTransitions) Debug.Log("[UltimateCooldown] Ready", this);
                }
                return;
            }

            // 쿨다운 중 (또는 발동 중)
            _wasReady = false;
            float endsAt = spawner.FusionCooldownEndsAt;
            float remaining = Mathf.Max(0f, endsAt - Time.time);
            float total = Mathf.Max(ultimateCooldownDuration, 0.0001f);
            float progress = 1f - remaining / total;
            view.SetCooldown(progress, remaining);
        }

        private void ResolveSpawnerIfNeeded()
        {
            if (spawner != null) return;
            spawner = FindAnyObjectByType<MagicalGirlSpawner>();
        }
    }
}
