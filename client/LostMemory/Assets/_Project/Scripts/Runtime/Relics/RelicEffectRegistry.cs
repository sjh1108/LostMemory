using System.Collections;
using System.Collections.Generic;
using LostMemory.Combat;
using LostMemory.Data;
using LostMemory.TestKhi;
using LostMemory.VFX;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Relics
{
    /// <summary>
    /// CL-107 Wave B + CL-108 Wave C + CL-109 Wave D 통합. 정식 *Relic Effect Registry* — effect 표 (effect table)
    /// 의 정식 진입점. PlayerRelicInventory.OnRelicAcquired 구독 → EffectType 에 따라 PlayerStatModifierContainer
    /// / PlayerShield 에 modifier 등록. 시간부 효과는 외부 이벤트 (EnemyKilledByPlayer / ParrySucceeded / OnDashEnded)
    /// 발화 시 AddTimed.
    ///
    /// CL-109 변경:
    /// - 이름 RelicEffectApplier → RelicEffectRegistry (정식 격상)
    /// - <see cref="IRelicEffectAuthority"/> 게이트 도입 (HandleAcquired + 모든 이벤트 핸들러 최상단)
    /// - PlayerRelicInventory.OnCleared 구독 → Run 종료 시 container/shield 일괄 정리
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Relics/Relic Effect Registry")]
    public sealed class RelicEffectRegistry : MonoBehaviour
    {
        [SerializeField] private PlayerRelicInventory inventory;
        [SerializeField] private PlayerStatModifierContainer container;
        [Tooltip("AttackPowerConditional (전투북) 의 HP 비율 평가용 + ShieldOnParry 의 maxHp 기준값. TDE Health.")]
        [SerializeField] private Health playerHealth;
        [Tooltip("AttackSpeedOnKillTimed (붉은송곳니) 의 적 처치 신호 구독용.")]
        [SerializeField] private KhiMeleeComboController combatController;
        [Tooltip("CL-108: ShieldOnParry (반격의 표식) 의 패링 성공 신호 구독용.")]
        [SerializeField] private KhiParryController parryController;
        [Tooltip("CL-108: MoveSpeedAfterDashTimed (추적자의 망토) 의 대시 종료 신호 구독용.")]
        [SerializeField] private KhiDashController dashController;
        [Tooltip("CL-108: ShieldOnParry 의 보호막 부여 대상.")]
        [SerializeField] private PlayerShield playerShield;

        [Header("VFX Prefabs (CL-203)")]
        [SerializeField] private GameObject _shieldVFXPrefab;
        [SerializeField] private GameObject _buffAuraVFXPrefab;
        [SerializeField] private Color _attackSpeedAuraColor = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color _moveSpeedAuraColor = new Color(0.3f, 0.6f, 1f, 1f);

        [Header("SFX (CL-203)")]
        [SerializeField] private AudioClip _shieldGrantedSfx;
        [SerializeField] private AudioClip _killBuffSfx;
        [SerializeField] private AudioClip _dashBuffSfx;
        [SerializeField, Range(0f, 1f)] private float _sfxVolume = 0.6f;

        [Header("VFX Fade Out (CL-203)")]
        [SerializeField, Min(0f)] private float _vfxFadeOutSeconds = 0.25f;

        // Phase B-1: NetworkRelicEffectAuthority 로 교체. 싱글 실행 시 NetworkManager 비활성 → true,
        // 멀티 실행 시 호스트만 true. LocalRelicEffectAuthority 는 보존(롤백 또는 테스트 용).
        private readonly IRelicEffectAuthority _authority = new NetworkRelicEffectAuthority();

        private readonly List<OnKillSubscription> _onKillSubscriptions = new();
        private readonly List<TimedSubscription> _onParrySuccessSubscriptions = new();
        private readonly List<TimedSubscription> _onDashEndSubscriptions = new();

        // CL-203: 활성 VFX 추적 (정책 B — 인스턴스 유지, expiresAt 만 갱신)
        private GameObject _activeShieldVFX;
        private float _shieldVFXExpiresAt;
        private GameObject _activeAttackSpeedAura;
        private float _attackSpeedAuraExpiresAt;
        private GameObject _activeMoveSpeedAura;
        private float _moveSpeedAuraExpiresAt;

        private Transform _playerTransform;

        private void Awake()
        {
            // CL-203: VFX 부착 대상 — playerHealth 가 플레이어에 있으니 그쪽 transform 사용. 없으면 self.
            _playerTransform = playerHealth != null ? playerHealth.transform : transform;
        }

        private void Update()
        {
            // CL-203: 활성 VFX 만료 체크 — duration 도달 시 페이드아웃 코루틴 시작.
            if (!_authority.IsAuthority) return;

            if (_activeShieldVFX != null && Time.time >= _shieldVFXExpiresAt)
            {
                StartCoroutine(FadeOutAndDestroy(_activeShieldVFX, _vfxFadeOutSeconds));
                _activeShieldVFX = null;
            }
            if (_activeAttackSpeedAura != null && Time.time >= _attackSpeedAuraExpiresAt)
            {
                StartCoroutine(FadeOutAndDestroy(_activeAttackSpeedAura, _vfxFadeOutSeconds));
                _activeAttackSpeedAura = null;
            }
            if (_activeMoveSpeedAura != null && Time.time >= _moveSpeedAuraExpiresAt)
            {
                StartCoroutine(FadeOutAndDestroy(_activeMoveSpeedAura, _vfxFadeOutSeconds));
                _activeMoveSpeedAura = null;
            }
        }

        private void OnEnable()
        {
            if (inventory != null)
            {
                inventory.OnRelicAcquired += HandleAcquired;
                inventory.OnCleared += HandleRunCleared;

                // 씬 전환 시 PlayerRunState 가 복구한 인벤토리에 대해 effect 를 재등록.
                // 첫 게임 시작 시 OwnedRelics 는 비어 있으므로 무동작. PlayerWallet 패턴 결과로
                // Player 가 매 씬 새로 스폰될 때 본 Registry 도 함께 새로 생성되므로 이 replay 가
                // 없으면 영속 모디파이어가 모두 풀린 상태로 진입한다.
                if (_authority.IsAuthority)
                {
                    IReadOnlyList<RelicData> owned = inventory.OwnedRelics;
                    for (int i = 0; i < owned.Count; i++)
                    {
                        RelicData relic = owned[i];
                        if (relic != null) HandleAcquired(relic);
                    }
                }
            }
            if (combatController != null) combatController.EnemyKilledByPlayer += HandleEnemyKilled;
            if (parryController != null) parryController.ParrySucceeded += HandleParrySuccess;
            if (dashController != null) dashController.OnDashEnded += HandleDashEnded;
        }

        private void OnDisable()
        {
            if (inventory != null)
            {
                inventory.OnRelicAcquired -= HandleAcquired;
                inventory.OnCleared -= HandleRunCleared;
            }
            if (combatController != null) combatController.EnemyKilledByPlayer -= HandleEnemyKilled;
            if (parryController != null) parryController.ParrySucceeded -= HandleParrySuccess;
            if (dashController != null) dashController.OnDashEnded -= HandleDashEnded;
        }

        private void HandleAcquired(RelicData relic)
        {
            if (!_authority.IsAuthority) return;
            if (relic == null || container == null) return;
            switch (relic.EffectType)
            {
                case RelicEffectType.AttackPowerPercent:
                    container.AddPermanent(StatId.AttackPower, relic.Magnitude, relic);
                    break;
                case RelicEffectType.FinisherDamagePercent:
                    container.AddPermanent(StatId.FinisherDamage, relic.Magnitude, relic);
                    break;
                case RelicEffectType.AttackPowerConditional:
                    float threshold = relic.Threshold;
                    container.AddConditional(
                        StatId.AttackPower, relic.Magnitude,
                        () => playerHealth != null && playerHealth.MaximumHealth > 0f
                              && playerHealth.CurrentHealth / playerHealth.MaximumHealth >= threshold,
                        relic);
                    break;
                case RelicEffectType.MoveSpeedPercent:
                    container.AddPermanent(StatId.MoveSpeed, relic.Magnitude, relic);
                    break;
                case RelicEffectType.MaxHealthPercent:
                    container.AddPermanent(StatId.MaxHealth, relic.Magnitude, relic);
                    break;
                case RelicEffectType.AttackSpeedOnKillTimed:
                    _onKillSubscriptions.Add(new OnKillSubscription
                    {
                        Magnitude = relic.Magnitude,
                        Duration = relic.Duration,
                        Source = relic,
                    });
                    break;

                // CL-108 Wave C
                case RelicEffectType.ShieldOnParry:
                    _onParrySuccessSubscriptions.Add(new TimedSubscription
                    {
                        Magnitude = relic.Magnitude,
                        Duration = relic.Duration,
                        Source = relic,
                    });
                    break;
                case RelicEffectType.HealReceivedPercent:
                    container.AddPermanent(StatId.HealReceived, relic.Magnitude, relic);
                    break;
                case RelicEffectType.DashCooldownPercent:
                    // 음수 magnitude (-0.12) 정상. PlayerStatModifierContainer 합산 정책상 1 + (-0.12) = 0.88.
                    container.AddPermanent(StatId.DashCooldown, relic.Magnitude, relic);
                    break;
                case RelicEffectType.MoveSpeedAfterDashTimed:
                    _onDashEndSubscriptions.Add(new TimedSubscription
                    {
                        Magnitude = relic.Magnitude,
                        Duration = relic.Duration,
                        Source = relic,
                    });
                    break;
                case RelicEffectType.HealConsumablePercent:
                    // CL-108 옵션 C: 회복약은 인벤토리 보관만. 사용 트리거는 PlayerHealing.UseConsumable
                    // (후속 CL 의 사용 UI 또는 본 CL 의 디버그 ContextMenu). 여기 case 는 no-op.
                    break;
                // None: Wave A 시점 미구현 효과 (비-MVP, 랜덤박스 등). 후속 ticket 분담.
            }
        }

        private void HandleEnemyKilled(KhiAttackRequest req, AttackStepData step, Health victim)
        {
            if (!_authority.IsAuthority) return;
            if (container == null) return;
            float maxDuration = 0f;
            for (int i = 0; i < _onKillSubscriptions.Count; i++)
            {
                OnKillSubscription s = _onKillSubscriptions[i];
                container.AddTimed(StatId.AttackSpeed, s.Magnitude, s.Duration, s.Source);
                if (s.Duration > maxDuration) maxDuration = s.Duration;
            }

            // CL-203: VFX (정책 B — 활성 시 expiresAt 만 갱신, 인스턴스 유지)
            if (_buffAuraVFXPrefab != null && _playerTransform != null && maxDuration > 0f)
            {
                if (_activeAttackSpeedAura == null)
                {
                    _activeAttackSpeedAura = VFXSpawner.SpawnAttached(
                        _buffAuraVFXPrefab, _playerTransform, new Vector3(0f, -0.5f, 0f));
                    ApplyAuraColor(_activeAttackSpeedAura, _attackSpeedAuraColor);
                }
                _attackSpeedAuraExpiresAt = Time.time + maxDuration;
            }

            if (_killBuffSfx != null && _onKillSubscriptions.Count > 0 && _playerTransform != null)
                AudioSource.PlayClipAtPoint(_killBuffSfx, _playerTransform.position, _sfxVolume);
        }

        private void HandleParrySuccess()
        {
            if (!_authority.IsAuthority) return;
            if (playerShield == null || playerHealth == null) return;
            float maxHp = playerHealth.MaximumHealth;
            float maxDuration = 0f;
            for (int i = 0; i < _onParrySuccessSubscriptions.Count; i++)
            {
                TimedSubscription s = _onParrySuccessSubscriptions[i];
                playerShield.GrantShield(maxHp * s.Magnitude, s.Duration);
                if (s.Duration > maxDuration) maxDuration = s.Duration;
            }

            // CL-203: VFX (정책 B)
            if (_shieldVFXPrefab != null && _playerTransform != null && maxDuration > 0f)
            {
                if (_activeShieldVFX == null)
                    _activeShieldVFX = VFXSpawner.SpawnAttached(_shieldVFXPrefab, _playerTransform);
                _shieldVFXExpiresAt = Time.time + maxDuration;
            }

            if (_shieldGrantedSfx != null && _onParrySuccessSubscriptions.Count > 0 && _playerTransform != null)
                AudioSource.PlayClipAtPoint(_shieldGrantedSfx, _playerTransform.position, _sfxVolume);
        }

        private void HandleDashEnded()
        {
            if (!_authority.IsAuthority) return;
            if (container == null) return;
            float maxDuration = 0f;
            for (int i = 0; i < _onDashEndSubscriptions.Count; i++)
            {
                TimedSubscription s = _onDashEndSubscriptions[i];
                container.AddTimed(StatId.MoveSpeed, s.Magnitude, s.Duration, s.Source);
                if (s.Duration > maxDuration) maxDuration = s.Duration;
            }

            // CL-203: VFX (정책 B)
            if (_buffAuraVFXPrefab != null && _playerTransform != null && maxDuration > 0f)
            {
                if (_activeMoveSpeedAura == null)
                {
                    _activeMoveSpeedAura = VFXSpawner.SpawnAttached(
                        _buffAuraVFXPrefab, _playerTransform, new Vector3(0f, -0.5f, 0f));
                    ApplyAuraColor(_activeMoveSpeedAura, _moveSpeedAuraColor);
                }
                _moveSpeedAuraExpiresAt = Time.time + maxDuration;
            }

            if (_dashBuffSfx != null && _onDashEndSubscriptions.Count > 0 && _playerTransform != null)
                AudioSource.PlayClipAtPoint(_dashBuffSfx, _playerTransform.position, _sfxVolume);
        }

        /// <summary>
        /// CL-109: PlayerRelicInventory.OnCleared 구독. Run 종료 시 모든 modifier/shield 일괄 정리.
        /// </summary>
        private void HandleRunCleared()
        {
            if (!_authority.IsAuthority) return;
            if (container != null) container.ClearAll();
            if (playerShield != null) playerShield.ClearShield();
            _onKillSubscriptions.Clear();
            _onParrySuccessSubscriptions.Clear();
            _onDashEndSubscriptions.Clear();

            // CL-203: 활성 VFX 즉시 정리 (페이드아웃 없음 — 스테이지 전환은 즉발)
            if (_activeShieldVFX != null) { Destroy(_activeShieldVFX); _activeShieldVFX = null; }
            if (_activeAttackSpeedAura != null) { Destroy(_activeAttackSpeedAura); _activeAttackSpeedAura = null; }
            if (_activeMoveSpeedAura != null) { Destroy(_activeMoveSpeedAura); _activeMoveSpeedAura = null; }
        }

        // ── CL-203 VFX 헬퍼 ────────────────────────────────

        /// <summary>
        /// BuffAuraVFX 의 모든 ParticleSystem startColor 를 일괄 변경 — 같은 prefab 으로 빨강/파랑 등 다른 색 인스턴스 생성용.
        /// </summary>
        private static void ApplyAuraColor(GameObject auraInstance, Color color)
        {
            if (auraInstance == null) return;
            ParticleSystem[] systems = auraInstance.GetComponentsInChildren<ParticleSystem>();
            foreach (ParticleSystem ps in systems)
            {
                if (ps == null) continue;
                ParticleSystem.MainModule main = ps.main;
                main.startColor = color;
            }
        }

        /// <summary>
        /// VFX 만료 시 페이드아웃 — ParticleSystem 은 emission 만 멈추고 자연 흩어짐, SpriteRenderer 는 알파 fade.
        /// CL-202 EnemyStatusEffect 패턴 동일.
        /// </summary>
        private IEnumerator FadeOutAndDestroy(GameObject vfx, float duration)
        {
            if (vfx == null) yield break;

            ParticleSystem[] systems = vfx.GetComponentsInChildren<ParticleSystem>();
            foreach (ParticleSystem ps in systems)
            {
                if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            SpriteRenderer[] renderers = vfx.GetComponentsInChildren<SpriteRenderer>();
            Color[] startColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) startColors[i] = renderers[i].color;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / duration);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    Color c = startColors[i];
                    renderers[i].color = new Color(c.r, c.g, c.b, c.a * k);
                }
                yield return null;
            }

            if (vfx != null) Destroy(vfx);
        }

        private struct OnKillSubscription
        {
            public float Magnitude;
            public float Duration;
            public RelicData Source;
        }

        private struct TimedSubscription
        {
            public float Magnitude;
            public float Duration;
            public RelicData Source;
        }
    }
}
