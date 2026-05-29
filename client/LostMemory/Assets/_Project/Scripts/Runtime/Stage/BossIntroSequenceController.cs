using System;
using System.Collections;
using LostMemory.Audio;
using LostMemory.Combat;
using LostMemory.Networking.Player;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using Unity.Netcode;
using UnityEngine;

namespace LostMemory.Stage
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Stage/Boss Intro Sequence Controller")]
    public sealed class BossIntroSequenceController : MonoBehaviour
    {
        [SerializeField] private BossIntroSequenceData sequenceData;
        [SerializeField] private Animator bossAnimator;
        [SerializeField] private string entryTriggerName = string.Empty;
        [SerializeField] private string entryStateName = "Entry";
        [SerializeField, Min(0)] private int entryStateLayer;

        [Header("Entry Motion")]
        [SerializeField] private Transform entryMotionRoot;
        [SerializeField] private bool moveEntryFromOffset;
        [SerializeField] private Vector3 entryStartLocalOffset = new Vector3(0f, 6f, 0f);
        [SerializeField, Min(0f)] private float entryMoveDuration = 0.9f;

        [SerializeField] private GameObject[] introVisibilityTargets = System.Array.Empty<GameObject>();
        [SerializeField] private GameObject dialoguePlayerOwner;
        [SerializeField] private Health bossHealth;
        [SerializeField] private bool makeBossInvulnerableDuringIntro = true;
        [SerializeField] private CombatTargetable bossTargetable;
        [SerializeField] private bool makeBossUntargetableDuringIntro = true;
        [SerializeField] private bool protectBossBeforeIntroStarts = true;

        [Header("Boss Start BGM (optional)")]
        [SerializeField] private AudioClip bossStartBgmClip;
        [SerializeField] private int bossStartBgmId = StageBgmPlayer.Stage2BossBgmId;
        [SerializeField, Range(0f, 2f)] private float bossStartBgmVolume = 1f;

        [SerializeField] private bool debugLogging;

        private Coroutine _introRoutine;
        private Coroutine _entryLandingSfxRoutine;
        private Coroutine _entryMotionRoutine;
        private Character[] _cachedPlayers = System.Array.Empty<Character>();
        private BossRoomTransitionCompletedContext _currentContext;
        private Transform _activeEntryMotionRoot;
        private Vector3 _entryMotionStartLocalPosition;
        private Vector3 _entryMotionEndLocalPosition;
        private bool _isIntroRunning;
        private bool _introCompleted;
        private bool _entryMotionPrepared;
        private bool _introInvulnerabilityApplied;
        private bool _bossWasInvulnerable;
        private bool _introTargetabilityApplied;
        private bool _bossWasTargetable;

        public event Action<BossRoomTransitionCompletedContext> IntroStarted;
        public event Action<BossRoomTransitionCompletedContext> IntroCompleted;

        public bool IsIntroRunning => _isIntroRunning;
        public BossRoomTransitionCompletedContext CurrentContext => _currentContext;
        public Animator BossAnimator => bossAnimator;

        private void Reset()
        {
            bossAnimator = ResolveBossAnimator();
            bossHealth = ResolveBossHealth();
            bossTargetable = ResolveBossTargetable(createIfMissing: false);
            AutoAssignVisibilityTargets();
            dialoguePlayerOwner = gameObject;
        }

        private void OnEnable()
        {
            bossAnimator ??= ResolveBossAnimator();
            bossHealth ??= ResolveBossHealth();
            bossTargetable ??= ResolveBossTargetable(createIfMissing: false);
            AutoAssignVisibilityTargets();
            ApplyPreIntroProtection();

            if (!_isIntroRunning && sequenceData != null && sequenceData.HideBossBeforeEntry)
            {
                SetIntroVisibility(isVisible: false);
            }
        }

        private void Start()
        {
            ApplyPreIntroProtection();
        }

        private void OnDisable()
        {
            StopActiveIntro(unfreezePlayers: true);
            SetIntroVisibility(isVisible: true);
        }

        public void BeginIntro(BossRoomTransitionCompletedContext context)
        {
            if (_isIntroRunning)
            {
                Log("Ignored duplicate intro start request.");
                return;
            }

            AutoAssignVisibilityTargets();
            _currentContext = context;
            _cachedPlayers = CopyPlayers(context.Players);
            _introCompleted = false;
            _introRoutine = StartCoroutine(RunIntroSequence(context));
        }

        private IEnumerator RunIntroSequence(BossRoomTransitionCompletedContext context)
        {
            _isIntroRunning = true;
            ApplyIntroInvulnerability();
            ApplyIntroTargetability();
            IntroStarted?.Invoke(context);
            Debug.Log($"[BossIntro] step=Start t={Time.time:F2} cachedPlayers={_cachedPlayers?.Length ?? 0}", this);
            Log("Boss intro sequence started.");

            PlayBossStartBgm();

            BossIntroSequenceData data = sequenceData;
            bool shouldLockPlayers = data == null || data.LockPlayersDuringIntro;
            bool shouldUnlockPlayers = data == null || data.UnlockPlayersOnComplete;

            if (data == null)
            {
                SetIntroVisibility(isVisible: true);
                Log("BossIntroSequenceData is not assigned. Running fallback intro flow.");
            }

            if (shouldLockPlayers)
            {
                FreezeCachedPlayers();
            }
            else
            {
                UnfreezeCachedPlayers();
            }

            if (data != null)
            {
                if (data.HideBossBeforeEntry)
                {
                    SetIntroVisibility(isVisible: false);
                }
                else
                {
                    SetIntroVisibility(isVisible: true);
                }

                if (data.DelayBeforeEntry > 0f)
                {
                    yield return new WaitForSecondsRealtime(data.DelayBeforeEntry);
                }

                PrepareEntryMotion();

                if (data.HideBossBeforeEntry)
                {
                    SetIntroVisibility(isVisible: true);
                }

                PlayEntryLandingSfx(data);

                if (data.PlayEntryAnimation)
                {
                    PlayEntryAnimation();
                    StartEntryMotion(data.EntryDuration);
                    if (data.EntryDuration > 0f)
                    {
                        yield return new WaitForSecondsRealtime(data.EntryDuration);
                    }

                    CompleteEntryMotion();
                }
                else
                {
                    CompleteEntryMotion();
                }

                if (data.DelayBeforeDialogue > 0f)
                {
                    yield return new WaitForSecondsRealtime(data.DelayBeforeDialogue);
                }

                if (data.ShouldPlayDialogue)
                {
                    bool dialogueCompleted = false;
                    PlayDialogue(data, context, () => dialogueCompleted = true);
                    while (!dialogueCompleted)
                    {
                        yield return null;
                    }
                }

                if (data.DelayAfterDialogue > 0f)
                {
                    yield return new WaitForSecondsRealtime(data.DelayAfterDialogue);
                }
            }

            CompleteIntro(context, shouldUnlockPlayers);
        }

        private void PlayEntryAnimation()
        {
            if (bossAnimator == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(entryTriggerName))
            {
                bossAnimator.ResetTrigger(entryTriggerName);
                bossAnimator.SetTrigger(entryTriggerName);
                return;
            }

            if (!string.IsNullOrWhiteSpace(entryStateName))
            {
                bossAnimator.Play(entryStateName, entryStateLayer, 0f);
            }
        }

        private void PrepareEntryMotion()
        {
            if (!moveEntryFromOffset || entryStartLocalOffset == Vector3.zero)
            {
                return;
            }

            Transform motionRoot = ResolveEntryMotionRoot();
            if (motionRoot == null)
            {
                return;
            }

            if (_entryMotionRoutine != null)
            {
                StopCoroutine(_entryMotionRoutine);
                _entryMotionRoutine = null;
            }

            _activeEntryMotionRoot = motionRoot;
            _entryMotionEndLocalPosition = motionRoot.localPosition;
            _entryMotionStartLocalPosition = _entryMotionEndLocalPosition + entryStartLocalOffset;
            motionRoot.localPosition = _entryMotionStartLocalPosition;
            _entryMotionPrepared = true;
        }

        private void StartEntryMotion(float fallbackDuration)
        {
            if (!_entryMotionPrepared || _activeEntryMotionRoot == null)
            {
                return;
            }

            float duration = entryMoveDuration > 0f ? entryMoveDuration : fallbackDuration;
            if (duration <= 0f)
            {
                CompleteEntryMotion();
                return;
            }

            _entryMotionRoutine = StartCoroutine(RunEntryMotion(duration));
        }

        private IEnumerator RunEntryMotion(float duration)
        {
            Transform motionRoot = _activeEntryMotionRoot;
            float elapsed = 0f;
            while (_entryMotionPrepared && motionRoot != null && elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                motionRoot.localPosition = Vector3.LerpUnclamped(
                    _entryMotionStartLocalPosition,
                    _entryMotionEndLocalPosition,
                    eased);
                yield return null;
            }

            if (_entryMotionPrepared && motionRoot != null)
            {
                motionRoot.localPosition = _entryMotionEndLocalPosition;
            }

            _entryMotionRoutine = null;
            _entryMotionPrepared = false;
            _activeEntryMotionRoot = null;
        }

        private void CompleteEntryMotion()
        {
            if (_entryMotionRoutine != null)
            {
                StopCoroutine(_entryMotionRoutine);
                _entryMotionRoutine = null;
            }

            if (_entryMotionPrepared && _activeEntryMotionRoot != null)
            {
                _activeEntryMotionRoot.localPosition = _entryMotionEndLocalPosition;
            }

            _entryMotionPrepared = false;
            _activeEntryMotionRoot = null;
        }

        private Transform ResolveEntryMotionRoot()
        {
            if (entryMotionRoot != null)
            {
                return entryMotionRoot;
            }

            return bossAnimator != null ? bossAnimator.transform : transform;
        }

        private void PlayEntryLandingSfx(BossIntroSequenceData data)
        {
            AudioClip clip = data.EntryLandingSfx;
            if (clip == null)
            {
                return;
            }

            if (_entryLandingSfxRoutine != null)
            {
                StopCoroutine(_entryLandingSfxRoutine);
                _entryLandingSfxRoutine = null;
            }

            float delay = data.EntryLandingSfxDelay;
            if (delay > 0f)
            {
                _entryLandingSfxRoutine = StartCoroutine(PlayEntryLandingSfxAfterDelay(clip, data.EntryLandingSfxVolume, delay));
                return;
            }

            PlayEntryLandingSfx(clip, data.EntryLandingSfxVolume);
        }

        private IEnumerator PlayEntryLandingSfxAfterDelay(AudioClip clip, float volume, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            _entryLandingSfxRoutine = null;
            PlayEntryLandingSfx(clip, volume);
        }

        private void PlayEntryLandingSfx(AudioClip clip, float volume)
        {
            if (clip == null)
            {
                return;
            }

            if (MMSoundManager.HasInstance && MMSoundManager.Current != null)
            {
                MMSoundManagerPlayOptions options = MMSoundManagerPlayOptions.Default;
                options.MmSoundManagerTrack = MMSoundManager.MMSoundManagerTracks.Sfx;
                options.Location = transform.position;
                options.Volume = volume;
                options.Loop = false;

                MMSoundManagerSoundPlayEvent.Trigger(clip, options);
                return;
            }

            AudioSource.PlayClipAtPoint(clip, transform.position, volume);
        }

        private void PlayDialogue(
            BossIntroSequenceData data,
            BossRoomTransitionCompletedContext context,
            Action onCompleted)
        {
            IBossIntroDialoguePlayer dialoguePlayer = null;
            if (dialoguePlayerOwner != null)
            {
                dialoguePlayer = dialoguePlayerOwner.GetComponent<IBossIntroDialoguePlayer>();
            }

            if (dialoguePlayer == null)
            {
                Log("Dialogue player missing. Continuing intro without dialogue playback.");
                onCompleted?.Invoke();
                return;
            }

            dialoguePlayer.Play(data, context, onCompleted);
        }

        private void CompleteIntro(BossRoomTransitionCompletedContext context, bool shouldUnlockPlayers)
        {
            Debug.Log($"[BossIntro] step=CompleteIntro t={Time.time:F2} shouldUnlock={shouldUnlockPlayers}", this);
            CompleteEntryMotion();

            if (shouldUnlockPlayers)
            {
                UnfreezeCachedPlayers();
            }

            RestoreIntroInvulnerability();
            RestoreIntroTargetability();
            _introRoutine = null;
            _isIntroRunning = false;
            _introCompleted = true;
            IntroCompleted?.Invoke(context);
            Debug.Log($"[BossIntro] step=End t={Time.time:F2}", this);
            Log("Boss intro sequence completed.");
        }

        private void StopActiveIntro(bool unfreezePlayers)
        {
            if (_entryLandingSfxRoutine != null)
            {
                StopCoroutine(_entryLandingSfxRoutine);
                _entryLandingSfxRoutine = null;
            }

            CompleteEntryMotion();

            if (_introRoutine != null)
            {
                StopCoroutine(_introRoutine);
                _introRoutine = null;
            }

            if (!_isIntroRunning)
            {
                RestoreIntroInvulnerability();
                RestoreIntroTargetability();
                return;
            }

            if (unfreezePlayers)
            {
                UnfreezeCachedPlayers();
            }

            SetIntroVisibility(isVisible: true);
            RestoreIntroInvulnerability();
            RestoreIntroTargetability();
            _isIntroRunning = false;
            Log("Boss intro sequence stopped.");
        }

        private void ApplyPreIntroProtection()
        {
            if (!Application.isPlaying || !protectBossBeforeIntroStarts || _introCompleted)
            {
                return;
            }

            // BossSceneAutoStarter may delay BeginIntro; protect spawned bosses before passive attacks tick.
            ApplyIntroInvulnerability();
            ApplyIntroTargetability();
        }

        private void ApplyIntroInvulnerability()
        {
            if (!makeBossInvulnerableDuringIntro)
            {
                return;
            }

            bossHealth ??= ResolveBossHealth();
            if (bossHealth == null)
            {
                Log("Boss Health is missing. Intro invulnerability was not applied.");
                return;
            }

            if (_introInvulnerabilityApplied)
            {
                bossHealth.Invulnerable = true;
                return;
            }

            _bossWasInvulnerable = bossHealth.Invulnerable;
            bossHealth.Invulnerable = true;
            _introInvulnerabilityApplied = true;
        }

        private void RestoreIntroInvulnerability()
        {
            if (!_introInvulnerabilityApplied)
            {
                return;
            }

            if (bossHealth != null)
            {
                bossHealth.Invulnerable = _bossWasInvulnerable;
            }

            _introInvulnerabilityApplied = false;
        }

        private void ApplyIntroTargetability()
        {
            if (!makeBossUntargetableDuringIntro)
            {
                return;
            }

            bossTargetable ??= ResolveBossTargetable(createIfMissing: true);
            if (bossTargetable == null)
            {
                Log("Boss CombatTargetable is missing. Intro targetability was not changed.");
                return;
            }

            if (_introTargetabilityApplied)
            {
                bossTargetable.IsTargetable = false;
                return;
            }

            _bossWasTargetable = bossTargetable.IsTargetable;
            bossTargetable.IsTargetable = false;
            _introTargetabilityApplied = true;
        }

        private void RestoreIntroTargetability()
        {
            if (!_introTargetabilityApplied)
            {
                return;
            }

            if (bossTargetable != null)
            {
                bossTargetable.IsTargetable = _bossWasTargetable;
            }

            _introTargetabilityApplied = false;
        }

        private void AutoAssignVisibilityTargets()
        {
            if (introVisibilityTargets != null && introVisibilityTargets.Length > 0)
            {
                return;
            }

            Transform visualChild = transform.Find("Visual");
            if (visualChild != null)
            {
                introVisibilityTargets = new[] { visualChild.gameObject };
                return;
            }

            if (bossAnimator != null)
            {
                introVisibilityTargets = new[] { bossAnimator.gameObject };
            }
        }

        private Animator ResolveBossAnimator()
        {
            Transform visualChild = transform.Find("Visual");
            if (visualChild != null)
            {
                Transform spriteChild = visualChild.Find("BerthaSprite");
                if (spriteChild != null)
                {
                    Animator spriteAnimator = spriteChild.GetComponent<Animator>();
                    if (spriteAnimator != null)
                    {
                        return spriteAnimator;
                    }
                }

                Animator visualAnimator = visualChild.GetComponentInChildren<Animator>(includeInactive: true);
                if (visualAnimator != null)
                {
                    return visualAnimator;
                }
            }

            return GetComponentInChildren<Animator>(includeInactive: true);
        }

        private Health ResolveBossHealth()
        {
            Health health = GetComponent<Health>();
            if (health != null)
            {
                return health;
            }

            if (bossAnimator != null)
            {
                health = bossAnimator.GetComponentInParent<Health>();
                if (health != null)
                {
                    return health;
                }
            }

            return GetComponentInChildren<Health>(includeInactive: true);
        }

        private CombatTargetable ResolveBossTargetable(bool createIfMissing)
        {
            CombatTargetable targetable = GetComponent<CombatTargetable>();
            if (targetable != null)
            {
                return targetable;
            }

            if (bossHealth != null)
            {
                targetable = bossHealth.GetComponentInParent<CombatTargetable>();
                if (targetable != null)
                {
                    return targetable;
                }
            }

            if (bossAnimator != null)
            {
                targetable = bossAnimator.GetComponentInParent<CombatTargetable>();
                if (targetable != null)
                {
                    return targetable;
                }
            }

            targetable = GetComponentInChildren<CombatTargetable>(includeInactive: true);
            if (targetable != null || !createIfMissing || !Application.isPlaying)
            {
                return targetable;
            }

            return gameObject.AddComponent<CombatTargetable>();
        }

        private void SetIntroVisibility(bool isVisible)
        {
            ApplyIntroVisibilityLocal(isVisible);

            // 멀티 환경: host 측에서 visibility 변경 시 모든 client 에 broadcast.
            // RunIntroSequence 가 host 측에서만 진행되어 게스트 측 Visual 이 reveal 안 되는 문제 fix.
            // OnEnable/OnDisable 의 hide 도 모든 client 에서 자체 호출되므로 broadcast 가 dup 이어도 무해 (활성 상태 비교 후 같으면 skip).
            BroadcastIntroVisibilityIfHost(isVisible);
        }

        /// <summary>
        /// 다른 client 의 ClientRpc 수신 시 호출 — local 적용만 (재broadcast 방지).
        /// </summary>
        public void ApplyRemoteIntroVisibility(bool isVisible)
        {
            ApplyIntroVisibilityLocal(isVisible);
        }

        private void ApplyIntroVisibilityLocal(bool isVisible)
        {
            if (introVisibilityTargets == null || introVisibilityTargets.Length == 0)
            {
                return;
            }

            for (int i = 0; i < introVisibilityTargets.Length; i++)
            {
                GameObject target = introVisibilityTargets[i];
                if (target != null && target.activeSelf != isVisible)
                {
                    target.SetActive(isVisible);
                }
            }
        }

        private void BroadcastIntroVisibilityIfHost(bool isVisible)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || !nm.IsServer)
            {
                return;
            }

            // 첫 번째 spawned PlayerMovementSync 인스턴스 통해 broadcast.
            // ClientRpc 는 owner 무관 모든 client 가 수신.
            PlayerMovementSync[] syncs = UnityEngine.Object.FindObjectsByType<PlayerMovementSync>(FindObjectsSortMode.None);
            for (int i = 0; i < syncs.Length; i++)
            {
                PlayerMovementSync sync = syncs[i];
                if (sync != null && sync.IsSpawned)
                {
                    sync.BroadcastBossIntroVisibilityClientRpc(isVisible);
                    return;
                }
            }
        }

        private void FreezeCachedPlayers()
        {
            int count = 0;
            for (int i = 0; i < _cachedPlayers.Length; i++)
            {
                Character player = _cachedPlayers[i];
                if (player != null)
                {
                    player.Freeze();
                    count++;
                }
            }
            Debug.Log($"[Freeze] FreezeCachedPlayers — {count}/{_cachedPlayers.Length} players frozen (host side).", this);
        }

        private void UnfreezeCachedPlayers()
        {
            int count = 0;
            for (int i = 0; i < _cachedPlayers.Length; i++)
            {
                Character player = _cachedPlayers[i];
                if (player != null)
                {
                    player.UnFreeze();
                    count++;
                }
            }
            Debug.Log($"[Unfreeze] UnfreezeCachedPlayers — {count}/{_cachedPlayers.Length} host-side characters UnFreeze 호출.", this);

            // 멀티 환경: host 측에서만 _cachedPlayers 의 UnFreeze 호출됨 (host 측 mirror 만 영향).
            // 게스트 측 actual player 는 owner-auth 라 자기 client 에서 UnFreeze 호출 필요 → ClientRpc broadcast.
            // Freeze 는 GatherPlayers 의 TeleportPlayerClientRpc(freeze=true) 가 이미 처리, 본 fix 는 Unfreeze 만 담당.
            BroadcastUnfreezeIfHost();

            // 견고성 — host 측에서만 시작. 0.5s 재송신 (NetworkObject IsSpawned race 대응) + 5s watchdog 발동.
            NetworkManager nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening && nm.IsServer && isActiveAndEnabled)
            {
                StartCoroutine(ResendUnfreezeAfterDelayCoroutine(0.5f));
                StartCoroutine(UnfreezeWatchdogCoroutine(5f));
            }
        }

        private void BroadcastUnfreezeIfHost()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || !nm.IsServer)
            {
                return;
            }

            // 모든 player NetworkBehaviour 인스턴스에 ClientRpc 발사 — 각 NB 의 owner client 측에서 자기 player UnFreeze.
            // IsOwner 가드는 PlayerMovementSync.UnfreezePlayerClientRpc 에서 제거됨 (idempotent).
            PlayerMovementSync[] syncs = UnityEngine.Object.FindObjectsByType<PlayerMovementSync>(FindObjectsSortMode.None);
            int sent = 0;
            for (int i = 0; i < syncs.Length; i++)
            {
                PlayerMovementSync sync = syncs[i];
                if (sync != null && sync.IsSpawned)
                {
                    sync.UnfreezePlayerClientRpc();
                    sent++;
                }
            }
            Debug.Log($"[Unfreeze] BroadcastUnfreezeIfHost — ClientRpc sent to {sent}/{syncs.Length} PlayerMovementSync instances.", this);
        }

        /// <summary>
        /// 0.5초 후 한 번 더 ClientRpc 재송신. NetworkObject Spawn race 대응.
        /// UnfreezePlayerClientRpc 는 idempotent 라 두 번 호출 무해.
        /// </summary>
        private IEnumerator ResendUnfreezeAfterDelayCoroutine(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || !nm.IsServer) yield break;
            Debug.Log($"[Unfreeze] Resend after {delay:F2}s — 재송신.", this);
            BroadcastUnfreezeIfHost();
        }

        /// <summary>
        /// watchdog — N초 후 어떤 player Character.ConditionState 가 여전히 Frozen 이면 강제 재 Unfreeze.
        /// 핵심 안전망 — intro 시퀀스 hang / RPC 미도달 / ownership race 모두 대응.
        /// </summary>
        private IEnumerator UnfreezeWatchdogCoroutine(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || !nm.IsServer) yield break;

            // host 측 모든 Character (Player) 검사 + 게스트 측은 PlayerMovementSync 통해 다시 RPC.
            Character[] all = UnityEngine.Object.FindObjectsByType<Character>(FindObjectsSortMode.None);
            int stuckCount = 0;
            for (int i = 0; i < all.Length; i++)
            {
                Character ch = all[i];
                if (ch == null) continue;
                if (ch.CharacterType != Character.CharacterTypes.Player) continue;
                if (ch.ConditionState != null && ch.ConditionState.CurrentState == CharacterStates.CharacterConditions.Frozen)
                {
                    stuckCount++;
                    Debug.LogWarning($"[Unfreeze][WATCHDOG] Player still Frozen after {delay:F1}s — force UnFreeze. go={ch.gameObject.name}", ch);
                    ch.UnFreeze();
                }
            }

            if (stuckCount > 0)
            {
                // 게스트 측 player 도 force unfreeze 위해 다시 broadcast.
                Debug.LogWarning($"[Unfreeze][WATCHDOG] {stuckCount} stuck — re-broadcast ClientRpc.", this);
                BroadcastUnfreezeIfHost();
            }
            else
            {
                Debug.Log($"[Unfreeze][WATCHDOG] All players unfrozen after {delay:F1}s. OK.", this);
            }
        }

        private static Character[] CopyPlayers(Character[] players)
        {
            if (players == null || players.Length == 0)
            {
                return System.Array.Empty<Character>();
            }

            Character[] copiedPlayers = new Character[players.Length];
            System.Array.Copy(players, copiedPlayers, players.Length);
            return copiedPlayers;
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[BossIntroSequence] " + message, this);
            }
        }

        private void PlayBossStartBgm()
        {
            if (bossStartBgmClip == null)
            {
                return;
            }

            StageBgmPlayer.PlayLoop(
                bossStartBgmClip,
                bossStartBgmId,
                bossStartBgmVolume,
                this,
                "BossIntro");
        }
    }
}
