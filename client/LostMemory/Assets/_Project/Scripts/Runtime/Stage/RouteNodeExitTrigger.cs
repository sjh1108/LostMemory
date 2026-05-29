using System;
using System.Collections;
using System.Collections.Generic;
using LostMemory.Audio;
using LostMemory.Networking.Common;
using LostMemory.Networking.Player;
using LostMemory.TestKhi;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using Unity.Netcode;
using UnityEngine;

namespace LostMemory.Stage
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    [AddComponentMenu("Lost Memory/Stage/Route Node Exit Trigger")]
    public sealed class RouteNodeExitTrigger : MonoBehaviour
    {
        // === [Multi Ready Gate] static registry ===
        // 멀티 플레이에서 StageRouteManager.FireLocalTeleportClientRpc 가 triggerId 로 트리거를 찾아
        // 각 클라이언트가 자기 owned Character 를 워프하기 위해 사용. owner-auth NetworkTransform 제약상
        // 서버가 게스트 캐릭터를 직접 이동시킬 수 없어 모든 클라이언트가 로컬에서 자기 캐릭터를 옮긴다.
        private static readonly Dictionary<string, RouteNodeExitTrigger> s_registry =
            new Dictionary<string, RouteNodeExitTrigger>(StringComparer.Ordinal);

        public static bool TryGetRegistered(string triggerId, out RouteNodeExitTrigger trigger)
        {
            return s_registry.TryGetValue(triggerId, out trigger);
        }

        private bool _registeredInRegistry;
        // ready 게이트가 ServerRpc 전송됨 — 로컬 F 재누름 차단 + Transitioning 뷰 표시.
        // ClientRpc fire 도착 또는 cancel 통지 시 false 로 복귀.
        private bool _multiReadySent;
        // === ===

        [SerializeField] private string triggerId = "default";
        [SerializeField] private StageRouteManager routeManager;
        [SerializeField] private bool unlockedOnStart;
        [SerializeField] private bool requireInteractInput = true;
        [SerializeField] private KeyCode fallbackInteractKey = KeyCode.F;
        [SerializeField] private string acceptedPlayerId = "Player1";
        [SerializeField] private bool useDistanceFallback = true;
        [SerializeField, Min(0.1f)] private float activationRadius = 1.5f;
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private bool hideVisualWhenLocked;
        [SerializeField] private RouteNodeExitTriggerView triggerView;
        [SerializeField] private bool completeRunInsteadOfAdvancingRoute;
        [SerializeField] private bool useLocalTeleport;
        [SerializeField] private Transform localTeleportTarget;
        [SerializeField] private string localTeleportTargetRootName = string.Empty;
        [SerializeField] private string localTeleportAnchorTag = "default";
        [SerializeField] private Vector3 localTeleportOffset;

        [Header("Local Teleport BGM (optional)")]
        [SerializeField] private AudioClip localTeleportBgmClip;
        [SerializeField] private int localTeleportBgmId = StageBgmPlayer.Stage2DungeonBgmId;
        [SerializeField, Range(0f, 2f)] private float localTeleportBgmVolume = 1f;

        [Header("Portal Animation (optional)")]
        [Tooltip("재생할 게이트/포탈 Animator. 비어있으면 즉시 전환.")]
        [SerializeField] private Animator portalAnimator;
        [Tooltip("0 이면 컨트롤러 내 클립 중 가장 긴 길이를 자동 사용. >0 이면 그 값을 사용.")]
        [SerializeField, Min(0f)] private float portalAnimationDurationOverride;
        [Tooltip("애니메이션 끝난 뒤 추가 대기 시간.")]
        [SerializeField, Min(0f)] private float postAnimationDelay = 0.5f;
        [Tooltip("애니메이션 + 대기 동안 플레이어 입력/이동을 잠금.")]
        [SerializeField] private bool freezePlayerDuringPortalAnimation = true;

        [SerializeField] private bool debugLogging;

        private readonly HashSet<Character> candidates = new HashSet<Character>();
        private readonly Collider2D[] overlapBuffer = new Collider2D[16];
        private Collider2D trigger;
        private bool unlocked;
        private bool requestInProgress;

        public string TriggerId => triggerId;
        public bool IsUnlocked => unlocked;
        public bool UsesLocalTeleport => useLocalTeleport;

        public void Unlock()
        {
            SetUnlocked(true);
        }

        public void Lock()
        {
            SetUnlocked(false);
        }

        public void SetUnlocked(bool value)
        {
            RefreshReferences();
            unlocked = value;
            requestInProgress = false;

            if (!value)
            {
                candidates.Clear();

                // 잠금 전환 — 멀티 ready 도 동시에 정리.
                if (_multiReadySent && HostAuthority.IsNetworkSessionActive && routeManager != null)
                {
                    CancelActiveReadyOnRouteManager();
                }
                _multiReadySent = false;
            }

            if (trigger != null)
            {
                trigger.enabled = true;
                trigger.isTrigger = true;
            }

            if (visualRoot != null)
            {
                visualRoot.SetActive(value || !hideVisualWhenLocked);
            }

            ApplyCurrentViewState();
            Log(value ? "Unlocked." : "Locked.");
        }

        private void Reset()
        {
            RefreshReferences();
            ConfigureTrigger();
        }

        private void OnValidate()
        {
            activationRadius = Mathf.Max(0.1f, activationRadius);
            RefreshReferences();
            ConfigureTrigger();
        }

        private void Awake()
        {
            RefreshReferences();
            ConfigureTrigger();
            SetUnlocked(unlockedOnStart);
        }

        private void OnEnable()
        {
            RegisterInRegistry();
        }

        private void OnDisable()
        {
            UnregisterFromRegistry();

            // 멀티: 비활성화/파괴되면 server 의 ready set 에 남은 우리 clientId 정리.
            // (FireLocalTeleportClientRpc 또는 route advance LoadScene 이 도착했을 때 트리거가 사라져 못 찾는 케이스도 같이 방지.)
            if (_multiReadySent && HostAuthority.IsNetworkSessionActive)
            {
                _multiReadySent = false;
                RefreshReferences();
                if (routeManager != null && !string.IsNullOrEmpty(triggerId))
                {
                    CancelActiveReadyOnRouteManager();
                }
            }
        }

        private void RegisterInRegistry()
        {
            if (_registeredInRegistry || string.IsNullOrEmpty(triggerId))
            {
                return;
            }

            if (s_registry.TryGetValue(triggerId, out RouteNodeExitTrigger existing) &&
                existing != null && existing != this)
            {
                Debug.LogWarning(
                    $"[RouteNodeExitTrigger] Duplicate triggerId '{triggerId}'. 기존='{existing.gameObject.name}', 신규='{gameObject.name}'. 최신 인스턴스로 덮어씁니다.",
                    this);
            }

            s_registry[triggerId] = this;
            _registeredInRegistry = true;
        }

        private void UnregisterFromRegistry()
        {
            if (!_registeredInRegistry)
            {
                return;
            }

            if (!string.IsNullOrEmpty(triggerId) &&
                s_registry.TryGetValue(triggerId, out RouteNodeExitTrigger registered) &&
                registered == this)
            {
                s_registry.Remove(triggerId);
            }

            _registeredInRegistry = false;
        }

        private void Update()
        {
            if (!unlocked || requestInProgress || _multiReadySent)
            {
                if (_multiReadySent)
                {
                    // 대기 중에도 뷰 상태는 갱신 (후보가 빠져나가는 등).
                    ApplyCurrentViewState();
                }
                return;
            }

            Character selected = ResolveCandidate();
            ApplyCurrentViewState(selected != null);

            if (selected == null)
            {
                return;
            }

            if (!requireInteractInput || IsInteractPressedThisFrame(selected))
            {
                RequestAdvance(selected);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TrackCandidate(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TrackCandidate(other);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            Character character = other != null ? other.GetComponentInParent<Character>() : null;
            if (character == null)
            {
                return;
            }

            candidates.Remove(character);
            ApplyCurrentViewState();

            // 멀티 ready 게이트: 로컬 owned 캐릭터가 트리거 영역을 벗어나면 cancel ServerRpc 발사.
            // 다른 클라(=non-owner AI 캐릭터) 가 빠져나가는 건 무시 — 서버는 그 클라가 직접 보낸 cancel 로만 처리.
            // useLocalTeleport=1 → LocalTeleportReady, =0 (route advance) → RouteAdvanceReady 양쪽 다 처리.
            if (_multiReadySent &&
                HostAuthority.IsNetworkSessionActive &&
                IsLocalOwnedCharacter(character))
            {
                _multiReadySent = false;
                RefreshReferences();
                if (routeManager != null)
                {
                    CancelActiveReadyOnRouteManager();
                }
                ApplyCurrentViewState();
            }
        }

        // 트리거 인스턴스 설정에 따라 적절한 ready 게이트 취소를 호출.
        // useLocalTeleport=1 → 같은 씬 워프용 LocalTeleportReady.
        // useLocalTeleport=0 → 다른 씬 LoadScene 용 RouteAdvanceReady.
        // (completeRunInsteadOfAdvancingRoute 는 ready 게이트 안 씀 → 호출되어도 noop.)
        private void CancelActiveReadyOnRouteManager()
        {
            if (routeManager == null || string.IsNullOrEmpty(triggerId)) return;
            if (useLocalTeleport)
            {
                routeManager.CancelLocalTeleportReady(triggerId);
            }
            else
            {
                routeManager.CancelRouteAdvanceReady(triggerId);
            }
        }

        // 멀티 모드에서 character 가 "내 클라이언트의 owned Character" 인지 판정.
        // 싱글에선 항상 true (네트워크 개념 없음).
        private static bool IsLocalOwnedCharacter(Character character)
        {
            if (character == null)
            {
                return false;
            }
            if (!HostAuthority.IsNetworkSessionActive)
            {
                return true;
            }
            NetworkObject no = character.GetComponentInParent<NetworkObject>();
            return no != null && no.IsOwner;
        }

        private void TrackCandidate(Collider2D other)
        {
            if (!unlocked || other == null)
            {
                return;
            }

            Character character = other.GetComponentInParent<Character>();
            if (!CanUseTrigger(character))
            {
                return;
            }

            bool wasAdded = candidates.Add(character);
            if (!wasAdded)
            {
                return;
            }

            ApplyCurrentViewState();

            if (!requireInteractInput)
            {
                RequestAdvance(character);
            }
        }

        private void RefreshReferences()
        {
            if (trigger == null)
            {
                trigger = GetComponent<Collider2D>();
            }

            if (routeManager == null)
            {
                routeManager = FindObjectOfType<StageRouteManager>();
            }

            if (visualRoot == null && transform.childCount > 0)
            {
                visualRoot = transform.GetChild(0).gameObject;
            }

            if (triggerView == null)
            {
                triggerView = GetComponentInChildren<RouteNodeExitTriggerView>(includeInactive: true);
            }
        }

        private void ConfigureTrigger()
        {
            if (trigger != null)
            {
                trigger.isTrigger = true;
            }
        }

        private Character ResolveCandidate()
        {
            RemoveInvalidCandidates();

            foreach (Character candidate in candidates)
            {
                if (CanUseTrigger(candidate))
                {
                    return candidate;
                }
            }

            if (!useDistanceFallback)
            {
                return null;
            }

            return ResolveCandidateFromTriggerOverlap() ?? ResolveCandidateByDistance();
        }

        private Character ResolveCandidateFromTriggerOverlap()
        {
            if (trigger == null)
            {
                return null;
            }

            ContactFilter2D filter = new ContactFilter2D
            {
                useTriggers = true,
                useLayerMask = false
            };

            int count = trigger.Overlap(filter, overlapBuffer);
            for (int i = 0; i < count; i++)
            {
                Character character = overlapBuffer[i] != null ? overlapBuffer[i].GetComponentInParent<Character>() : null;
                if (CanUseTrigger(character))
                {
                    return character;
                }
            }

            return null;
        }

        private Character ResolveCandidateByDistance()
        {
            Character[] characters = FindObjectsOfType<Character>();
            Vector3 triggerPosition = trigger != null ? trigger.bounds.center : transform.position;
            float sqrRadius = activationRadius * activationRadius;

            for (int i = 0; i < characters.Length; i++)
            {
                Character character = characters[i];
                if (!CanUseTrigger(character))
                {
                    continue;
                }

                Vector3 delta = character.transform.position - triggerPosition;
                delta.z = 0f;
                if (delta.sqrMagnitude <= sqrRadius)
                {
                    return character;
                }
            }

            return null;
        }

        private void RemoveInvalidCandidates()
        {
            candidates.RemoveWhere(candidate => !CanUseTrigger(candidate));
        }

        private bool CanUseTrigger(Character character)
        {
            if (character == null || !character.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (character.CharacterType != Character.CharacterTypes.Player)
            {
                return false;
            }

            if (KhiPlayerActionGate.IsBlocked(character))
            {
                return false;
            }

            return string.IsNullOrEmpty(acceptedPlayerId) || character.PlayerID == acceptedPlayerId;
        }

        private bool IsInteractPressedThisFrame(Character character)
        {
            InputManager inputManager = character != null ? character.LinkedInputManager : null;
            if (inputManager != null &&
                inputManager.InteractButton != null &&
                inputManager.InteractButton.State.CurrentState == MMInput.ButtonStates.ButtonDown)
            {
                return true;
            }

            return Input.GetKeyDown(fallbackInteractKey);
        }

        private void RequestAdvance(Character character)
        {
            if (requestInProgress)
            {
                return;
            }

            RefreshReferences();

            if (portalAnimator != null)
            {
                requestInProgress = true;
                ApplyCurrentViewState();
                StartCoroutine(PlayPortalAnimationAndAdvance(character));
                return;
            }

            RequestAdvanceImmediate(character);
        }

        private IEnumerator PlayPortalAnimationAndAdvance(Character character)
        {
            if (freezePlayerDuringPortalAnimation && character != null)
            {
                character.Freeze();
            }

            portalAnimator.enabled = true;

            float duration = portalAnimationDurationOverride > 0f
                ? portalAnimationDurationOverride
                : ResolveAnimatorClipDuration(portalAnimator);

            if (duration > 0f)
            {
                yield return new WaitForSeconds(duration);
            }

            if (postAnimationDelay > 0f)
            {
                yield return new WaitForSeconds(postAnimationDelay);
            }

            // 애니메이션이 끝났으니 freeze 유지할 이유 없음.
            // 플레이어는 DontDestroyOnLoad 로 다음 씬까지 살아남고 Freeze 상태도 그대로 이어지므로
            // 씬 전환 성공 여부와 무관하게 여기서 무조건 풀어줘야 다음 방에서 입력이 막히지 않는다.
            if (freezePlayerDuringPortalAnimation && character != null)
            {
                character.UnFreeze();
            }

            // 코루틴이 잡고 있던 in-progress 플래그를 비우고 실제 advance 로 넘긴다.
            // RequestAdvanceImmediate 가 내부에서 다시 set/clear 한다.
            requestInProgress = false;
            RequestAdvanceImmediate(character);
        }

        // [DiagRoute] trigger 측에서도 호출 시점의 routeManager 상태 1초 throttle 로 로깅.
        [Header("Diagnostics (route advance trace)")]
        [SerializeField] private bool _diagRouteLogging = false;
        private float _diagNextLogTimeImmediate;

        private bool RequestAdvanceImmediate(Character character)
        {
            if (useLocalTeleport)
            {
                RequestLocalTeleport(character);
                return true;
            }

            if (completeRunInsteadOfAdvancingRoute)
            {
                RequestRunCompletion(character);
                return true;
            }

            // [DiagRoute] routeManager 참조 상태와 Instance 일치 여부 throttle 로깅.
            if (_diagRouteLogging && Time.unscaledTime >= _diagNextLogTimeImmediate)
            {
                _diagNextLogTimeImmediate = Time.unscaledTime + 1f;
                StageRouteManager singleton = StageRouteManager.Instance;
                Debug.Log(
                    $"[DiagRoute] RouteNodeExitTrigger.RequestAdvanceImmediate triggerId='{triggerId}' " +
                    $"routeManager={(routeManager != null ? routeManager.gameObject.name : "NULL")} " +
                    $"routeManagerIsSpawned={(routeManager != null ? routeManager.IsSpawned.ToString() : "n/a")} " +
                    $"Instance={(singleton != null ? singleton.gameObject.name : "NULL")} " +
                    $"refEqualsInstance={(routeManager == singleton)} " +
                    $"character='{(character != null ? character.name : "null")}' " +
                    $"requestInProgress={requestInProgress}",
                    this);
            }

            if (routeManager == null)
            {
                Debug.LogWarning($"[RouteNodeExitTrigger] No StageRouteManager found for trigger '{triggerId}'.", this);
                return false;
            }

            // 멀티: ready 게이트로 위임 — 전원 ready 합의 시 서버가 NGO SceneManager.LoadScene 으로 동기 로드.
            // 본 클라는 ServerRpc(=CustomMessage) 만 보내고 _multiReadySent=true 로 파랑 표시 + F 재누름 차단.
            // useLocalTeleport 경로의 RequestLocalTeleport 와 대칭 구조.
            if (HostAuthority.IsNetworkSessionActive)
            {
                bool accepted = routeManager.RequestRouteAdvanceReady(triggerId);
                if (!accepted)
                {
                    requestInProgress = false;
                    ApplyCurrentViewState();
                    return false;
                }

                _multiReadySent = true;
                requestInProgress = false;
                ApplyCurrentViewState();
                Log($"Route advance ready 전송. triggerId='{triggerId}'. 다른 플레이어 ready 대기.");
                return true;
            }

            // 싱글: 기존 즉시 동작.
            requestInProgress = true;
            ApplyCurrentViewState();
            bool acceptedSolo = routeManager.RequestAdvanceRouteNode(triggerId, character);
            if (!acceptedSolo)
            {
                requestInProgress = false;
                ApplyCurrentViewState();
                return false;
            }
            return true;
        }

        private static float ResolveAnimatorClipDuration(Animator animator)
        {
            if (animator == null)
            {
                return 0f;
            }

            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            if (controller == null)
            {
                return 0f;
            }

            AnimationClip[] clips = controller.animationClips;
            if (clips == null)
            {
                return 0f;
            }

            float max = 0f;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip != null && clip.length > max)
                {
                    max = clip.length;
                }
            }
            return max;
        }

        private void RequestRunCompletion(Character character)
        {
            RunManager runManager = RunManager.Instance;
            if (runManager == null)
            {
                Debug.LogWarning($"[RouteNodeExitTrigger] No RunManager found for trigger '{triggerId}'.", this);
                return;
            }

            requestInProgress = true;
            ApplyCurrentViewState();

            bool accepted = runManager.NotifyBossClearPortalEntered();
            if (!accepted)
            {
                requestInProgress = false;
                ApplyCurrentViewState();
                return;
            }

            Log($"Completed run from trigger '{triggerId}' for '{character.name}'.");
        }

        private void RequestLocalTeleport(Character character)
        {
            // 멀티 (네트워크 세션 활성): ready 게이트로 위임.
            //   - 본 클라이언트는 ServerRpc 로 "내가 준비됐다" 통보만 한다.
            //   - 실제 워프는 서버가 전원 ready 를 감지해 FireLocalTeleportClientRpc 를 broadcast 하면
            //     각 클라이언트가 자기 owned Character 를 ExecuteLocalTeleportForLocalCharacter 로 옮긴다.
            //   - PlayerMovementSync 가 owner-auth NetworkTransform 이라 서버가 직접 게스트 캐릭터를
            //     움직여도 다음 sync 에 되돌아가므로 ClientRpc 분기가 필수.
            if (HostAuthority.IsNetworkSessionActive)
            {
                RefreshReferences();
                if (routeManager == null)
                {
                    Debug.LogWarning(
                        $"[RouteNodeExitTrigger] No StageRouteManager for trigger '{triggerId}' — multi ready 게이트 불가, 폴백으로 즉시 워프.",
                        this);
                    RequestLocalTeleportImmediate(character);
                    return;
                }

                bool accepted = routeManager.RequestLocalTeleportReady(triggerId);
                if (!accepted)
                {
                    // 거부됨 (load 진행 중 등) — 상태 정리.
                    requestInProgress = false;
                    ApplyCurrentViewState();
                    return;
                }

                _multiReadySent = true;
                requestInProgress = false; // 코루틴/Animation flow 에서 set 한 값을 정리.
                ApplyCurrentViewState();
                Log($"Local teleport ready 전송. triggerId='{triggerId}'. 다른 플레이어 ready 대기.");
                return;
            }

            // 싱글: 기존 즉시 동작.
            RequestLocalTeleportImmediate(character);
        }

        private void RequestLocalTeleportImmediate(Character character)
        {
            requestInProgress = true;
            ApplyCurrentViewState();

            Transform target = ResolveLocalTeleportTarget();
            if (target == null)
            {
                Debug.LogWarning($"[RouteNodeExitTrigger] Local teleport target not found for trigger '{triggerId}'.", this);
                requestInProgress = false;
                ApplyCurrentViewState();
                return;
            }

            // [Loading Panel] 싱글 흐름도 멀티와 시각 일관성 위해 panel fade in/out.
            StartCoroutine(SoloTeleportWithLoadingPanel(character, target));
        }

        private IEnumerator SoloTeleportWithLoadingPanel(Character character, Transform target)
        {
            const float FadeInWait = 0.25f;
            const float TravelDelay = 0.6f;

            LostMemory.UI.LoadingFadeOverlay.Show();
            yield return new WaitForSecondsRealtime(FadeInWait);

            TeleportCharacter(character, target.position + localTeleportOffset);
            PlayLocalTeleportBgm();
            candidates.Clear();
            requestInProgress = false;
            ApplyCurrentViewState();
            Log($"Local teleported '{character.name}' to '{target.name}'.");

            yield return new WaitForSecondsRealtime(TravelDelay);
            LostMemory.UI.LoadingFadeOverlay.Hide();
        }

        /// <summary>
        /// 멀티 모드 FireLocalTeleportClientRpc 가 도착했을 때 각 클라이언트가 호출.
        /// 자기 owned Character (LocalPlayerResolver.LocalCharacter) 를 target 위치로 이동.
        /// 모두 ready → 텔레포트 시작 시점에 LoadingFadeOverlay panel 표시,
        /// 텔레포트 후 짧은 대기 후 panel 내려감.
        /// </summary>
        public void ExecuteLocalTeleportForLocalCharacter()
        {
            if (!useLocalTeleport)
            {
                _multiReadySent = false;
                requestInProgress = false;
                ApplyCurrentViewState();
                return;
            }

            Character me = LocalPlayerResolver.LocalCharacter;
            if (me == null)
            {
                Debug.LogWarning(
                    $"[RouteNodeExitTrigger] LocalCharacter null on multi-fire for trigger '{triggerId}'.",
                    this);
                _multiReadySent = false;
                requestInProgress = false;
                ApplyCurrentViewState();
                return;
            }

            Transform target = ResolveLocalTeleportTarget();
            if (target == null)
            {
                Debug.LogWarning(
                    $"[RouteNodeExitTrigger] Local teleport target not found for trigger '{triggerId}' (multi-fire).",
                    this);
                _multiReadySent = false;
                requestInProgress = false;
                ApplyCurrentViewState();
                return;
            }

            // [Loading Panel] 모두 ready → 텔레포트 시작 시점에 panel fade in.
            // 텔레포트 즉시 실행, 짧은 대기 후 fade out.
            StartCoroutine(TeleportWithLoadingPanel(me, target));
        }

        // 텔레포트 + LoadingFadeOverlay 통합 코루틴. fade in 완료 후 텔레포트, 짧은 "이동 시간" 후 fade out.
        private IEnumerator TeleportWithLoadingPanel(Character me, Transform target)
        {
            const float FadeInWait = 0.25f;   // panel fade in 완료 대기
            const float TravelDelay = 0.6f;   // "이동 시간" — 캐릭터 워프 후 잠깐 panel 유지

            LostMemory.UI.LoadingFadeOverlay.Show();
            yield return new WaitForSecondsRealtime(FadeInWait);

            // 실제 텔레포트 — panel 이 화면 가리는 동안 캐릭터 워프
            TeleportCharacter(me, target.position + localTeleportOffset);
            PlayLocalTeleportBgm();
            candidates.Clear();
            _multiReadySent = false;
            requestInProgress = false;
            ApplyCurrentViewState();
            Log($"Local teleported (multi-fire) '{me.name}' to '{target.name}'.");

            // 이동 시간 — 새 위치 도착 후 panel 살짝 더 유지 → fade out
            yield return new WaitForSecondsRealtime(TravelDelay);
            LostMemory.UI.LoadingFadeOverlay.Hide();
        }

        private void PlayLocalTeleportBgm()
        {
            if (localTeleportBgmClip == null)
            {
                return;
            }

            StageBgmPlayer.PlayLoop(
                localTeleportBgmClip,
                localTeleportBgmId,
                localTeleportBgmVolume,
                this,
                triggerId);
        }

        private Transform ResolveLocalTeleportTarget()
        {
            if (localTeleportTarget != null)
            {
                return localTeleportTarget;
            }

            if (string.IsNullOrWhiteSpace(localTeleportTargetRootName))
            {
                return null;
            }

            GameObject root = GameObject.Find(localTeleportTargetRootName);
            return root != null ? ResolveEntryAnchor(root.transform) : null;
        }

        private Transform ResolveEntryAnchor(Transform root)
        {
            RoomEntryAnchor[] anchors = root.GetComponentsInChildren<RoomEntryAnchor>(includeInactive: true);
            for (int i = 0; i < anchors.Length; i++)
            {
                RoomEntryAnchor anchor = anchors[i];
                if (anchor != null && anchor.Matches(localTeleportAnchorTag))
                {
                    return anchor.transform;
                }
            }

            return anchors.Length > 0 ? anchors[0].transform : root;
        }

        private static void TeleportCharacter(Character character, Vector3 targetPosition)
        {
            if (character == null)
            {
                return;
            }

            TopDownController controller = character.GetComponent<TopDownController>();
            if (controller != null)
            {
                controller.SetMovement(Vector3.zero);
                controller.MovePosition(targetPosition, true);
                return;
            }

            character.transform.position = targetPosition;
        }

        private void ApplyCurrentViewState(bool hasCandidate = false)
        {
            if (triggerView == null)
            {
                return;
            }

            if (!unlocked)
            {
                triggerView.ApplyState(RouteNodeExitTriggerViewState.Locked);
                return;
            }

            // _multiReadySent 도 Transitioning 으로 표현 — 일행 대기 중인 동안 시각적으로 동일하게.
            if (requestInProgress || _multiReadySent)
            {
                triggerView.ApplyState(RouteNodeExitTriggerViewState.Transitioning);
                return;
            }

            triggerView.ApplyState(hasCandidate || candidates.Count > 0
                ? RouteNodeExitTriggerViewState.CandidateInside
                : RouteNodeExitTriggerViewState.UnlockedIdle);
        }

        private void OnDrawGizmosSelected()
        {
            RefreshReferences();
            if (trigger == null)
            {
                return;
            }

            Gizmos.color = unlocked ? new Color(0.2f, 1f, 0.4f, 0.35f) : new Color(1f, 0.4f, 0.2f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;

            if (trigger is BoxCollider2D box)
            {
                Gizmos.DrawCube(box.offset, box.size);
            }
            else if (trigger is CircleCollider2D circle)
            {
                Gizmos.DrawSphere(circle.offset, circle.radius);
            }
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[RouteNodeExitTrigger] " + message, this);
            }
        }
    }
}
