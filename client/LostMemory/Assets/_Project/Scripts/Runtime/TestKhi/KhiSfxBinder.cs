using LostMemory.Data;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// Khi 캐릭터의 SFX(효과음) 통합 바인더. 플레이어 루트에 부착.
    /// 컨트롤러 이벤트를 구독해 `AudioSource.PlayOneShot` 으로 짧은 SFX 를 재생한다.
    /// KhiCombatFeedbackBinder 와 책임 분리: 이쪽은 오디오만, 저쪽은 HitStop/Flash/Camera.
    ///
    /// AudioSource 를 직접 보유하는 이유:
    ///   `PlayClipAtPoint` 는 HitStop(Time.timeScale=0) 구간에서 사운드가 묻히는 케이스가 있음.
    ///   같은 GameObject 의 2D AudioSource(spatialBlend=0) 로 PlayOneShot 하면 timeScale/거리 영향 X.
    ///
    /// 클립이 null 인 슬롯은 자동 스킵되므로, Inspector 에서 원하는 것만 부분 할당해도 OK.
    /// 패링 성공 SFX 는 KhiParryFeedbackPresenter.successSfx 가 이미 처리하므로 이 컴포넌트에서는 다루지 않는다.
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Sfx Binder")]
    [DefaultExecutionOrder(260)]
    public class KhiSfxBinder : MonoBehaviour
    {
        [Header("Source Events (Awake 에서 자동 resolve 가능)")]
        [SerializeField] private KhiMeleeComboController meleeCombo;
        [SerializeField] private KhiParryController parry;
        [SerializeField] private KhiDashController dash;
        [SerializeField] private KhiHitStunController hitStun;
        [SerializeField] private KhiDownController down;

        [Header("Audio Source (없으면 자동 추가 — HitStop 영향 우회용)")]
        [Tooltip("비워두면 Awake 에서 자동 추가. spatialBlend=0 (2D) 로 설정되어 거리·timeScale 영향 없음.")]
        [SerializeField] private AudioSource audioSource;

        [Header("Attack — 휘두름 (콤보 step 1·2·3, 인덱스 = comboStep-1)")]
        [Tooltip("AttackStarted 시 재생. 배열 길이가 콤보 step 수보다 작으면 마지막 클립을 fallback. 추천: KoalaSword1, KoalaSword2, KoalaSword3.")]
        [SerializeField] private AudioClip[] attackSwingClips;
        [Range(0f, 1f)] [SerializeField] private float attackSwingVolume = 0.9f;

        [Header("Attack — 적 명중 (TargetHit)")]
        [Tooltip("적 명중 시 재생. 배열에 1개만 넣으면 항상 같은 클립, 여러 개면 랜덤. 추천: LoftImpact 단독.")]
        [SerializeField] private AudioClip[] hitImpactClips;
        [Range(0f, 1f)] [SerializeField] private float hitImpactVolume = 0.85f;

        [Header("Parry — 시작 / 성공 / 실패")]
        [Tooltip("ParryStarted 시 재생 (윈도우 진입 큐). 비워두면 무재생. 추천: LoftTinySelect.")]
        [SerializeField] private AudioClip parryStartClip;
        [Tooltip("ParrySucceeded 시 재생 (패링 성공). 비워두면 무재생. 추천: KoalaLoot. ※ KhiParryFeedbackPresenter.successSfx 와 중복되지 않도록 한쪽만 사용.")]
        [SerializeField] private AudioClip parrySuccessClip;
        [Tooltip("ParryFailed 시 재생. 추천: LoftBump.")]
        [SerializeField] private AudioClip parryFailClip;
        [Range(0f, 1f)] [SerializeField] private float parryVolume = 0.7f;

        [Header("Dash")]
        [Tooltip("DashStarted 시 재생. 추천: KoalaDash.")]
        [SerializeField] private AudioClip dashStartClip;
        [Range(0f, 1f)] [SerializeField] private float dashVolume = 0.8f;

        [Header("Hurt / Down / Revive")]
        [Tooltip("HitStunStarted(피격) 시 랜덤 재생. 추천: KoalaHurt1~3.")]
        [SerializeField] private AudioClip[] hurtClips;
        [Range(0f, 1f)] [SerializeField] private float hurtVolume = 0.85f;

        [Tooltip("DownEntered 시 재생. 추천: LoftDeath.")]
        [SerializeField] private AudioClip downEnterClip;
        [Tooltip("ReviveStarted 시 재생. 추천: LoftMechanism.")]
        [SerializeField] private AudioClip reviveStartClip;
        [Tooltip("ReviveCompleted 시 재생. 추천: MMInterface/Ding 또는 KoalaLoot.")]
        [SerializeField] private AudioClip reviveCompleteClip;
        [Tooltip("DefeatedByTimeout(패배) 시 재생. 추천: LoftDeath.")]
        [SerializeField] private AudioClip defeatClip;
        [Range(0f, 1f)] [SerializeField] private float downVolume = 0.9f;

        [Header("Debug")]
        [SerializeField] private bool logSfx = false;

        private void Awake()
        {
            meleeCombo ??= GetComponent<KhiMeleeComboController>();
            parry      ??= GetComponent<KhiParryController>();
            dash       ??= GetComponent<KhiDashController>();
            hitStun    ??= GetComponent<KhiHitStunController>();
            down       ??= GetComponent<KhiDownController>();

            // AudioSource 자동 확보. PlayOneShot 은 HitStop(Time.timeScale=0) 에서도 정상 재생.
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D — 카메라 거리·HitStop 영향 X
        }

        private void OnEnable()
        {
            if (meleeCombo != null)
            {
                meleeCombo.AttackStarted += HandleAttackStarted;
                meleeCombo.TargetHit     += HandleTargetHit;
                // EnemyKilledByPlayer 는 적 측 사망 SFX 가 처리하므로 여기서 구독 X.
            }

            if (parry != null)
            {
                parry.ParryStarted   += HandleParryStarted;
                parry.ParrySucceeded += HandleParrySucceeded;
                parry.ParryFailed    += HandleParryFailed;
            }

            if (dash != null)
            {
                dash.DashStarted += HandleDashStarted;
            }

            if (hitStun != null)
            {
                hitStun.HitStunStarted += HandleHitStunStarted;
            }

            if (down != null)
            {
                down.DownEntered       += HandleDownEntered;
                down.ReviveStarted     += HandleReviveStarted;
                down.ReviveCompleted   += HandleReviveCompleted;
                down.DefeatedByTimeout += HandleDefeated;
            }
        }

        private void OnDisable()
        {
            if (meleeCombo != null)
            {
                meleeCombo.AttackStarted -= HandleAttackStarted;
                meleeCombo.TargetHit     -= HandleTargetHit;
            }

            if (parry != null)
            {
                parry.ParryStarted   -= HandleParryStarted;
                parry.ParrySucceeded -= HandleParrySucceeded;
                parry.ParryFailed    -= HandleParryFailed;
            }

            if (dash != null)
            {
                dash.DashStarted -= HandleDashStarted;
            }

            if (hitStun != null)
            {
                hitStun.HitStunStarted -= HandleHitStunStarted;
            }

            if (down != null)
            {
                down.DownEntered       -= HandleDownEntered;
                down.ReviveStarted     -= HandleReviveStarted;
                down.ReviveCompleted   -= HandleReviveCompleted;
                down.DefeatedByTimeout -= HandleDefeated;
            }
        }

        // ─── Handlers ────────────────────────────────────────────────────────

        private void HandleAttackStarted(KhiAttackRequest _, AttackStepData step)
        {
            if (attackSwingClips == null || attackSwingClips.Length == 0 || step == null)
            {
                return;
            }

            // comboStep 은 1-based. 배열 길이 초과 시 마지막 클립 fallback.
            int index = Mathf.Clamp(step.comboStep - 1, 0, attackSwingClips.Length - 1);
            Play(attackSwingClips[index], attackSwingVolume, $"AttackSwing(combo={step.comboStep})");
        }

        private void HandleTargetHit(KhiAttackRequest _, AttackStepData __, Health ___)
        {
            // 콤보 step 무관 — 모든 명중에 동일 클립(또는 배열 랜덤) 재생.
            PlayRandom(hitImpactClips, hitImpactVolume, "HitImpact");
        }

        private void HandleParryStarted()       => Play(parryStartClip,   parryVolume, "ParryStart");
        private void HandleParrySucceeded()     => Play(parrySuccessClip, parryVolume, "ParrySuccess");
        private void HandleParryFailed()        => Play(parryFailClip,    parryVolume, "ParryFail");
        private void HandleDashStarted()        => Play(dashStartClip,   dashVolume,  "DashStart");
        private void HandleHitStunStarted(float _) => PlayRandom(hurtClips, hurtVolume, "Hurt");
        private void HandleDownEntered(float _) => Play(downEnterClip,     downVolume, "DownEnter");
        private void HandleReviveStarted(GameObject _) => Play(reviveStartClip, downVolume, "ReviveStart");
        private void HandleReviveCompleted(GameObject _) => Play(reviveCompleteClip, downVolume, "ReviveComplete");
        private void HandleDefeated()           => Play(defeatClip, downVolume, "Defeated");

        // ─── Playback ────────────────────────────────────────────────────────

        private void Play(AudioClip clip, float volume, string tag)
        {
            if (clip == null || audioSource == null)
            {
                return;
            }

            // AudioSource.PlayOneShot 은 Time.timeScale=0 (HitStop) 에서도 정상 재생됨.
            audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
            Log(tag, clip);
        }

        private void PlayRandom(AudioClip[] clips, float volume, string tag)
        {
            if (clips == null || clips.Length == 0)
            {
                return;
            }

            AudioClip pick = clips[Random.Range(0, clips.Length)];
            Play(pick, volume, tag);
        }

        private void Log(string tag, AudioClip clip)
        {
            if (!logSfx || clip == null)
            {
                return;
            }

            Debug.Log($"[KhiSfx] {tag} → {clip.name} t={Time.time:F3}");
        }
    }
}
