using LostMemory.Data;
using LostMemory.UI;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// CL-017 전투 피드백 오케스트레이터. 플레이어 루트에 부착.
    /// Khi 컨트롤러 이벤트 → HitStop + Flash + Camera Impulse 로 팬아웃.
    /// 모든 튜닝 파라미터(duration/color/intensity)는 Inspector 단일 진실 소스.
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Combat Feedback Binder")]
    [DefaultExecutionOrder(250)]
    public class KhiCombatFeedbackBinder : MonoBehaviour
    {
        [Header("Source Events (Awake에서 자동 resolve 가능)")]
        [SerializeField] private KhiHitStunController hitStun;
        [SerializeField] private KhiParryController parry;
        [SerializeField] private KhiMeleeComboController meleeCombo;

        [Header("Feedback Systems")]
        [SerializeField] private KhiHitFlashPresenter flashPresenter;
        [SerializeField] private KhiPlayerCamera playerCamera;
        [Tooltip("CL-234 (A-10): 피격 시 화면 가장자리 선혈 vignette. null 이면 비활성.")]
        [SerializeField] private HitVignetteOverlay hitVignette;

        [Header("HitStunStarted (player 피격)")]
        [SerializeField, Min(0f)] private float hitStopOnHit = 0.06f;
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField, Min(0f)] private float hitFlashDuration = 0.12f;
        [SerializeField, Min(0f)] private float hitShakeIntensity = 0.15f;
        [SerializeField, Min(0f)] private float hitShakeDuration = 0.12f;

        [Header("ParrySucceeded")]
        [SerializeField, Min(0f)] private float hitStopOnParry = 0.10f;
        [SerializeField] private Color parryFlashColor = new Color(0.5f, 0.85f, 1f);
        [SerializeField, Min(0f)] private float parryFlashDuration = 0.15f;
        [SerializeField, Min(0f)] private float parryShakeIntensity = 0.14f;
        [SerializeField, Min(0f)] private float parryShakeDuration = 0.10f;

        [Header("TargetHit (플레이어 공격 적중)")]
        [SerializeField, Min(0f)] private float hitStopOnLanding = 0.04f;
        [SerializeField, Min(0f)] private float landingShakeIntensity = 0.05f;
        [SerializeField, Min(0f)] private float landingShakeDuration = 0.08f;
        [SerializeField] private bool flashOnLanding = false;
        [SerializeField] private Color landingFlashColor = new Color(1f, 0.95f, 0.7f);
        [SerializeField, Min(0f)] private float landingFlashDuration = 0.08f;

        [Header("Finisher Hit (3타 적중 전용 — 기본 TargetHit 대신 적용)")]
        [SerializeField] private bool enableFinisherFeedback = true;
        [SerializeField] private int finisherComboStep = 3;
        [SerializeField, Min(0f)] private float finisherHitStop = 0.08f;
        [SerializeField, Min(0f)] private float finisherShakeIntensity = 0.28f;
        [SerializeField, Min(0f)] private float finisherShakeDuration = 0.18f;

        [Header("Debug")]
        [SerializeField] private bool logFeedback = false;

        private void Awake()
        {
            hitStun ??= GetComponent<KhiHitStunController>();
            parry ??= GetComponent<KhiParryController>();
            meleeCombo ??= GetComponent<KhiMeleeComboController>();
            flashPresenter ??= GetComponent<KhiHitFlashPresenter>() ?? GetComponentInChildren<KhiHitFlashPresenter>();

            if (playerCamera == null)
            {
                Camera main = Camera.main;
                if (main != null)
                {
                    playerCamera = main.GetComponent<KhiPlayerCamera>();
                }
            }
        }

        private void OnEnable()
        {
            if (hitStun != null)
            {
                hitStun.HitStunStarted += HandleHitStun;
            }

            if (parry != null)
            {
                parry.ParrySucceeded += HandleParrySucceeded;
            }

            if (meleeCombo != null)
            {
                meleeCombo.TargetHit += HandleTargetHit;
            }
        }

        private void OnDisable()
        {
            if (hitStun != null)
            {
                hitStun.HitStunStarted -= HandleHitStun;
            }

            if (parry != null)
            {
                parry.ParrySucceeded -= HandleParrySucceeded;
            }

            if (meleeCombo != null)
            {
                meleeCombo.TargetHit -= HandleTargetHit;
            }
        }

        private void HandleHitStun(float _)
        {
            RequestFreeze(hitStopOnHit);
            flashPresenter?.Flash(hitFlashColor, hitFlashDuration);
            playerCamera?.ApplyImpulse(hitShakeIntensity, hitShakeDuration);
            hitVignette?.Pulse();   // CL-234 (A-10): 화면 가장자리 선혈 펄스.
            Log("HitStun feedback");
        }

        private void HandleParrySucceeded()
        {
            RequestFreeze(hitStopOnParry);
            flashPresenter?.Flash(parryFlashColor, parryFlashDuration);
            playerCamera?.ApplyImpulse(parryShakeIntensity, parryShakeDuration);
            Log("ParrySucceeded feedback");
        }

        private void HandleTargetHit(KhiAttackRequest _, AttackStepData step, Health ___)
        {
            if (enableFinisherFeedback && step != null && step.comboStep == finisherComboStep)
            {
                RequestFreeze(finisherHitStop);
                playerCamera?.ApplyImpulse(finisherShakeIntensity, finisherShakeDuration);
                if (flashOnLanding)
                {
                    flashPresenter?.Flash(landingFlashColor, landingFlashDuration);
                }
                Log($"Finisher({finisherComboStep}) hit feedback");
                return;
            }

            RequestFreeze(hitStopOnLanding);
            playerCamera?.ApplyImpulse(landingShakeIntensity, landingShakeDuration);

            if (flashOnLanding)
            {
                flashPresenter?.Flash(landingFlashColor, landingFlashDuration);
            }

            Log("TargetHit feedback");
        }

        private static void RequestFreeze(float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            KhiHitStopController hitStop = KhiHitStopController.Instance;
            if (hitStop == null)
            {
                return;
            }

            hitStop.RequestFreeze(duration);
        }

        private void Log(string msg)
        {
            if (!logFeedback)
            {
                return;
            }

            Debug.Log($"[KhiFeedback] {msg} t={Time.time:F3}");
        }
    }
}
