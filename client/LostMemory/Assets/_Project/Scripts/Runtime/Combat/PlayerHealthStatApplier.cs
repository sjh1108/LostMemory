using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Combat
{
    /// <summary>
    /// CL-107: PlayerStatModifierContainer 의 MaxHealth multiplier 변경 시 TDE Health 의
    /// MaximumHealth 재계산 + CurrentHealth 비례 증가 (수호의 파편 등). CL-107 결정 #3.
    /// 정책: MaxHP × ratio 유지. 예) MaxHP 100→112 + CurrentHP 50 → CurrentHP 56 (50% 비율 유지).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Player Health Stat Applier")]
    public sealed class PlayerHealthStatApplier : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private PlayerStatModifierContainer container;
        [SerializeField] private bool _logChanges = true;

        private float _baseMaxHealth;
        private float _lastAppliedMul = 1f;
        private bool _baseCaptured;

        private void Awake()
        {
            // modifier 적용 전 값을 _baseMaxHealth 로 캡처 — 이후 multiplier 변경 시 base × mul 로 계산.
            ResolveRefs();
            CaptureBaseIfNeeded();
        }

        private void OnEnable()
        {
            ResolveRefs();
            CaptureBaseIfNeeded();
            if (container == null)
            {
                Debug.LogWarning("[PlayerHealthStatApplier] container 미할당 — MaxHealth modifier 적용 안 됨.", this);
                return;
            }
            container.ModifiersChanged += HandleChanged;
            // 이미 다른 시스템(TalentStartupApplier 등)이 container 에 modifier 를 등록한 상태일 수도 있음.
            // OnEnable 직후 한 번 동기화.
            HandleChanged(default);
        }

        private void OnDisable()
        {
            if (container != null) container.ModifiersChanged -= HandleChanged;
        }

        private void ResolveRefs()
        {
            if (health == null) health = GetComponent<Health>() ?? GetComponentInChildren<Health>(true) ?? GetComponentInParent<Health>();
            if (container == null) container = GetComponent<PlayerStatModifierContainer>() ?? GetComponentInChildren<PlayerStatModifierContainer>(true) ?? GetComponentInParent<PlayerStatModifierContainer>();
            if (container == null) container = FindAnyObjectByType<PlayerStatModifierContainer>();
        }

        private void CaptureBaseIfNeeded()
        {
            if (_baseCaptured) return;
            if (health == null) return;
            // Awake 순서가 보장되지 않아 TalentStartupApplier 가 이미 modifier 를 등록했을 수 있다.
            // 그 경우 health.MaximumHealth 는 base * mul 상태. mul 로 역산해서 진짜 base 추출 — 이중 적용 방지.
            float currentMul = container != null ? container.GetTotalMultiplier(StatId.MaxHealth) : 1f;
            _baseMaxHealth = currentMul > 0f ? health.MaximumHealth / currentMul : health.MaximumHealth;
            _lastAppliedMul = currentMul;
            _baseCaptured = true;
            if (_logChanges)
                Debug.Log($"[PlayerHealthStatApplier] base MaxHealth captured = {_baseMaxHealth:F0} (currentMul={currentMul:F2}, observedMax={health.MaximumHealth:F0})", this);
        }

        private void HandleChanged(StatId stat)
        {
            if (health == null || container == null) return;
            CaptureBaseIfNeeded();

            float newMul = container.GetTotalMultiplier(StatId.MaxHealth);
            if (Mathf.Approximately(newMul, _lastAppliedMul)) return;

            float oldMax = health.MaximumHealth;
            float ratio = oldMax > 0f ? health.CurrentHealth / oldMax : 1f;
            float newMax = _baseMaxHealth * newMul;
            health.MaximumHealth = newMax;
            health.SetHealth(newMax * ratio);
            _lastAppliedMul = newMul;

            if (_logChanges)
                Debug.Log($"[PlayerHealthStatApplier] MaxHealth {oldMax:F0} → {newMax:F0} (mul {newMul:F2}, base {_baseMaxHealth:F0})", this);
        }
    }
}
