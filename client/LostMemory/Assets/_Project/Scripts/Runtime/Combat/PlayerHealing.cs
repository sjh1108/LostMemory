using LostMemory.Relics;
using LostMemory.VFX;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Combat
{
    /// <summary>
    /// CL-108: 회복 진입점. HealReceived multiplier 자동 적용.
    /// 모든 회복 (회복약 / 부활 / 향후 시간 회복 / 이벤트 회복) 은 본 컴포넌트의 Heal API 거침 (결정 #7).
    /// 회복약 사용 시점 = 옵션 C (보관만, 사용 UI 후속) — 본 CL 은 디버그 ContextMenu 로만 검증.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Player Healing")]
    public sealed class PlayerHealing : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private PlayerStatModifierContainer container;
        [SerializeField] private bool logHeals = false;

        [Header("Debug (CL-108 검증용 — 옵션 C 의 사용 트리거)")]
        [Tooltip("ContextMenu 'Use assigned consumable' 누르면 이 RelicData 의 효과 발동. CL-110 후속에서 정식 UI 로 대체.")]
        [SerializeField] private RelicData _debugConsumableToUse;

        [Header("VFX / SFX (CL-203)")]
        [SerializeField] private GameObject _healVFXPrefab;
        [SerializeField] private AudioClip _healSfx;
        [SerializeField, Range(0f, 1f)] private float _sfxVolume = 0.6f;

        private void Awake()
        {
            if (health == null) health = GetComponent<Health>();
            if (container == null) container = GetComponent<PlayerStatModifierContainer>();
        }

        /// <summary>
        /// 기본 회복량을 받아 HealReceived multiplier 적용 후 Health.ReceiveHealth 호출.
        /// 모든 회복 경로의 단일 진입점.
        /// </summary>
        public void Heal(float baseAmount, object source)
        {
            if (health == null || baseAmount <= 0f) return;
            float mul = container != null ? container.GetTotalMultiplier(StatId.HealReceived) : 1f;
            float effective = baseAmount * mul;
            health.ReceiveHealth(effective, source as GameObject ?? gameObject);
            if (logHeals)
            {
                Debug.Log($"[PlayerHealing] Heal base={baseAmount:F1} mul={mul:F3} -> {effective:F1} src={source}");
            }
        }

        /// <summary>
        /// 회복약 소모 사용. HealConsumablePercent 만 처리. Magnitude 는 maxHp 비율.
        /// </summary>
        public void UseConsumable(RelicData consumable)
        {
            if (consumable == null || !consumable.IsConsumable) return;
            if (consumable.EffectType != RelicEffectType.HealConsumablePercent) return;
            float baseAmount = (health != null ? health.MaximumHealth : 0f) * consumable.Magnitude;
            Heal(baseAmount, consumable);

            // CL-203: 회복 VFX (1회성, 1.5초 후 자동 destroy)
            if (_healVFXPrefab != null)
                VFXSpawner.Spawn(_healVFXPrefab, transform.position, Quaternion.identity, 1.5f);

            if (_healSfx != null)
                AudioSource.PlayClipAtPoint(_healSfx, transform.position, _sfxVolume);
        }

        [ContextMenu("Debug — Use assigned consumable")]
        private void DebugUseAssignedConsumable()
        {
            if (_debugConsumableToUse == null)
            {
                Debug.LogWarning("[PlayerHealing] _debugConsumableToUse 가 비어있음. Inspector 에서 RelicData 드래그.");
                return;
            }
            UseConsumable(_debugConsumableToUse);
        }
    }
}
