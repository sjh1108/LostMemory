using LostMemory.Combat;
using LostMemory.Relics;
using MoreMountains.TopDownEngine;
using Unity.Netcode;
using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// 시연용 자동 포션 사용 컴포넌트.
    ///
    /// HP 가 임계값(hpThresholdPercent) 이하로 떨어지면 단축바 슬롯 1→2→3→4 순서로 첫 회복약을 자동 사용.
    /// 발표/시연 중 보스 패턴을 안정적으로 보여주기 위한 보조 기능 — 정식 빌드에서는 autoEnabled=false 또는 컴포넌트 제거 권장.
    ///
    /// 멀티: NetworkObject.IsOwner 만 발동 → 호스트/게스트 각자 자기 캐릭터에만 적용. 솔로(NGO 미스폰)면 항상 통과.
    /// HP sync 는 PlayerHealthSync 가 알아서 처리 — 본 컴포넌트는 그저 owner-side 에서 TryUseConsumable 호출.
    ///
    /// 부착: Player prefab 의 root (Health / PlayerHealing / PlayerConsumableInventory / NetworkObject 가 있는 자리).
    /// 의존 컴포넌트는 Awake 에서 GetComponentInParent 로 자동 해결 — 인스펙터 wireup 불필요.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Test Khi/Auto Potion Controller")]
    public sealed class AutoPotionController : MonoBehaviour
    {
        [Header("Demo")]
        [Tooltip("시연용 자동 사용 활성. 인스펙터 기본 off. 런타임 토글 키로 on/off 가능.")]
        [SerializeField] private bool autoEnabled = false;
        [Tooltip("자동 사용 on/off 토글 키.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F8;
        [Tooltip("HP / MaxHP 비율이 이 값 미만이면 자동 사용. 0.4 = 40%.")]
        [SerializeField, Range(0f, 1f)] private float hpThresholdPercent = 0.4f;
        [Tooltip("같은 프레임/짧은 간격 중복 사용 방지용 안전벨트(초). unscaledTime 기준.")]
        [SerializeField, Min(0f)] private float internalCooldownSeconds = 0.25f;
        [SerializeField] private bool verboseLog = false;

        [Header("Dependencies (Auto-resolved if null)")]
        [SerializeField] private Health health;
        [SerializeField] private PlayerHealing playerHealing;
        [SerializeField] private PlayerConsumableInventory inventory;
        [SerializeField] private KhiDownController downController;
        [SerializeField] private NetworkObject networkObject;

        private float _nextAllowedAt;

        private void Awake()
        {
            if (health == null) health = GetComponentInParent<Health>();
            if (playerHealing == null) playerHealing = GetComponentInParent<PlayerHealing>();
            if (inventory == null) inventory = GetComponentInParent<PlayerConsumableInventory>();
            if (downController == null) downController = GetComponentInParent<KhiDownController>();
            if (networkObject == null) networkObject = GetComponentInParent<NetworkObject>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                autoEnabled = !autoEnabled;
                if (verboseLog) Debug.Log($"[AutoPotion] toggled → {(autoEnabled ? "ON" : "OFF")}", this);
            }
            if (!autoEnabled) return;

            // 멀티: 자기 owner-side 에서만 발동. 솔로(NGO 미스폰)는 통과.
            if (networkObject != null && networkObject.IsSpawned && !networkObject.IsOwner) return;

            if (Time.unscaledTime < _nextAllowedAt) return;

            // Down / Defeated 등 행동 차단 상태에서는 포션 사용 불가 (ShortcutBarPresenter 와 동일 정책).
            if (KhiPlayerActionGate.IsBlocked(downController)) return;

            if (health == null || health.MaximumHealth <= 0f) return;
            float ratio = health.CurrentHealth / health.MaximumHealth;
            if (ratio >= hpThresholdPercent) return;

            if (inventory == null || playerHealing == null) return;
            for (int i = 0; i < PlayerConsumableInventory.SlotCount; i++)
            {
                RelicData c = inventory.Get(i);
                if (c == null || !c.IsConsumable) continue;

                // EffectType==HealConsumablePercent 검증은 PlayerHealing.TryUseConsumable 내부에서 처리 — 여기선 위임.
                if (playerHealing.TryUseConsumable(c))
                {
                    inventory.Remove(i);
                    _nextAllowedAt = Time.unscaledTime + internalCooldownSeconds;
                    if (verboseLog) Debug.Log($"[AutoPotion] used slot {i} ({c.name}) at HP {health.CurrentHealth:F0}/{health.MaximumHealth:F0} ({ratio:P0})", this);
                    return;
                }
            }
        }
    }
}
