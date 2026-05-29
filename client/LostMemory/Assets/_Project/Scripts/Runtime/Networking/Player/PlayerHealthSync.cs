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

        [SerializeField, Tooltip("부활 시 모든 클라(특히 mirror)에서 호출할 Animator Trigger 이름. Hero_Animator 에 'Revive' 정의됨. " +
            "KhiDownController.reviveAnimatorTriggerName 과 일치시켜야 호스트 본인 + mirror 양쪽 흐름 sync.")]
        private string reviveAnimatorTrigger = "Revive";

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

        // B-2: 4인 동시 사망 race — BroadcastRunFailedUiClientRpc 중복 차단.
        private static bool _runFailedBroadcastFired;

        /// <summary>RunFailed 가드 리셋. RunManager.StartRun / ReturnToTown / 부활 성공 시 호출.</summary>
        public static void ResetRunFailedBroadcastGuard()
        {
            _runFailedBroadcastFired = false;
        }

        /// <summary>
        /// 결과창의 Lobby 버튼 핸들러 진입점 — 솔로/호스트/게스트 어느 측에서 호출돼도 정상 동작.
        /// 솔로/호스트: 즉시 RunManager.ReturnToTown() (saveRunRewards=true).
        /// 게스트: 자기 LocalPlayerObject 의 PlayerHealthSync 를 찾아 ServerRpc 발사 → 호스트에서 실제 전환 수행.
        /// </summary>
        public static void RequestReturnFromAnyClient()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening)
            {
                // 솔로 — 직접 호출.
                LostMemory.Stage.RunManager.Instance?.ReturnToTown();
                return;
            }
            if (nm.IsServer)
            {
                // 호스트 — 직접 호출. NGO LoadScene 으로 게스트도 sync.
                LostMemory.Stage.RunManager.Instance?.ReturnToTown();
                return;
            }
            // 게스트 — 자기 LocalPlayerObject 의 PlayerHealthSync 를 찾아 ServerRpc.
            NetworkObject localPlayer = nm.LocalClient?.PlayerObject;
            if (localPlayer == null)
            {
                Debug.LogWarning("[PlayerHealthSync] RequestReturnFromAnyClient: LocalClient.PlayerObject 없음 — 게스트 요청 무시.");
                return;
            }
            PlayerHealthSync sync = localPlayer.GetComponent<PlayerHealthSync>();
            if (sync == null)
            {
                Debug.LogWarning("[PlayerHealthSync] RequestReturnFromAnyClient: LocalPlayerObject 에 PlayerHealthSync 없음.");
                return;
            }
            sync.RequestReturnServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestReturnServerRpc(ServerRpcParams rpcParams = default)
        {
            // server-side only — 게스트가 결과창에서 Lobby 버튼 누를 때 호출됨.
            LostMemory.Stage.RunManager rm = LostMemory.Stage.RunManager.Instance;
            if (rm == null)
            {
                if (verboseLog) Debug.Log("[PlayerHealthSync] RequestReturnServerRpc — RunManager null. ignore.");
                return;
            }
            // Resulting 상태(정상 사망 결과) → saveRunRewards=true.
            // 그 외 상태(예: ESC abandon 미구현 게이트 우회) → 그래도 saveRunRewards=true 로 동일 처리.
            // 실제 분기는 RunManager 측에서 결정.
            if (verboseLog) Debug.Log($"[PlayerHealthSync] RequestReturnServerRpc 수신 (from clientId={rpcParams.Receive.SenderClientId}). ReturnToTown 호출.");
            rm.ReturnToTown();
        }

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

            // scene-placed TestKhi Player 인스턴스가 NGO sweep 으로 NetworkObject register 되어 추가 player 처럼 보이는 문제 회피.
            // 진짜 PlayerObject 는 SpawnAsPlayerObject 로 spawn 되어 IsPlayerObject=true.
            // scene-placed 는 IsPlayerObject=false → host 측에서 즉시 Despawn (모든 client 자동 sync).
            if (!NetworkObject.IsPlayerObject && IsServer)
            {
                Debug.LogWarning($"[PlayerHealthSync] scene-placed Player 가 PlayerObject 아님 — Despawn. {gameObject.name}", this);
                NetworkObject.Despawn(destroy: true);
                return;
            }

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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // GodMode enforce — server 측만, 매 프레임 모든 player.Health.Invulnerable=true 강제.
            // CompleteRevive 가 false 로 set 해도 다음 프레임에 복원.
            // 인스턴스마다 호출되지만 EnforceGodModeOnAllServerInstances 가 static 이라 한 번만 작동.
            if (IsServer && GodModeActive) EnforceGodModeOnAllServerInstances();
