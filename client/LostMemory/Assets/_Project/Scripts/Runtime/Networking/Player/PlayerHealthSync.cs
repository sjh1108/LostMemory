using System.Collections;
using System.Collections.Generic;
using LostMemory.TestKhi;
using MoreMountains.TopDownEngine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostMemory.Networking.Player
{
    /// <summary>
    /// 플레이어 Health 의 server-authoritative sync.
    ///
    /// - server (host): Update 에서 Health.CurrentHealth 변경 감지 → NetworkVariable write.
    /// - 비-server (게스트): OnNetworkSpawn 에서 Health.DamageDisabled() 호출 → 자체 Damage 호출 차단.
    ///     OnValueChanged → Health.SetHealth(value).
    ///
    /// 사망 sync: server 측 Damage 누적 → CurrentHealth=0 → Health.Kill() (자체 OnDeath 흐름).
    /// 게스트 측은 NetworkVariable sync 로 health=0 보고 → SetHealth 적용 → TDE Health 가 자체 처리.
    ///
    /// 부착: player prefab 의 root (NetworkObject + PlayerMovementSync 옆).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    [AddComponentMenu("Lost Memory/Networking/Player Health Sync")]
    public sealed class PlayerHealthSync : NetworkBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private float minDelta = 0.01f;

        [Header("Hit/Death sync")]
        [SerializeField, Tooltip("server 측 health 감소 시 게스트 측 Animator SetTrigger(피격 trigger) 호출 + flicker SFX 등 시각 sync.")]
        private bool syncHit = true;

        [SerializeField, Tooltip("게스트 측 피격 시 호출할 Animator Trigger 이름. Hero_Animator 에는 'Hit' 가 정의되어 있음.")]
        private string hitAnimatorTrigger = "Hit";

        [SerializeField, Tooltip("호스트의 KhiDownController.DownEntered event 발화 시 게스트 측 Animator SetTrigger(down trigger) 호출. " +
            "임계값 추측 없이 실제 EnterDown 시점을 hook.")]
        private bool syncDown = true;

        [SerializeField, Tooltip("게스트 측 down 모션용 Animator Trigger 이름. Hero_Animator 에는 'Down' 정의됨.")]
        private string downAnimatorTrigger = "Down";

        [SerializeField, Tooltip("사망 시 모든 클라에서 Animator SetTrigger + Collider2D disable + 입력 컴포넌트 disable.")]
        private bool syncDeath = true;

        [SerializeField, Tooltip("게스트 측 사망 시 호출할 Animator Trigger 이름. Hero_Animator 에는 'Down' (TDE 표준 'Death' 미사용). " +
            "다른 prefab 이면 적절한 이름으로 교체.")]
        private string deathAnimatorTrigger = "Down";

        [SerializeField, Tooltip("사망 시 disable 할 player 입력 컴포넌트 이름. 본 prefab 의 자기 + 자식에서 검색.")]
        private string[] inputComponentNamesToDisableOnDeath = new[]
        {
            "KhiMeleeComboController",
            "KhiParryController",
            "KhiDashController",
            "KhiFinisherLunge",
        };

        [SerializeField, Tooltip("owner 측 사망 시 SetActive(true) 할 UI GameObject. " +
            "예: NicknameCanvas 자식에 비활성 'You Died' 텍스트 두고 wireup. 비워두면 OnGUI fallback 으로 'You Died' 표시.")]
        private GameObject deathOverlayObject;

        [SerializeField, Tooltip("deathOverlayObject 가 wireup 안 됐을 때 OnGUI 로 표시할 사망 문구.")]
        private string deathOverlayFallbackText = "You Died";

        [SerializeField, Tooltip("OnNetworkSpawn / NetworkVariable write / OnValueChanged / Death 로그 출력.")]
        private bool verboseLog = false;

        private readonly NetworkVariable<float> _syncedHealth = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // KhiDownState 를 int 로 sync. server 가 매 프레임 _downController.CurrentState 추적 + write.
        // client 는 OnValueChanged 받아 명시적 Down/Defeated/Normal visual 처리 (event-based RPC 보조).
        private readonly NetworkVariable<int> _syncedDownState = new(
            (int)KhiDownState.Normal,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private bool _deathTriggered;
        private bool _hasBeenAlive;
        private bool _showDeathOverlayOnGui;
        private bool _ownerComponentsDisabledForDown;
        private bool _visualHiddenAfterDefeat;
        private KhiDownController _downController;
        private bool _downSubscribed;
        private bool _defeatSubscribed;

        [SerializeField, Tooltip("DefeatedByTimeout/DefeatedSolo 발화 후 모든 클라에서 player visual hide 까지 대기. " +
            "KhiDownController.defeatObjectDisableDelay (=5f) 와 일치시켜 호스트의 TDE Health destroy timing 과 sync.")]
        private float defeatHideDelaySeconds = 5f;

        // server 측 모든 PlayerHealthSync 추적 (host 만 등록). 멀티 환경 RunFailed 가드 (AnyPlayerAlive) 용도.
        private static readonly HashSet<PlayerHealthSync> _serverInstances = new HashSet<PlayerHealthSync>();

        /// <summary>
        /// server 측 등록된 player 중 하나라도 살아있으면 true.
        /// 솔로 (NGO 미시작) 또는 server 인스턴스 0개면 false.
        /// RunManager.HandlePlayerDefeatedDirect 가드용 — 멀티에서 한 명만 죽었을 때 RunFailed 차단.
        /// </summary>
        public static bool AnyPlayerAlive()
        {
            int aliveCount = 0;
            int totalCount = 0;
            foreach (PlayerHealthSync p in _serverInstances)
            {
                if (p == null) continue;
                totalCount++;
                if (!p._deathTriggered) aliveCount++;
            }
            Debug.Log($"[PlayerHealthSync.AnyPlayerAlive] alive={aliveCount}/{totalCount}");
            return aliveCount > 0;
        }

        private void Awake()
        {
            if (health == null) health = GetComponent<Health>();
            _downController = GetComponent<KhiDownController>();
        }

        // SceneManager.activeSceneChanged hook — RPC 가 LoadScene 시점 race 로 무효될 가능성 대비.
        // 자기 측에서 자동 reset 호출 (idempotent — RPC 와 중복 호출 무해).
        private void OnEnable()
        {
            SceneManager.activeSceneChanged += HandleActiveSceneChangedForReset;
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChangedForReset;
        }

        private void HandleActiveSceneChangedForReset(Scene previous, Scene current)
        {
            if (verboseLog) Debug.Log($"[PlayerHealthSync] activeSceneChanged ({previous.name} -> {current.name}) — Animator force reset. {gameObject.name}", this);

            // server 측 KhiDownController 가 Defeated 면 reset.
            if (IsServer && _downController != null && _downController.IsDefeated)
            {
                _downController.DebugRespawn();
            }

            // 사망 잔재 flag 있으면 전체 reset.
            if (_deathTriggered || _visualHiddenAfterDefeat || _ownerComponentsDisabledForDown)
            {
                ApplyLocalDeathReset();
            }
            else
            {
                // 잔재 flag 없어도 Animator 만 한 번 더 default state 강제 (LoadScene 후 Dead state 잔재 대비).
                ForceAnimatorToDefaultState();
            }
        }

        /// <summary>
        /// server 가 호출 — 모든 PlayerHealthSync server 인스턴스에 대해 사망 상태 reset ClientRpc 발화.
        /// 마을 복귀 / 다음 던전 진입 등 게스트 사망 잔재 정리용. 씬 전환 이벤트 의존성 제거 (server-authoritative 단일 트리거).
        /// </summary>
        /// <summary>
        /// 정공법 — server 가 모든 PlayerObject 를 Despawn(destroy=true) 후 새 씬 LoadScene 완료 시점에 PlayerPrefab 으로 fresh 재 spawn.
        /// DontDestroyOnLoad 인 player 가 사망 상태를 유지하는 문제를 근본 해결 (사망 잔재 거꾸로 풀기 불필요).
        /// 호스트가 LoadScene 전에 호출 — 콜백이 LoadScene 완료 후 재 spawn 처리.
        /// </summary>
        public static void RespawnPlayersAfterSceneLoad()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            GameObject playerPrefab = nm.NetworkConfig.PlayerPrefab;
            if (playerPrefab == null)
            {
                Debug.LogWarning("[PlayerHealthSync] NetworkManager.NetworkConfig.PlayerPrefab is null — fresh respawn 불가. 인스펙터에서 PlayerPrefab 등록 필요.");
                return;
            }

            // OnLoadEventCompleted — 모든 client 가 새 씬 로드 완료 후 발화. 그 시점에 fresh PlayerObject 재 spawn.
            // local function 패턴 — delegate type 명시 없이 method group 으로 register/unregister.
            void OnLoadCompleted(string sceneName, LoadSceneMode loadMode, System.Collections.Generic.List<ulong> clientsCompleted, System.Collections.Generic.List<ulong> clientsTimedOut)
            {
                nm.SceneManager.OnLoadEventCompleted -= OnLoadCompleted;
                Debug.Log($"[PlayerHealthSync] OnLoadEventCompleted scene={sceneName} — respawning {clientsCompleted.Count} player(s).");

                // ConnectedClientsIds snapshot — spawn 중 변경 대비.
                ulong[] clientIds = new ulong[nm.ConnectedClientsIds.Count];
                int idx = 0;
                foreach (ulong id in nm.ConnectedClientsIds) clientIds[idx++] = id;

                for (int i = 0; i < clientIds.Length; i++)
                {
                    ulong clientId = clientIds[i];
                    GameObject inst = GameObject.Instantiate(playerPrefab);
                    NetworkObject no = inst.GetComponent<NetworkObject>();
                    if (no == null)
                    {
                        Debug.LogWarning($"[PlayerHealthSync] PlayerPrefab missing NetworkObject. clientId={clientId}");
                        GameObject.Destroy(inst);
                        continue;
                    }
                    no.SpawnAsPlayerObject(clientId, destroyWithScene: false);
                    Debug.Log($"[PlayerHealthSync] Respawned PlayerObject for clientId={clientId}");
                }
            }
            nm.SceneManager.OnLoadEventCompleted += OnLoadCompleted;

            // 기존 PlayerObject Despawn — destroy=true 로 GameObject 자체 destroy + NGO sync 로 모든 client 도 destroy.
            ulong[] existingClientIds = new ulong[nm.ConnectedClientsIds.Count];
            int j = 0;
            foreach (ulong id in nm.ConnectedClientsIds) existingClientIds[j++] = id;

            for (int i = 0; i < existingClientIds.Length; i++)
            {
                ulong clientId = existingClientIds[i];
                if (nm.ConnectedClients.TryGetValue(clientId, out NetworkClient client) && client.PlayerObject != null)
                {
                    Debug.Log($"[PlayerHealthSync] Despawn old PlayerObject clientId={clientId}");
                    client.PlayerObject.Despawn(destroy: true);
                }
            }
        }

        public static void BroadcastResetDeathStateForAll()
        {
            foreach (PlayerHealthSync p in _serverInstances)
            {
                if (p == null) continue;
                // server 측 player GameObject 가 TDE Health 의 DelayBeforeDestruction 처리로 inactive 됐을 수 있음.
                // RPC 발화 전 active 화 (NetworkBehaviour 가 inactive 면 ClientRpc 안 보내짐).
                if (!p.gameObject.activeSelf)
                {
                    p.gameObject.SetActive(true);
                }

                // KhiDownController._state 가 Defeated 면 Normal 로 복귀 — DebugRespawn 활용 (Health.Revive + _state=Normal).
                // 이걸 안 하면 server 측 LateUpdate 가 _state=Defeated 매 프레임 추적 → _syncedDownState=Defeated 유지
                // → 모든 client 에서 Dead state 강제 → 도착 후 즉사 모션.
                if (p._downController != null && p._downController.IsDefeated)
                {
                    p._downController.DebugRespawn();
                }

                if (!p.IsSpawned) continue;
                p.ResetDeathStateClientRpc();
            }
        }

        [ClientRpc]
        private void ResetDeathStateClientRpc()
        {
            if (verboseLog) Debug.Log($"[PlayerHealthSync] Reset death state broadcast received. IsServer={IsServer} IsOwner={IsOwner} {gameObject.name}", this);
            ApplyLocalDeathReset();
        }

        /// <summary>
        /// Local 측 사망 잔재 reset — RPC 본문 + SceneManager.activeSceneChanged fallback 공용.
        /// LoadScene 시 RPC 가 NetworkObject destroy/recreate 와 race 일으킬 수 있어 scene change hook 으로 safety net.
        /// </summary>
        private void ApplyLocalDeathReset()
        {

            // 0. GameObject 자체 active 화 — TDE Health 가 inactive 처리한 경우 복구.
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            // 1. 상태 flag reset.
            _deathTriggered = false;
            _hasBeenAlive = false;
            _showDeathOverlayOnGui = false;
            _ownerComponentsDisabledForDown = false;
            _visualHiddenAfterDefeat = false;

            // 2. owner 측 사망 UI hide (deathOverlayObject wireup 된 경우).
            if (IsOwner && deathOverlayObject != null && deathOverlayObject.scene.IsValid())
            {
                deathOverlayObject.SetActive(false);
            }

            // 3. Renderer 복구 — HidePlayerVisualClientRpc 가 disable 한 것 reenable.
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) renderers[i].enabled = true;
            }

            // 4. Collider2D 복구.
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) colliders[i].enabled = true;
            }

            // 5. 입력 컴포넌트 reenable (사망 시 disable 한 것들).
            ReenableComponentsByName(inputComponentNamesToDisableOnDeath);
            // 6. owner CharacterMovement / KhiAnimatorMovementBinder / KhiSpriteFlipBinder reenable.
            if (IsOwner)
            {
                CharacterMovement cm = GetComponent<CharacterMovement>();
                if (cm != null) cm.enabled = true;
                ReenableComponentsByName(new[] { "KhiAnimatorMovementBinder", "KhiSpriteFlipBinder" });
            }

            // 7. Health 부활 + 최대 체력 복구.
            // server: Revive + SetHealth(Max) → _syncedHealth NetworkVariable 갱신 → 게스트도 sync.
            // 게스트: DamageEnabled 만 (server sync 받아 health 자동 복구). 명시적 SetHealth 도 호출 — sync 도착 전 즉시 visual.
            if (health != null)
            {
                health.DamageEnabled();
                if (IsServer)
                {
                    health.Revive();
                    health.SetHealth(health.MaximumHealth);
                }
                else
                {
                    // 게스트: server sync 보장 전 자기 측 즉시 복구 — 시각적 일관성.
                    health.SetHealth(health.MaximumHealth);
                }
            }

            // 8. Animator state 강제 reset — Dead/Down state 머물러 있는 경우 default state 복귀.
            // Rebind + Play(0) 명시적 default state 호출 두 단계 모두 시도.
            ForceAnimatorToDefaultState();

            // 9. 다음 프레임에 한 번 더 Animator reset — LoadScene/OnEnable 등으로 인한 state 변화 대응.
            if (isActiveAndEnabled)
            {
                StartCoroutine(DelayedAnimatorResetCoroutine());
            }
        }

        private void ForceAnimatorToDefaultState()
        {
            Animator a = ResolveTargetAnimator();
            if (a == null || !a.isActiveAndEnabled) return;

            if (!string.IsNullOrWhiteSpace(downAnimatorTrigger)) a.ResetTrigger(downAnimatorTrigger);
            if (!string.IsNullOrWhiteSpace(deathAnimatorTrigger)) a.ResetTrigger(deathAnimatorTrigger);
            a.Rebind();
            a.Update(0f);
            // Rebind 후에도 default state 진입 안 한 케이스 대비 — Play(0) 가 hash 0 = default state 명시.
            a.Play(0, 0, 0f);
            a.Update(0f);
        }

        private IEnumerator DelayedAnimatorResetCoroutine()
        {
            yield return null; // 다음 프레임
            ForceAnimatorToDefaultState();
        }

        private void ReenableComponentsByName(string[] names)
        {
            if (names == null) return;
            MonoBehaviour[] all = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < names.Length; i++)
            {
                string typeName = names[i];
                if (string.IsNullOrWhiteSpace(typeName)) continue;
                for (int j = 0; j < all.Length; j++)
                {
                    MonoBehaviour mb = all[j];
                    if (mb == null) continue;
                    if (mb.GetType().Name == typeName)
                    {
                        mb.enabled = true;
                    }
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (health == null)
            {
                Debug.LogWarning($"[PlayerHealthSync] Health 컴포넌트 누락: {gameObject.name}", this);
                return;
            }

            _syncedHealth.OnValueChanged += HandleSyncedHealthChanged;
            _syncedDownState.OnValueChanged += HandleDownStateChanged;

            if (IsServer)
            {
                _syncedHealth.Value = health.CurrentHealth;
                _syncedDownState.Value = _downController != null ? (int)_downController.CurrentState : (int)KhiDownState.Normal;
                if (verboseLog) Debug.Log($"[PlayerHealthSync] OnNetworkSpawn SERVER {gameObject.name} initial={health.CurrentHealth} downState={(KhiDownState)_syncedDownState.Value}", this);

                // server 만 KhiDownController.DownEntered 구독 → 발화 시 ClientRpc 로 모든 클라에 down 시각 sync.
                if (syncDown && _downController != null)
                {
                    _downController.DownEntered += HandleDownEntered;
                    _downSubscribed = true;
                }

                // server 만 DefeatedByTimeout/DefeatedSolo 구독 → defeatHideDelaySeconds 후 모든 클라에 hide RPC.
                // 호스트의 TDE Health destroy/hide 타이밍과 sync.
                if (_downController != null)
                {
                    _downController.DefeatedByTimeout += HandleDefeatedServer;
                    _downController.DefeatedSolo += HandleDefeatedServer;
                    _defeatSubscribed = true;
                }

                _serverInstances.Add(this);
            }
            else
            {
                health.DamageDisabled();
                if (_syncedHealth.Value > 0f)
                {
                    health.SetHealth(_syncedHealth.Value);
                }
                if (verboseLog) Debug.Log($"[PlayerHealthSync] OnNetworkSpawn CLIENT {gameObject.name} synced={_syncedHealth.Value} downState={(KhiDownState)_syncedDownState.Value}", this);

                // 늦게 join 한 client 가 초기 state 가 Down/Defeated 면 즉시 적용.
                KhiDownState initial = (KhiDownState)_syncedDownState.Value;
                if (initial == KhiDownState.Down) ApplyDownVisualsOnClient();
                else if (initial == KhiDownState.Defeated) ApplyDefeatedVisualsOnClient();
            }
        }

        public override void OnNetworkDespawn()
        {
            _syncedHealth.OnValueChanged -= HandleSyncedHealthChanged;
            _syncedDownState.OnValueChanged -= HandleDownStateChanged;
            if (_downSubscribed && _downController != null)
            {
                _downController.DownEntered -= HandleDownEntered;
                _downSubscribed = false;
            }
            if (_defeatSubscribed && _downController != null)
            {
                _downController.DefeatedByTimeout -= HandleDefeatedServer;
                _downController.DefeatedSolo -= HandleDefeatedServer;
                _defeatSubscribed = false;
            }
            _serverInstances.Remove(this);
            base.OnNetworkDespawn();
        }

        private void LateUpdate()
        {
            if (!IsSpawned) return;
            if (health == null) return;

            // Down / Defeated state 매 프레임 강제. 매 프레임 컴포넌트 disable 재강제 + Animator.Play(target) 강제.
            KhiDownState state = (KhiDownState)_syncedDownState.Value;
            if (state == KhiDownState.Normal) return;

            // 매 프레임 컴포넌트 disable 재강제 — 어떤 코드가 enable=true 되돌려도 즉시 reset.
            CharacterMovement cm = GetComponent<CharacterMovement>();
            if (cm != null && cm.enabled) cm.enabled = false;
            ForceDisableMovementBinders();

            // health.TargetAnimator (KhiDownController 와 동일) 우선 — health.GetComponentInChildren 은 잘못된 Animator (무기 등) 잡을 가능성.
            Animator animator = ResolveTargetAnimator();
            if (animator == null || !animator.isActiveAndEnabled) return;

            string targetState = state == KhiDownState.Defeated ? "Dead" : "Down";

            // HasState 체크 — state 없으면 Play noop (error log 차단 + SetTrigger transition 만 의존).
            int targetHash = Animator.StringToHash(targetState);
            if (!animator.HasState(0, targetHash)) return;

            bool nextIsTarget = animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(targetState);
            bool currentIsTarget = !animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).IsName(targetState);
            if (!nextIsTarget && !currentIsTarget)
            {
                animator.Play(targetState, 0, 0f);
            }
        }

        private Animator ResolveTargetAnimator()
        {
            if (health == null) return null;
            if (health.TargetAnimator != null) return health.TargetAnimator;
            return health.GetComponentInChildren<Animator>();
        }

        private void ForceDisableMovementBinders()
        {
            MonoBehaviour[] all = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < all.Length; i++)
            {
                MonoBehaviour mb = all[i];
                if (mb == null || !mb.enabled) continue;
                string typeName = mb.GetType().Name;
                if (typeName == "KhiAnimatorMovementBinder" || typeName == "KhiSpriteFlipBinder")
                {
                    mb.enabled = false;
                }
            }
        }

        private void Update()
        {
            if (!IsServer || health == null) return;

            float current = health.CurrentHealth;
            if (Mathf.Abs(current - _syncedHealth.Value) >= minDelta)
            {
                float prev = _syncedHealth.Value;
                if (verboseLog) Debug.Log($"[PlayerHealthSync] SERVER write {gameObject.name} {prev} -> {current}", this);
                _syncedHealth.Value = current;

                // 피격 sync — health 가 감소했을 때 (= 데미지) 게스트 측 시각 효과 ClientRpc 발화.
                // _hasBeenAlive 게이트 통과 후에만 (Initialization 직후의 0→100 sync 를 hit 으로 잘못 트리거하지 않도록).
                if (syncHit && _hasBeenAlive && current < prev && current > 0f)
                {
                    TriggerHitClientRpc();
                }
            }

            // 한 번이라도 살아있었음을 확인 — TDE Health 의 Initialization 전 일시적 0 으로 인한 false positive 사망 감지 차단.
            if (current > 0f) _hasBeenAlive = true;

            // KhiDownController 의 state 를 매 프레임 NetworkVariable 로 sync. event-based RPC 와 함께 state-based
            // backup 으로 동작 — late-join client 도 OnNetworkSpawn 의 initial 적용으로 정상 visual.
            if (_downController != null)
            {
                int currentDownState = (int)_downController.CurrentState;
                if (currentDownState != _syncedDownState.Value)
                {
                    if (verboseLog) Debug.Log($"[PlayerHealthSync] SERVER downState {(KhiDownState)_syncedDownState.Value} -> {(KhiDownState)currentDownState} {gameObject.name}", this);
                    _syncedDownState.Value = currentDownState;
                }
            }

            // 사망 감지 — 살아있던 적이 있고 현재 0 이하 → 모든 클라에 시각 + 입력 차단 sync.
            if (syncDeath && !_deathTriggered && _hasBeenAlive && current <= 0f)
            {
                _deathTriggered = true;
                if (verboseLog) Debug.Log($"[PlayerHealthSync] SERVER death detected. Broadcast: {gameObject.name}", this);
                TriggerDeathClientRpc();

                // 팀 전체 사망 검사 — RunManager 는 local player 한 명만 hook 이라 다른 player 사망 경로가 직접 트리거되지 않음.
                // 본 인스턴스가 막 _deathTriggered=true 가 됐으므로 다른 모든 인스턴스도 dead 면 팀 전멸.
                if (!AnyPlayerAlive() && LostMemory.Stage.RunManager.Instance != null)
                {
                    if (verboseLog) Debug.Log($"[PlayerHealthSync] SERVER team defeated — RunManager.HandlePlayerDefeatedDirect 직접 호출 + broadcast RPC delay={runFailedUiDelaySeconds}", this);
                    LostMemory.Stage.RunManager.Instance.HandlePlayerDefeatedDirect();
                    // 즉시 RPC 발화 + delay 인자 — server 측 GameObject 가 그 사이 TDE 가 inactive 시켜도 RPC 는 이미 전송됨.
                    // client 측 (호스트 자기 포함) 이 자체 coroutine 으로 delay 후 ShowResultingUI 호출.
                    BroadcastRunFailedUiClientRpc(runFailedUiDelaySeconds);
                }
            }
        }

        [SerializeField, Tooltip("팀 전멸 시 결과 UI 표시까지 대기. RunManager.failureResultingDelaySeconds (=5f) 와 동일하게 둬서 호스트/게스트 동시에 뜨도록.")]
        private float runFailedUiDelaySeconds = 5f;

        [ClientRpc]
        private void BroadcastRunFailedUiClientRpc(float delaySeconds)
        {
            // 호스트도 게스트도 같은 RPC 받음 — 단일 시간 출처 (호스트 측 코루틴 vs RPC 의 frame skew 제거).
            // server 측에서 코루틴을 돌리지 않고 즉시 RPC 발화 → client 측이 자체 coroutine 으로 delay 후 ShowResultingUI 호출.
            // 호스트 자기 player GameObject 가 TDE 의 defeatObjectDisableDelay 로 inactive 되어도 RPC 는 이미 전송됨.
            // 게스트 측 player GameObject 는 active 유지 (NGO 가 SetActive sync 안 함) — coroutine 정상 작동.
            if (verboseLog) Debug.Log($"[PlayerHealthSync] RunFailed UI RPC received delay={delaySeconds}s. IsServer={IsServer} IsOwner={IsOwner} {gameObject.name}", this);
            StartCoroutine(ShowRunFailedUiAfterDelayCoroutine(delaySeconds));
        }

        private IEnumerator ShowRunFailedUiAfterDelayCoroutine(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (LostMemory.Stage.RunManager.Instance != null)
            {
                if (verboseLog) Debug.Log($"[PlayerHealthSync] ShowRunFailedUi after delay — RunManager.ShowResultingUI 호출. IsServer={IsServer} {gameObject.name}", this);
                LostMemory.Stage.RunManager.Instance.ShowResultingUI();
            }
        }

        private void HandleSyncedHealthChanged(float previous, float current)
        {
            if (IsServer) return;
            if (health == null) return;

            if (verboseLog) Debug.Log($"[PlayerHealthSync] CLIENT OnValueChanged {gameObject.name} {previous} -> {current}", this);
            health.SetHealth(current);
            // 사망 시각 / 입력 차단 / UI 는 TriggerDeathClientRpc 에서 처리.
        }

        [ClientRpc]
        private void TriggerHitClientRpc()
        {
            // 호스트는 자체 Health.Damage 흐름으로 시각 처리. 게스트만 모방.
            if (IsServer) return;
            if (health == null) return;

            if (verboseLog) Debug.Log($"[PlayerHealthSync] CLIENT hit broadcast received. IsOwner={IsOwner} {gameObject.name}", this);

            Animator animator = health.GetComponentInChildren<Animator>();
            if (animator != null && !string.IsNullOrWhiteSpace(hitAnimatorTrigger))
            {
                animator.SetTrigger(hitAnimatorTrigger);
            }
        }

        private void HandleDownEntered(float duration)
        {
            // KhiDownController.DownEntered 는 호스트 측 자기 인스턴스 + host-side 게스트 인스턴스 둘 다에서 발화 가능.
            // 본 PlayerHealthSync 는 server 만 구독 (OnNetworkSpawn 분기). 발화 시 모든 클라에 시각 sync.
            if (!IsServer) return;
            if (verboseLog) Debug.Log($"[PlayerHealthSync] SERVER DownEntered. Broadcast: {gameObject.name}", this);
            TriggerDownClientRpc();
        }

        [ClientRpc]
        private void TriggerDownClientRpc()
        {
            // 호스트는 자체 KhiDownController.EnterDown 자체 흐름으로 시각 처리. 게스트만 모방.
            if (IsServer) return;
            if (health == null) return;

            if (verboseLog) Debug.Log($"[PlayerHealthSync] CLIENT down broadcast received. IsOwner={IsOwner} {gameObject.name}", this);

            Animator animator = health.GetComponentInChildren<Animator>();
            if (animator != null && !string.IsNullOrWhiteSpace(downAnimatorTrigger))
            {
                animator.SetTrigger(downAnimatorTrigger);
            }

            // owner 측만 — Animator parameter 갱신 컴포넌트들을 disable. Down state 유지.
            // (호스트의 host-side 게스트 인스턴스는 IsServer=true 라 위 첫 줄 return. 호스트 자기 흐름 보존.)
            if (IsOwner)
            {
                CharacterMovement cm = GetComponent<CharacterMovement>();
                if (cm != null) cm.enabled = false;

                // KhiAnimatorMovementBinder 는 자식 GameObject (MinimalCharacterModel) 의 컴포넌트.
                // 매 frame Walking/Speed/Idle Animator parameter 갱신 → Down state 에서 다른 state 로 transition 유발.
                // 그래서 같이 disable.
                MonoBehaviour[] allBehaviours = GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < allBehaviours.Length; i++)
                {
                    MonoBehaviour mb = allBehaviours[i];
                    if (mb == null) continue;
                    string typeName = mb.GetType().Name;
                    if (typeName == "KhiAnimatorMovementBinder" || typeName == "KhiSpriteFlipBinder")
                    {
                        mb.enabled = false;
                    }
                }
            }
        }

        [ClientRpc]
        private void TriggerDeathClientRpc()
        {
            if (verboseLog) Debug.Log($"[PlayerHealthSync] CLIENT death broadcast received. IsServer={IsServer} IsOwner={IsOwner} {gameObject.name}", this);

            // 비-server (게스트) — 호스트는 자체 Health.Kill 흐름으로 처리됨. 게스트만 시각 모방.
            if (!IsServer && health != null)
            {
                Animator animator = health.GetComponentInChildren<Animator>();
                if (animator != null && !string.IsNullOrWhiteSpace(deathAnimatorTrigger))
                {
                    animator.SetTrigger(deathAnimatorTrigger);
                }
                foreach (Collider2D col in health.GetComponentsInChildren<Collider2D>())
                {
                    col.enabled = false;
                }
            }

            // 모든 클라 — owner 측 입력 컴포넌트 disable.
            // (비-owner 측은 PlayerMovementSync.disableInputComponentsOnNonOwner 로 이미 disable 됨 — 중복 호출 무해.)
            DisableInputComponentsOnDeath();

            // owner 측 — 사망 UI 표시.
            if (IsOwner)
            {
                if (deathOverlayObject != null)
                {
                    // 사용자가 Inspector 에서 wireup 한 GameObject 가 prefab asset 이 아닌 scene instance 인지 검사.
                    // prefab asset 이면 SetActive 가 런타임 효과 없음 → 그 경우 OnGUI fallback 으로 대체.
                    if (deathOverlayObject.scene.IsValid())
                    {
                        deathOverlayObject.SetActive(true);
                    }
                    else
                    {
                        if (verboseLog) Debug.LogWarning($"[PlayerHealthSync] deathOverlayObject 가 prefab asset reference — OnGUI fallback 사용: {gameObject.name}", this);
                        _showDeathOverlayOnGui = true;
                    }
                }
                else
                {
                    _showDeathOverlayOnGui = true;
                }
            }
        }

        // NetworkVariable<int> _syncedDownState 의 OnValueChanged 핸들러.
        // 모든 인스턴스 (server 측 자기/타인 + client 측 자기/타인) 에서 visual sync 적용 —
        // KhiAnimatorMovementBinder 등 parameter override 컴포넌트가 owner/비-owner 양쪽에서 enable 가능하기 때문.
        private void HandleDownStateChanged(int previous, int current)
        {
            KhiDownState prevState = (KhiDownState)previous;
            KhiDownState newState = (KhiDownState)current;

            if (verboseLog) Debug.Log($"[PlayerHealthSync] DownState changed {prevState} -> {newState}. IsServer={IsServer} IsOwner={IsOwner} {gameObject.name}", this);

            if (newState == KhiDownState.Down)
            {
                ApplyDownVisualsOnClient();
            }
            else if (newState == KhiDownState.Defeated)
            {
                ApplyDefeatedVisualsOnClient();
            }
            else if (newState == KhiDownState.Normal)
            {
                ClearDownVisualsOnClient();
            }
        }

        private void ApplyDownVisualsOnClient()
        {
            if (health != null)
            {
                Animator animator = health.GetComponentInChildren<Animator>();
                if (animator != null && !string.IsNullOrWhiteSpace(downAnimatorTrigger))
                {
                    animator.SetTrigger(downAnimatorTrigger);
                }
            }

            // 모든 인스턴스 — Animator parameter override 컴포넌트 비활성. Down state transition 안 빠져나가게.
            // 호스트 측 자기 인스턴스는 KhiDownController 가 자체 처리하지만 추가 disable 도 idempotent (부작용 X, MovementForbidden 과 함께 동작).
            if (!_ownerComponentsDisabledForDown)
            {
                CharacterMovement cm = GetComponent<CharacterMovement>();
                if (cm != null) cm.enabled = false;
                DisableComponentsByName(new[] { "KhiAnimatorMovementBinder", "KhiSpriteFlipBinder" });
                _ownerComponentsDisabledForDown = true;
            }
        }

        private void ApplyDefeatedVisualsOnClient()
        {
            if (health != null)
            {
                Animator animator = health.GetComponentInChildren<Animator>();
                if (animator != null && animator.isActiveAndEnabled)
                {
                    if (!string.IsNullOrWhiteSpace(downAnimatorTrigger))
                    {
                        animator.ResetTrigger(downAnimatorTrigger);
                    }
                    // outgoing transition 없는 Dead state 로 강제 — KhiAnimatorMovementBinder 가 disable 된 상태라
                    // parameter override 없음 → Dead state 유지.
                    animator.Play("Dead", 0, 0f);
                    animator.Update(0f);
                }
                foreach (Collider2D col in health.GetComponentsInChildren<Collider2D>(true))
                {
                    col.enabled = false;
                }
            }

            DisableInputComponentsOnDeath();

            // 모든 인스턴스 — Down state 에서 disable 한 컴포넌트들 유지 (parameter override 차단).
            if (!_ownerComponentsDisabledForDown)
            {
                CharacterMovement cm = GetComponent<CharacterMovement>();
                if (cm != null) cm.enabled = false;
                DisableComponentsByName(new[] { "KhiAnimatorMovementBinder", "KhiSpriteFlipBinder" });
                _ownerComponentsDisabledForDown = true;
            }

            // 사망 UI 는 IsOwner 자기 화면에만.
            if (IsOwner)
            {
                if (deathOverlayObject != null)
                {
                    if (deathOverlayObject.scene.IsValid())
                    {
                        deathOverlayObject.SetActive(true);
                    }
                    else
                    {
                        _showDeathOverlayOnGui = true;
                    }
                }
                else
                {
                    _showDeathOverlayOnGui = true;
                }
            }
        }

        private void ClearDownVisualsOnClient()
        {
            // 부활 케이스 — 모든 인스턴스에서 컴포넌트 reenable.
            if (_ownerComponentsDisabledForDown)
            {
                CharacterMovement cm = GetComponent<CharacterMovement>();
                if (cm != null) cm.enabled = true;
                ReenableComponentsByName(new[] { "KhiAnimatorMovementBinder", "KhiSpriteFlipBinder" });
                _ownerComponentsDisabledForDown = false;
            }
        }

        // KhiDownController.DefeatedByTimeout / DefeatedSolo event 핸들러 (server only).
        // 호스트의 TDE Health 가 defeatObjectDisableDelay 후 destroy/hide 처리 → 게스트 sync 안 됨 (NGO Despawn 미발화 등).
        // 즉시 RPC 발화 + delay 인자 — server 측 GameObject 가 그 사이 destroy 되어도 RPC 는 이미 전송됨.
        // client 측이 자체 coroutine 으로 delay 후 hide.
        private void HandleDefeatedServer()
        {
            if (!IsServer) return;
            if (_visualHiddenAfterDefeat) return;
            if (verboseLog) Debug.Log($"[PlayerHealthSync] SERVER Defeated event — broadcast hide RPC delay={defeatHideDelaySeconds}s. {gameObject.name}", this);
            HidePlayerVisualWithDelayClientRpc(defeatHideDelaySeconds);
        }

        [ClientRpc]
        private void HidePlayerVisualWithDelayClientRpc(float delaySeconds)
        {
            if (_visualHiddenAfterDefeat) return;
            if (verboseLog) Debug.Log($"[PlayerHealthSync] HidePlayerVisual RPC received delay={delaySeconds}s. IsServer={IsServer} IsOwner={IsOwner} {gameObject.name}", this);
            StartCoroutine(HidePlayerVisualAfterDelayCoroutine(delaySeconds));
        }

        private IEnumerator HidePlayerVisualAfterDelayCoroutine(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            ApplyHidePlayerVisual();
        }

        private void ApplyHidePlayerVisual()
        {
            if (_visualHiddenAfterDefeat) return;
            _visualHiddenAfterDefeat = true;

            if (verboseLog) Debug.Log($"[PlayerHealthSync] ApplyHidePlayerVisual. IsServer={IsServer} IsOwner={IsOwner} {gameObject.name}", this);

            // 모든 Renderer 비활성 — model GameObject 자체는 살려두되 그리기만 멈춤 (NetworkObject 유지 → NGO 안전).
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) renderers[i].enabled = false;
            }

            // Collider 비활성 — 시체와 충돌 X.
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) colliders[i].enabled = false;
            }

            // 사망 UI 제거 — 시체와 동시에 사라지도록.
            _showDeathOverlayOnGui = false;
            if (IsOwner && deathOverlayObject != null && deathOverlayObject.scene.IsValid())
            {
                deathOverlayObject.SetActive(false);
            }
        }

        private void OnGUI()
        {
            if (!_showDeathOverlayOnGui) return;
            if (!IsOwner) return;
            if (string.IsNullOrWhiteSpace(deathOverlayFallbackText)) return;

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 80,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            style.normal.textColor = new Color(1f, 0.15f, 0.15f, 1f);

            Rect rect = new Rect(0, Screen.height / 2f - 60f, Screen.width, 120f);
            // 검정 그림자 (가독성)
            GUIStyle shadow = new GUIStyle(style);
            shadow.normal.textColor = Color.black;
            GUI.Label(new Rect(rect.x + 3f, rect.y + 3f, rect.width, rect.height), deathOverlayFallbackText, shadow);
            GUI.Label(rect, deathOverlayFallbackText, style);
        }

        private void DisableInputComponentsOnDeath()
        {
            DisableComponentsByName(inputComponentNamesToDisableOnDeath);
        }

        private void DisableComponentsByName(string[] names)
        {
            if (names == null) return;
            MonoBehaviour[] all = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < names.Length; i++)
            {
                string typeName = names[i];
                if (string.IsNullOrWhiteSpace(typeName)) continue;
                for (int j = 0; j < all.Length; j++)
                {
                    MonoBehaviour mb = all[j];
                    if (mb == null) continue;
                    if (mb.GetType().Name == typeName)
                    {
                        mb.enabled = false;
                    }
                }
            }
        }
    }
}
