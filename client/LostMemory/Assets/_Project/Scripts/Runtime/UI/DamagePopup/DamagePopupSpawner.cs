using MoreMountains.Feedbacks;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.UI
{
    /// <summary>
    /// 플레이어가 입힌 데미지를 월드 floating text 로 띄우는 중앙 디스패처.
    /// TDE MMFloatingTextSpawner 가 같은 채널로 씬에 1개 있어야 동작 (이 컴포넌트는 trigger 만).
    /// 본인 데미지만 표시 — 호출 측이 이미 본인 흐름에서만 부르므로 owner 체크 없음.
    /// </summary>
    [AddComponentMenu("Lost Memory/UI/Damage Popup Spawner")]
    [DefaultExecutionOrder(-50)]
    public class DamagePopupSpawner : MonoBehaviour
    {
        public static DamagePopupSpawner Instance { get; private set; }

        [Header("Style")]
        [SerializeField] private DamagePopupStyle style;

        [Tooltip("같이 배치된 MMFloatingTextSpawner. riseDistance(SO) 를 RemapYOne 으로 전파한다. " +
            "null 이면 같은 GameObject 에서 GetComponent 시도.")]
        [SerializeField] private MMFloatingTextSpawner floatingTextSpawner;

        /// <summary>외부(OutlineMirror 등) 가 SO 값을 읽기 위한 read-only getter.</summary>
        public DamagePopupStyle Style => style;

        [Header("Categories")]
        [Tooltip("KhiMeleeComboController 평타 hit 시 popup 표시.")]
        [SerializeField] private bool showMelee = true;
        [Tooltip("KhiArrowProjectile 명중 시 popup 표시.")]
        [SerializeField] private bool showArrow = true;
        [Tooltip("OnHitEffectRegistry 체인/풍속/화상/도트 데미지 시 popup 표시. 기본 OFF (시끄러움).")]
        [SerializeField] private bool showSubEffect = false;

        [Header("Lifetime")]
        [Tooltip("DontDestroyOnLoad 적용. 씬 전환 후에도 단일 인스턴스 유지.")]
        [SerializeField] private bool persistAcrossScenes = true;

        [Header("Debug")]
        [SerializeField] private bool logTriggers = false;

        private MMChannelData _channelData;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (persistAcrossScenes && transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            if (floatingTextSpawner == null)
            {
                floatingTextSpawner = GetComponent<MMFloatingTextSpawner>();
            }

            RefreshChannelData();
            ApplyMotionToSpawner();
        }

        private void OnValidate()
        {
            RefreshChannelData();
            ApplyMotionToSpawner();
        }

        private void LateUpdate()
        {
            // SO 의 riseDistance 변경을 다음 popup 부터 즉시 반영하기 위해 매 LateUpdate 적용.
            // 같은 값이면 no-op. 비용 미미.
            ApplyMotionToSpawner();
        }

        private void ApplyMotionToSpawner()
        {
            if (floatingTextSpawner == null || style == null) return;
            float d = style.riseDistance;
            Vector2 target = new Vector2(d, d);
            if (floatingTextSpawner.RemapYOne != target)
            {
                floatingTextSpawner.RemapYOne = target;
            }
            if (floatingTextSpawner.RemapYZero != Vector2.zero)
            {
                floatingTextSpawner.RemapYZero = Vector2.zero;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void RefreshChannelData()
        {
            int channelId = style != null ? style.channel : 0;
            _channelData = new MMChannelData(MMChannelModes.Int, channelId, null);
        }

        // ── 외부 API ────────────────────────────────────────

        public void NotifyMeleeDamage(Health target, float amount, bool isCritical)
        {
            if (!showMelee) return;
            TriggerInternal(target, amount, isCritical ? Category.Critical : Category.Normal);
        }

        public void NotifyArrowDamage(Health target, float amount, bool isCritical)
        {
            if (!showArrow) return;
            TriggerInternal(target, amount, isCritical ? Category.Critical : Category.Normal);
        }

        public void NotifySubEffectDamage(Health target, float amount, bool isCritical = false)
        {
            if (!showSubEffect) return;
            // 보조효과도 크리티컬 가능 — critical 발동 시 일반 Critical 카테고리 사용 (노란 + 라벨).
            TriggerInternal(target, amount, isCritical ? Category.Critical : Category.SubEffect);
        }

        // ── 내부 ────────────────────────────────────────────

        private enum Category { Normal, Critical, SubEffect }

        private void TriggerInternal(Health target, float amount, Category category)
        {
            if (target == null || style == null) return;
            if (amount <= 0f) return;

            Vector3 pos = target.transform.position + style.spawnOffset;
            string text = BuildText(amount, category);
            Color color = ResolveColor(category);
            float intensity = ResolveIntensity(category);
            Gradient gradient = BuildGradient(color);

            MMFloatingTextSpawnEvent.Trigger(
                _channelData,
                pos,
                text,
                style.direction,
                intensity,
                forceLifetime: true,
                lifetime: style.lifetime,
                forceColor: true,
                animateColorGradient: gradient,
                useUnscaledTime: style.useUnscaledTime);

            if (logTriggers)
            {
                Debug.Log($"[DamagePopup] {category} {amount:0.##} -> {target.name} at {pos}", this);
            }
        }

        private string BuildText(float amount, Category category)
        {
            string number = amount.ToString(style.numberFormat);
            if (category == Category.Critical && !string.IsNullOrEmpty(style.criticalPrefix))
            {
                return style.criticalPrefix + number;
            }
            return number;
        }

        private Color ResolveColor(Category category) => category switch
        {
            Category.Critical => style.criticalColor,
            Category.SubEffect => style.subEffectColor,
            _ => style.normalColor,
        };

        private float ResolveIntensity(Category category) => category switch
        {
            Category.Critical => style.criticalIntensity,
            Category.SubEffect => style.subEffectIntensity,
            _ => style.normalIntensity,
        };

        private static Gradient BuildGradient(Color color)
        {
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            return g;
        }
    }
}