#endif

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

        private static int _lastGodModeToggleFrame = -1;
        // 시연 디버그 — F6 토글 상태. static 이라 모든 인스턴스 공유. CompleteRevive 가 Invulnerable=false 로 set 해도
        // 매 프레임 server LateUpdate 가 다시 true 로 강제 enforce 함.
        // KhiParryHealth.Damage 도 이 flag 보고 진입 가드 (TDE Invulnerable 우회 경로 차단 안전망).
        public static bool GodModeActive { get; private set; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static bool IsF6PressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.f6Key.wasPressedThisFrame) return true;
#endif
            try { if (Input.GetKeyDown(KeyCode.F6)) return true; }
            catch (System.InvalidOperationException) { }
            return false;
        }

        /// <summary>
        /// server 측에서만 호출 — God Mode toggle.
        /// </summary>
        private static void ToggleGodModeForAllOnServer()
        {
            GodModeActive = !GodModeActive;
            EnforceGodModeOnAllServerInstances();
            Debug.Log($"[PlayerHealthSync] DEBUG F6 — God Mode {(GodModeActive ? "ON" : "OFF")} applied to {_serverInstances.Count} player(s)");
        }

        /// <summary>
        /// 매 프레임 LateUpdate 에서 호출 — GodMode ON 이면 모든 server 측 player.Health.Invulnerable=true 강제.
        /// CompleteRevive 등에서 false 로 set 해도 다음 프레임에 복원.
        /// </summary>
        private static void EnforceGodModeOnAllServerInstances()
        {
            foreach (var p in _serverInstances)
            {
                if (p == null || p.health == null) continue;
                if (GodModeActive)
                {
                    if (!p.health.Invulnerable) p.health.Invulnerable = true;
                }
            }
        }
