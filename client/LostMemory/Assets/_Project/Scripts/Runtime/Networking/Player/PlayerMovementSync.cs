using System.Collections;
using LostMemory.Networking.Common;
using LostMemory.Rewards;
using LostMemory.Stage;
using LostMemory.TestKhi;
using MoreMountains.TopDownEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostMemory.Networking.Player
{
    /// <summary>
    /// 플레이어 prefab 의 네트워크 동기화 진입점. Phase B-2.
    ///
    /// 정책:
    ///   - 위치 동기화: <see cref="NetworkTransform.OnIsServerAuthoritative"/> 를 false 로 override 해
    ///     *owner 권위* 동작. 자기 캐릭터의 위치는 자기 클라가 결정하고 다른 측에 broadcast.
    ///   - 비-owner 캐릭터: TDE <see cref="Character.CharacterType"/> 를 AI 로 전환해
    ///     <c>InputManager</c> 가 입력을 보내지 않도록 한다. 결과적으로 자체 이동 0, 위치는
    ///     NetworkTransform 가 owner 로부터 받은 값으로만 갱신.
    ///   - owner 캐릭터: <see cref="LocalPlayerResolver.Register"/> 호출해 UI/카메라가
    ///     "내 플레이어" 를 식별 가능하게 한다.
    ///
    /// 부착 위치: 플레이어 prefab 의 *루트* (NetworkObject 와 같은 GameObject).
    /// 권장 컴포넌트 순서: NetworkObject → (TDE Character 외) → PlayerMovementSync.
    ///
    /// 후속 Phase B 에서 추가될 컴포넌트와의 관계:
    ///   - B-3 KhiPlayerStateNetSync: 행동 상태 broadcast. 본 컴포넌트와 *별도* GameObject 부착 가능.
    ///   - B-4 MeleeHitboxNetRouter: hitbox 자식 오브젝트에 부착. 본 컴포넌트와 무관.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Networking/Player Movement Sync")]
    public sealed class PlayerMovementSync : NetworkTransform
    {
        [Header("Optional Refs (Awake 자동 해석)")]
        [SerializeField] private Character character;
        [SerializeField] private KhiPlayerStateAggregator stateAggregator;

        [Header("Behavior")]
        [SerializeField, Tooltip("비-owner 캐릭터의 Character.CharacterType 을 AI 로 전환할지. " +
            "false 로 두면 InputManager 의 입력이 양쪽 캐릭터 모두에 적용되어 동작 이상 발생 가능.")]
        private bool convertNonOwnerToAi = true;

        [SerializeField, Tooltip("비-owner 캐릭터의 마우스/InputAction 직접 의존 컴포넌트(KhiMeleeCombo/Parry/Dash/FinisherLunge/WeaponPresenter)를 비활성화할지. " +
            "Phase B-2 임시 처리. Phase B-3 에서 owner 가 행동 상태를 broadcast 하면 비-owner 측 시각 효과 reproduce 로 보강 예정.")]
        private bool disableInputComponentsOnNonOwner = true;

        // NetworkTransform default = server authoritative. 우리는 owner 권위.
        protected override bool OnIsServerAuthoritative() => false;

        protected override void Awake()
        {
            base.Awake();
            ResolveRefs();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            ResolveRefs();

            // scene-placed PrefabInstance (Town_solo_Copy 의 'Player', 던전 씬의 'TestKhi_MinimalCharacter2D')
            // 가 NetworkObject 라 NGO scene sweep 으로 spawn 됨 → 여기서 LocalPlayerResolver/AlignToSpawnPoint 가
            // 잘못 실행되면 호스트 카메라가 곧 Despawn 될 캐릭터에 바인딩됨.
            // PlayerHealthSync 의 동일 가드와 짝.
            if (!NetworkObject.IsPlayerObject)
            {
                NetLog.Info("Player",
                    $"scene-placed NetworkObject — OnNetworkSpawn skipped (PlayerHealthSync 가 Despawn). {gameObject.name}",
                    this);
                return;
            }

            if (IsOwner)
            {
                AlignToSpawnPoint();

                // 씬 전환 시 PlayerObject 는 DontDestroyOnLoad 라 살아남고 position 유지 →
                // 새 씬에 진입할 때마다 active scene 의 spawn point 로 재정렬.
                SceneManager.activeSceneChanged += HandleActiveSceneChanged;

                if (stateAggregator != null)
                {
                    LocalPlayerResolver.Register(stateAggregator);
                }
                NetLog.Info("Player",
                    $"Local player spawned. ClientId={NetworkManager.LocalClientId} " +
                    $"OwnerClientId={OwnerClientId}", this);
            }
            else
            {
                if (convertNonOwnerToAi && character != null)
                {
                    character.CharacterType = Character.CharacterTypes.AI;
                }
                if (disableInputComponentsOnNonOwner)
                {
                    DisableInputComponentsForNonOwner();
                }
                NetLog.Info("Player",
                    $"Remote player spawned. OwnerClientId={OwnerClientId} " +
                    $"LocalClientId={NetworkManager.LocalClientId}", this);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
                if (stateAggregator != null)
                {
                    LocalPlayerResolver.Unregister(stateAggregator);
                }
            }
            base.OnNetworkDespawn();
        }

        private void HandleActiveSceneChanged(Scene previous, Scene current)
        {
            if (!IsOwner) return;
            AlignToSpawnPoint();
        }

        private void ResolveRefs()
        {
            if (character == null) character = GetComponent<Character>();
            if (stateAggregator == null) stateAggregator = GetComponent<KhiPlayerStateAggregator>();
        }

        /// <summary>
        /// owner 측 spawn 직후 활성 씬의 spawn point 위치로 align.
        /// 우선순위:
        ///   1. RouteNodeSpawnPoint — 던전 씬에서 StageRouteManager 가 사용하는 표준 spawn 컴포넌트
        ///   2. Tag "Respawn" — Town_solo 등 일반 씬의 fallback
        /// NetworkTransform 이 owner-authoritative 라 다음 frame 에 다른 클라에도 자동 broadcast.
        /// </summary>
        private void AlignToSpawnPoint()
        {
            // 1. RouteNodeSpawnPoint 우선 — 던전 씬은 이걸로 spawn 위치 정의.
            RouteNodeSpawnPoint routeSpawn = Object.FindFirstObjectByType<RouteNodeSpawnPoint>();
            if (routeSpawn != null)
            {
                Vector3 target = routeSpawn.transform.position;
                Debug.Log($"[PlayerMovementSync] AlignToSpawnPoint via RouteNodeSpawnPoint @ {target} (was {transform.position}) IsOwner={IsOwner} ClientId={NetworkManager.LocalClientId} {gameObject.name}", this);
                transform.position = target;
                return;
            }

            // 2. Tag "Respawn" fallback — Town_solo 등 기존 흐름.
            GameObject spawnPoint = GameObject.FindGameObjectWithTag("Respawn");
            if (spawnPoint == null)
            {
                Debug.LogWarning($"[PlayerMovementSync] AlignToSpawnPoint skipped — no RouteNodeSpawnPoint, no 'Respawn' tag. {gameObject.name}", this);
                return;
            }
            Debug.Log($"[PlayerMovementSync] AlignToSpawnPoint via Tag 'Respawn' @ {spawnPoint.transform.position} (was {transform.position}) IsOwner={IsOwner} {gameObject.name}", this);
            transform.position = spawnPoint.transform.position;
        }

        /// <summary>
        /// 게스트(non-server) 측 BossCurrentPositionStartTrigger 진입 시 호출.
        /// owner-authoritative NetworkTransform 특성상 host 가 직접 게스트 transform 옮길 수 없어,
        /// host 권위로 BeginEncounter 결정 + 모든 player 에 TeleportPlayerClientRpc 발송 패턴 사용.
        ///
        /// 호출자 = owner client 의 자기 player. ServerRpc 도착 시 host 측에서 매칭하는
        /// BossCurrentPositionStartTrigger 인스턴스의 StartBossAtCurrentPositions 호출.
        /// 씬에 여러 trigger 가 있으면 첫 번째 인스턴스 사용 (현 시점 시연 씬은 1개).
        /// </summary>
        [ServerRpc]
        public void RequestBossStartServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            Debug.Log($"[BossEntry] Request received from clientId={rpcParams.Receive.SenderClientId} via netId={NetworkObjectId}", this);

            BossCurrentPositionStartTrigger trigger = Object.FindFirstObjectByType<BossCurrentPositionStartTrigger>();
            if (trigger == null)
            {
                NetLog.Warn("Player", "RequestBossStartServerRpc — scene 에 BossCurrentPositionStartTrigger 없음.", this);
                return;
            }

            Character initiator = GetComponentInParent<Character>();
            trigger.StartBossAtCurrentPositions(initiator);
        }

        /// <summary>
        /// 게스트 측 cooperative revive 차징 완료 시 호출. host 권위로 target.ForceRevive 처리.
        ///
        /// RequireOwnership=true (default) — reviver(나) 가 자기 PlayerMovementSync 의 ServerRpc 호출.
        /// 따라서 host 측 this.transform.position == reviver 측 host 가 보는 reviver position.
        ///
        /// 검증 흐름:
        /// 1. target NetworkObjectId → NetworkObject 해결
        /// 2. target.IsPlayerObject 확인 (몬스터/씬오브젝트 차단)
        /// 3. self 부활 차단 (this NetworkObjectId == targetNetObjId)
        /// 4. 거리 재검증 (client 1.5m × 2 = 3.0m tolerance — lag 보정 + 변조 차단)
        /// 5. target KhiDownController.IsDown 확인
        /// 6. 통과 → ForceRevive. PlayerHealthSync NetworkVariable sync → 모든 client OK.
        /// </summary>
        [ServerRpc]
        public void RequestCooperativeReviveServerRpc(ulong targetNetObjId)
        {
            if (!IsServer) return;

            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null) return;

            // 1. target 해결.
            if (!nm.SpawnManager.SpawnedObjects.TryGetValue(targetNetObjId, out var targetNo))
            {
                Debug.LogWarning($"[PlayerMovementSync] Coop revive — target {targetNetObjId} 찾기 실패", this);
                return;
            }

            // 2. target 이 player.
            if (!targetNo.IsPlayerObject)
            {
                Debug.LogWarning($"[PlayerMovementSync] Coop revive — target {targetNo.name} not player", this);
                return;
            }

            // 3. self 부활 차단.
            if (NetworkObject != null && NetworkObject.NetworkObjectId == targetNetObjId)
            {
                Debug.LogWarning($"[PlayerMovementSync] Coop revive — self target 거부 (no={targetNetObjId})", this);
                return;
            }

            // 4. 거리 재검증 (anti-cheat + lag tolerance).
            const float hostDistanceTolerance = 3.0f; // client side 1.5m × 2
            float distSqr = (targetNo.transform.position - transform.position).sqrMagnitude;
            if (distSqr > hostDistanceTolerance * hostDistanceTolerance)
            {
                Debug.LogWarning($"[PlayerMovementSync] Coop revive — 거리 초과 거부 dist={Mathf.Sqrt(distSqr):F2}m tolerance={hostDistanceTolerance}m", this);
                return;
            }

            // 5. KhiDownController 해결 + Down 상태 확인.
            var dc = targetNo.GetComponent<LostMemory.TestKhi.KhiDownController>()
                  ?? targetNo.GetComponentInChildren<LostMemory.TestKhi.KhiDownController>();
            if (dc == null)
            {
                Debug.LogWarning($"[PlayerMovementSync] Coop revive — target KhiDownController 없음 ({targetNo.name})", this);
                return;
            }
            if (!dc.IsDown)
            {
                Debug.Log($"[PlayerMovementSync] Coop revive — target 이미 state={dc.CurrentState}, 무시", this);
                return;
            }

            // 6. 통과 — host 권위로 ForceRevive. NetworkVariable sync → 모든 client.
            Debug.Log($"[PlayerMovementSync] Coop revive 승인 — reviver={NetworkObjectId} target={targetNetObjId} dist={Mathf.Sqrt(distSqr):F2}m", this);
            dc.ForceRevive();
        }

        /// <summary>
        /// 게스트가 협력 부활 차징 중일 때 progress (0~1) 를 host 에 전달.
        /// host 가 target.PlayerHealthSync.BroadcastCoopReviveProgressClientRpc 발사 →
        /// 모든 client 가 target 머리 위 차징바 fill 갱신.
        ///
        /// RequireOwnership=true — 자기 PlayerMovementSync 에 호출하므로 reviver 측만 가능.
        /// 검증: target 존재 / IsPlayerObject / ratio clamp. 거리/Down 상태는 RequestCooperativeReviveServerRpc 에서 검증.
        /// throttle 0.1s 는 caller (KhiDownController) 측에서.
        /// </summary>
        [ServerRpc]
        public void SubmitCoopReviveProgressServerRpc(ulong targetNetObjId, float ratio)
        {
            if (!IsServer) return;
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null) return;
            if (!nm.SpawnManager.SpawnedObjects.TryGetValue(targetNetObjId, out var targetNo)) return;
            if (!targetNo.IsPlayerObject) return;

            var targetHs = targetNo.GetComponent<PlayerHealthSync>();
            if (targetHs == null) return;
            targetHs.BroadcastCoopReviveProgressClientRpc(Mathf.Clamp01(ratio));
        }

        /// <summary>
        /// host 측 Rena 보스 SpawnProjectile 시점에 모든 client 에 broadcast.
        /// 게스트 측에서 자기 RenaBossSpellCombatController 인스턴스에 SpawnVisualOnlyProjectile 호출 → 시각만 재현.
        /// 데미지/충돌 처리는 host 권위 — visual-only clone 은 RenaBossProjectile.SetVisualOnly(true) 로 hit 처리 skip.
        /// kindIndex 는 RenaBossSpellCombatController.CastKind enum 의 int 값.
        /// </summary>
        [ClientRpc]
        public void BroadcastBossProjectileSpawnClientRpc(int kindIndex, Vector3 spawnPos, Vector2 direction, ulong homingTargetNoId, Vector2 homingTargetOffset, double hostStartedNetworkTime)
        {
            // host 는 이미 자기 측에서 SpawnProjectile 호출됨 — 중복 spawn 방지.
            if (IsHost) return;

            LostMemory.Enemies.Boss.Rena.RenaBossSpellCombatController spellCtrl =
                UnityEngine.Object.FindFirstObjectByType<LostMemory.Enemies.Boss.Rena.RenaBossSpellCombatController>();
            if (spellCtrl == null) return;
            spellCtrl.SpawnVisualOnlyProjectile(kindIndex, spawnPos, direction, homingTargetNoId, homingTargetOffset, hostStartedNetworkTime);
        }

        /// <summary>
        /// host 측 Rena 보스 fireball 폭발 시점 broadcast.
        /// 게스트 측 visual-only clone 중 위치가 가장 가까운 것을 찾아 hit 애니메이션 trigger.
        /// </summary>
        [ClientRpc]
        public void BroadcastBossProjectileExplosionClientRpc(Vector3 explosionPos, int visualPhaseIndex)
        {
            // Host 는 이미 local 에서 폭발 처리됨 — 중복 skip
            if (IsHost) return;

            var spellCtrl = UnityEngine.Object.FindFirstObjectByType<LostMemory.Enemies.Boss.Rena.RenaBossSpellCombatController>();
            if (spellCtrl == null) return;
            spellCtrl.TriggerProjectileExplosionVisual(explosionPos, visualPhaseIndex);
        }

        /// <summary>
        /// host 측 Rena 보스 SpawnThunderboltBeam 시점 broadcast. 게스트 측 visual-only beam 생성.
        /// </summary>
        [ClientRpc]
        public void BroadcastBossThunderboltClientRpc(Vector3 origin, Vector2 direction, float duration, float rotationDegrees, int sortingOrderOffset)
        {
            if (IsHost) return;

            var spellCtrl = UnityEngine.Object.FindFirstObjectByType<LostMemory.Enemies.Boss.Rena.RenaBossSpellCombatController>();
            if (spellCtrl == null) return;
            spellCtrl.SpawnVisualOnlyThunderboltBeam(origin, direction, duration, rotationDegrees, sortingOrderOffset);
        }

        /// <summary>
        /// host 측 Rena 보스 SpawnThunderStrikeArea 시점 broadcast. 게스트 측 visual-only area 생성.
        /// </summary>
        [ClientRpc]
        public void BroadcastBossThunderStrikeAreaClientRpc(Vector3 position, float warningDuration)
        {
            if (IsHost) return;

            var spellCtrl = UnityEngine.Object.FindFirstObjectByType<LostMemory.Enemies.Boss.Rena.RenaBossSpellCombatController>();
            if (spellCtrl == null) return;
            spellCtrl.SpawnVisualOnlyThunderStrikeArea(position, warningDuration);
        }

        /// <summary>
        /// host 측 Rena 보스 사망 시 broadcast. 게스트 측 보스 death animator state 재생.
        /// NetworkAnimator 가 비활성이라 직접 trigger 전송 필요.
        /// </summary>
        [ClientRpc]
        public void BroadcastBossDeathClientRpc()
        {
            if (IsHost) return;

            var encounterCtrl = UnityEngine.Object.FindFirstObjectByType<LostMemory.Enemies.Boss.Rena.RenaBossEncounterController>();
            if (encounterCtrl == null) return;
            encounterCtrl.ApplyRemoteDeath();
        }

        /// <summary>
        /// host 측 Rena 보스 PhaseTwoTransitionRoutine 시작 시 broadcast. 게스트 측 보스 phase 2 intro animation 재생.
        /// </summary>
        [ClientRpc]
        public void BroadcastBossPhaseTransitionStartClientRpc()
        {
            if (IsHost) return;

            var spellCtrl = UnityEngine.Object.FindFirstObjectByType<LostMemory.Enemies.Boss.Rena.RenaBossSpellCombatController>();
            if (spellCtrl == null) return;
            spellCtrl.ApplyRemotePhaseTransitionStart();
        }

        /// <summary>
        /// host 측 Rena 보스 PhaseTwoTransitionRoutine 종료 시 broadcast. 게스트 측 보스 idle state 진입 + _isPhaseTwo=true.
        /// </summary>
        [ClientRpc]
        public void BroadcastBossPhaseTransitionEndClientRpc()
        {
            if (IsHost) return;

            var spellCtrl = UnityEngine.Object.FindFirstObjectByType<LostMemory.Enemies.Boss.Rena.RenaBossSpellCombatController>();
            if (spellCtrl == null) return;
            spellCtrl.ApplyRemotePhaseTransitionEnd();
        }

        /// <summary>
        /// host 측 Rena 보스 SpawnIceSweepRowWarnings 시점 broadcast. 게스트 측 visual-only row warning 생성.
        /// pulse 깜빡임은 NetworkTime 기준이라 host/guest 동기화.
        /// </summary>
        [ClientRpc]
        public void BroadcastBossIceSweepRowWarningsClientRpc(Vector3 areaCenter, float bottom, float cellHeight, Vector2 damageRowSize, int rowCount, int safeRow, float warningDuration, bool sweepLeftToRight)
        {
            if (IsHost) return;

            var spellCtrl = UnityEngine.Object.FindFirstObjectByType<LostMemory.Enemies.Boss.Rena.RenaBossSpellCombatController>();
            if (spellCtrl == null) return;
            spellCtrl.SpawnVisualOnlyIceSweepRowWarnings(areaCenter, bottom, cellHeight, damageRowSize, rowCount, safeRow, warningDuration, sweepLeftToRight);
        }

        /// <summary>
        /// host 측 Rena 보스 SpawnIcePillarCell 시점 broadcast. 게스트 측 visual-only pillar 생성.
        /// </summary>
        [ClientRpc]
        public void BroadcastBossIcePillarCellClientRpc(Vector3 center, Vector2 size, bool showVisual)
        {
            if (IsHost) return;

            var spellCtrl = UnityEngine.Object.FindFirstObjectByType<LostMemory.Enemies.Boss.Rena.RenaBossSpellCombatController>();
            if (spellCtrl == null) return;
            spellCtrl.SpawnVisualOnlyIcePillarCell(center, size, showVisual);
        }

        /// <summary>
        /// host 측 BossIntroSequenceController.UnfreezeCachedPlayers 시점에 모든 client 에 broadcast.
        /// 각 owner client 가 자기 측 Character.UnFreeze 호출 → 게스트 입력 복구.
        /// Freeze 는 GatherPlayers 의 TeleportPlayerClientRpc(freeze=true) 가 이미 broadcast 처리하므로
        /// 본 ClientRpc 는 Unfreeze 만 담당. owner-auth 라 owner client 측 호출만 의미 있음.
        /// </summary>
        [ClientRpc]
        public void UnfreezePlayerClientRpc()
        {
            // 견고성 — IsOwner 가드 제거. Character.UnFreeze 는 idempotent (Frozen state 만 체크 후 복구) 라
            // 모든 client 가 자기 인스턴스 unfreeze 호출해도 안전. 이전엔 IsOwner 가드로 막혀서
            // ownership 변경/타이밍 race 시 영구 freeze 가능성 있었음.
            Debug.Log($"[Unfreeze] recv netId={NetworkObjectId} owner={IsOwner} local={IsLocalPlayer} server={IsServer} go={gameObject.name}", this);

            Character ch = GetComponentInParent<Character>();
            if (ch == null)
            {
                Debug.LogWarning($"[Unfreeze] Character not found on parent. netId={NetworkObjectId} go={gameObject.name}", this);
                return;
            }
            ch.UnFreeze();
            Debug.Log($"[Unfreeze] Character.UnFreeze 호출 완료. condition={ch.ConditionState?.CurrentState}", this);
        }

        /// <summary>
        /// host 측 BossIntroSequenceController 의 SetIntroVisibility 호출 시 broadcast.
        /// 모든 client 가 자기 측 BossIntroSequenceController 찾아 visibility 적용 → Visual reveal/hide sync.
        /// RunIntroSequence 가 host 측에서만 진행되어 게스트 측 보스 invisible 문제 fix.
        /// ClientRpc 는 owner 무관 모든 client 수신 — 어느 PlayerMovementSync 인스턴스에서 호출하든 동일.
        /// </summary>
        [ClientRpc]
        public void BroadcastBossIntroVisibilityClientRpc(bool isVisible)
        {
            BossIntroSequenceController controller = UnityEngine.Object.FindFirstObjectByType<BossIntroSequenceController>();
            if (controller == null)
            {
                return;
            }
            controller.ApplyRemoteIntroVisibility(isVisible);
        }

        /// <summary>
        /// host 가 BeginEncounter 시점에 모든 player 의 본 ClientRpc 호출 — 각 owner client 가
        /// 자기 측에서 자기 player 를 worldPosition 으로 텔레포트.
        /// owner-auth NetworkTransform 이라 owner 측 transform 변경만이 정상 sync 됨.
        ///
        /// Reward panel 떠있으면 닫힐 때까지 대기 후 텔레포트 — 보상 선택 누락 방지.
        /// 다른 player 가 trigger 밟아도 자기 reward 못 받고 끌려가는 일 없음.
        /// </summary>
        [ClientRpc]
        public void TeleportPlayerClientRpc(Vector3 worldPosition, bool freezeAfter)
        {
            Debug.Log($"[Freeze][Teleport] recv netId={NetworkObjectId} owner={IsOwner} freeze={freezeAfter} pos={worldPosition} go={gameObject.name}", this);

            if (!IsOwner) return;

            RewardPanelView rewardPanel = Object.FindFirstObjectByType<RewardPanelView>(FindObjectsInactive.Exclude);
            if (rewardPanel != null && rewardPanel.gameObject.activeInHierarchy)
            {
                StartCoroutine(WaitForRewardCloseThenTeleport(worldPosition, freezeAfter, rewardPanel));
                return;
            }

            PerformBossEntryTeleport(worldPosition, freezeAfter);
            Debug.Log($"[Freeze][Teleport] PerformBossEntryTeleport 완료 netId={NetworkObjectId} freezeAfter={freezeAfter}", this);
        }

        private IEnumerator WaitForRewardCloseThenTeleport(Vector3 worldPosition, bool freezeAfter, RewardPanelView rewardPanel)
        {
            // reward panel 이 비활성될 때까지 polling. 카드 선택 후 RewardPanelView.OnCardSelected 가 SetActive(false) 호출.
            while (rewardPanel != null && rewardPanel.gameObject.activeInHierarchy)
            {
                yield return null;
            }
            PerformBossEntryTeleport(worldPosition, freezeAfter);
        }

        private void PerformBossEntryTeleport(Vector3 worldPosition, bool freezeAfter)
        {
            Character ch = GetComponentInParent<Character>();
            if (ch == null) return;

            TopDownController controller = ch.GetComponent<TopDownController>();
            if (controller != null)
            {
                controller.SetMovement(Vector3.zero);
                controller.MovePosition(worldPosition, true);
            }
            else
            {
                ch.transform.position = worldPosition;
            }

            if (freezeAfter)
            {
                ch.Freeze();
            }
        }

        private void DisableInputComponentsForNonOwner()
        {
            DisableIfPresent<KhiMeleeComboController>();
            DisableIfPresent<KhiParryController>();
            DisableIfPresent<KhiDashController>();
            DisableIfPresent<KhiFinisherLunge>();
            // Phase B: WeaponModeController 는 자체 IsOwner 가드 (Update / CycleMode / SetMode) 있지만
            // 안전망으로 비-owner 의 input Update 자체를 차단. visual 적용은 NetworkVariable.OnValueChanged
            // 가 별도로 처리하므로 enabled=false 여도 sync OK.
            DisableIfPresent<WeaponModeController>();
            // Phase E: Staff / Flame 도 owner-only 입력. Update IsOwner gate 가 1차 방어,
            // 본 disable 이 2차 안전망. WeaponModeController 모드 전환 시 enabled 토글 충돌 가능성은
            // WeaponModeController 가 owner-write NetworkVariable 동기화로 비-owner 측 모드를 표시만 함
            // (실제 입력은 owner 만 → 비-owner enabled=false 무관).
            DisableIfPresent<KhiStaffController>();
            DisableIfPresent<KhiFlamethrowerController>();
            // KhiWeaponPresenter 는 비활성하지 않음 — Update 에서 KhiPlayerAim.GetAimDirection()
            // (NetworkVariable sync 값) 받아 무기 회전 적용. non-owner 측에서도 무기 위치/방향이
            // owner 의 마우스 방향을 따라가도록 한다 (Bug #22 의 일부).
        }

        private void DisableIfPresent<T>() where T : MonoBehaviour
        {
            T component = GetComponent<T>();
            if (component != null) component.enabled = false;
        }

        private void DisableInChildrenIfPresent<T>() where T : MonoBehaviour
        {
            T[] components = GetComponentsInChildren<T>(true);
            for (int i = 0; i < components.Length; i++)
            {
                components[i].enabled = false;
            }
        }
    }
}