#endif

        /// <summary>
        /// 게스트가 F6 누르면 ServerRpc 발사 → host 측에서 모든 player 무적 토글.
        /// RequireOwnership=false — owner 인 자기 인스턴스에서 호출하지만 안전을 위해 명시.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestToggleGodModeServerRpc()
        {
            if (!IsServer) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ToggleGodModeForAllOnServer();
#endif
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // F6 폴링 — 호스트면 직접 토글, 게스트면 ServerRpc.
            // 매 인스턴스 (호스트 측 자기 + 게스트 mirror 등) 가 Update 호출되므로 static frame guard 로 중복 차단.
            if (Time.frameCount != _lastGodModeToggleFrame && IsF6PressedThisFrame())
            {
                _lastGodModeToggleFrame = Time.frameCount;
                if (IsServer)
                {
                    ToggleGodModeForAllOnServer();
                }
                else if (IsOwner) // 게스트는 자기 인스턴스에서만 한 번 ServerRpc 발사
                {
                    RequestToggleGodModeServerRpc();
                }
            }
#endif

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
                // B-2: 4인 race 가드 — broadcast 중복 차단.
                if (!_runFailedBroadcastFired && !AnyPlayerAlive() && LostMemory.Stage.RunManager.Instance != null)
                {
                    _runFailedBroadcastFired = true;
                    if (verboseLog) Debug.Log($"[PlayerHealthSync] SERVER team defeated — broadcast RPC delay={runFailedUiDelaySeconds}", this);
                    LostMemory.Stage.RunManager.Instance.HandlePlayerDefeatedDirect();
                    BroadcastRunFailedUiClientRpc(runFailedUiDelaySeconds);
                }
            }
        }

        [SerializeField, Tooltip("팀 전멸 시 결과 UI 표시까지 대기. RunManager.failureResultingDelaySeconds (=10f) 와 동일하게 둬서 호스트/게스트 동시에 뜨도록. " +
            "이 wait 중에는 협력/기억 부활 가능 — wait 종료 시 AnyPlayerAlive 검사하여 부활 성공이면 결과창 abort.")]
        private float runFailedUiDelaySeconds = 10f;

        // 부활 race 동기화 플래그 — 서버가 wait 중 부활 감지 시 CancelRunFailedUiClientRpc 로 모든 클라에 abort 신호.
        // 각 클라의 ShowRunFailedUiAfterDelayCoroutine 가 wait 종료 후 본 플래그를 검사.
        private static bool _runFailedUiAborted;

        [ClientRpc]
        private void BroadcastRunFailedUiClientRpc(float delaySeconds)
        {
            // 호스트도 게스트도 같은 RPC 받음 — 단일 시간 출처 (호스트 측 코루틴 vs RPC 의 frame skew 제거).
            // 새 broadcast 시작 — 이전 abort 플래그 reset.
            _runFailedUiAborted = false;
            if (verboseLog) Debug.Log($"[PlayerHealthSync] RunFailed UI RPC received delay={delaySeconds}s. IsServer={IsServer} IsOwner={IsOwner} {gameObject.name}", this);
            StartCoroutine(ShowRunFailedUiAfterDelayCoroutine(delaySeconds));
            // server 측 — wait 중 부활 폴링 코루틴 병행 시작. 감지 시 CancelRunFailedUiClientRpc broadcast.
            if (IsServer)
            {
                StartCoroutine(ServerPollReviveDuringWaitCoroutine(delaySeconds));
            }
        }

        /// <summary>
        /// 서버 측 wait 중 부활 폴링 — 0.5s 간격으로 AnyPlayerAlive 검사.
        /// 부활 감지 시 CancelRunFailedUiClientRpc 발화 + ResetRunFailedBroadcastGuard.
        /// wait 다 소비될 때까지 부활 없으면 그냥 종료 (client coroutine 들이 정상적으로 UI 표시).
        /// </summary>
        private IEnumerator ServerPollReviveDuringWaitCoroutine(float duration)
        {
            float elapsed = 0f;
            const float pollInterval = 0.5f;
            while (elapsed < duration)
            {
                yield return new WaitForSeconds(pollInterval);
                elapsed += pollInterval;
                if (!IsServer) yield break;  // 호스트 이탈 등
                if (AnyPlayerAlive())
                {
                    if (verboseLog) Debug.Log("[PlayerHealthSync] SERVER 부활 감지 (wait 중) — CancelRunFailedUiClientRpc broadcast.", this);
                    CancelRunFailedUiClientRpc();
                    ResetRunFailedBroadcastGuard();
                    yield break;
                }
            }
        }

        [ClientRpc]
        private void CancelRunFailedUiClientRpc()
        {
            _runFailedUiAborted = true;
            if (verboseLog) Debug.Log($"[PlayerHealthSync] CancelRunFailedUi RPC 수신 — 결과창 표시 abort 예약. IsServer={IsServer} {gameObject.name}", this);
        }

        private IEnumerator ShowRunFailedUiAfterDelayCoroutine(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            // 부활 race 가드 — 서버의 ServerPollReviveDuringWaitCoroutine 가 wait 중 cancel RPC 보냈으면 abort.
            if (_runFailedUiAborted)
            {
                if (verboseLog) Debug.Log($"[PlayerHealthSync] ShowRunFailedUi — abort 플래그 set, 결과창 표시 skip. {gameObject.name}", this);
                yield break;
            }
            // server 추가 안전망 — 서버에선 최종 alive 검사 한 번 더 (poll 간격 사이 부활 캐치).
            if (IsServer && AnyPlayerAlive())
            {
                if (verboseLog) Debug.Log($"[PlayerHealthSync] ShowRunFailedUi — server 최종 검사에서 부활 감지, abort + broadcast cancel.", this);
                CancelRunFailedUiClientRpc();
                ResetRunFailedBroadcastGuard();
                yield break;
            }
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
            // (player_died analytics 발화는 RunManager 가 KhiDownController.Defeated* 이벤트 구독으로 처리 — 여기서는 박지 않음.)
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

            // CRITICAL: mirror (non-owner) 측 KhiDownController._state 도 sync —
            // 그래야 동료가 R hold 시 FindNearestDownAlly 가 IsDown=true 로 발견.
            // 호스트 측 자기 인스턴스는 KhiDownController 가 이미 EnterDown/ForceRevive 로 _state 변경했으므로 idempotent.
            if (_downController != null)
            {
                _downController.ApplyDownStateFromNetwork(newState);
            }

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
            // 부활 sync — 호스트 본인 측은 KhiDownController.CompleteRevive 가 직접 "Revive" trigger 발동.
            // mirror (게스트 화면의 호스트 / 호스트 화면의 게스트) 측은 OnValueChanged 흐름이 별도라 여기서 trigger 발동 필요.
            // 안 그러면 Animator 가 Down state 에 그대로 멈춰서 시각적으로 부활 안 됨.
            Animator animator = ResolveTargetAnimator();
            if (animator != null && animator.isActiveAndEnabled)
            {
                if (!string.IsNullOrWhiteSpace(downAnimatorTrigger))
                {
                    animator.ResetTrigger(downAnimatorTrigger);
                }
                if (!string.IsNullOrWhiteSpace(deathAnimatorTrigger))
                {
                    animator.ResetTrigger(deathAnimatorTrigger);
                }
                if (!string.IsNullOrWhiteSpace(reviveAnimatorTrigger))
                {
                    animator.SetTrigger(reviveAnimatorTrigger);
                }

                // Animator Controller 에 "Down → ... " 의 Revive transition 이 wireup 안 됐을 때 stuck 방지 fallback.
                // 호스트 본인 측은 KhiDownController.EnsureNotStuckInDownAnimationCoroutine 으로 동일 처리. mirror 측은 별도 처리 필요.
                if (isActiveAndEnabled) StartCoroutine(EnsureMirrorRevivedNotStuckCoroutine());
            }

            // 부활 케이스 — 모든 인스턴스에서 컴포넌트 reenable.
            if (_ownerComponentsDisabledForDown)
            {
                CharacterMovement cm = GetComponent<CharacterMovement>();
                if (cm != null) cm.enabled = true;
                ReenableComponentsByName(new[] { "KhiAnimatorMovementBinder", "KhiSpriteFlipBinder" });
                _ownerComponentsDisabledForDown = false;
            }

            // Collider2D 도 reenable (ApplyDefeatedVisualsOnClient 가 disable 했을 가능성 — 시각적 잔재 차단).
            Collider2D[] cols = GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < cols.Length; i++) if (cols[i] != null) cols[i].enabled = true;

            // 부활 후 Renderer 가 ApplyHidePlayerVisual 로 disable 된 상태면 복구 (Down→Normal 가 가능성 적지만 안전망).
            if (_visualHiddenAfterDefeat)
            {
                _visualHiddenAfterDefeat = false;
                Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = true;
            }
        }

        private IEnumerator EnsureMirrorRevivedNotStuckCoroutine()
        {
            // Animator transition 평가 시간 확보.
            yield return new WaitForSeconds(0.4f);
            Animator a = ResolveTargetAnimator();
            if (a == null || !a.isActiveAndEnabled) yield break;
            // 부활 sync 후 다시 Down 으로 갔으면 (예: 또 죽음) 손대지 않음.
            if ((KhiDownState)_syncedDownState.Value != KhiDownState.Normal) yield break;

            var cur = a.GetCurrentAnimatorStateInfo(0);
            bool stuck = cur.IsName("Down") || cur.IsName("Dead");
            if (!stuck) yield break;

            if (verboseLog) Debug.Log($"[PlayerHealthSync] Mirror revive — Animator stuck (state hash={cur.shortNameHash}), force Rebind. {gameObject.name}", this);
            // KhiAnimatorMovementBinder 등은 이미 reenable. Rebind → Idle/default state 자연 복귀.
            if (!string.IsNullOrWhiteSpace(downAnimatorTrigger)) a.ResetTrigger(downAnimatorTrigger);
            if (!string.IsNullOrWhiteSpace(deathAnimatorTrigger)) a.ResetTrigger(deathAnimatorTrigger);
            a.Rebind();
            a.Update(0f);
            a.Play(0, 0, 0f);
            a.Update(0f);
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

        /// <summary>
        /// 협력 부활 차징 progress 를 모든 client 에 broadcast.
        /// host 가 호출 — 자기 측 KhiDownController (호스트가 살리는 경우) 또는
        /// 게스트의 PlayerMovementSync.SubmitCoopReviveProgressServerRpc 가 host 측에서 호출.
        /// 받는 쪽: 자기 측 target.KhiDownController.ApplyCoopReviveProgressFromNetwork → bar fill.
        /// </summary>
        [ClientRpc]
        public void BroadcastCoopReviveProgressClientRpc(float ratio)
        {
            if (_downController != null)
            {
                _downController.ApplyCoopReviveProgressFromNetwork(Mathf.Clamp01(ratio));
            }
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
