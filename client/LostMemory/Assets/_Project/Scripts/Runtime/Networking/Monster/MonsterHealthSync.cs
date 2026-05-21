using MoreMountains.TopDownEngine;
using Unity.Netcode;
using UnityEngine;

namespace LostMemory.Networking.Monster
{
    /// <summary>
    /// 몬스터 Health 의 server-authoritative sync.
    ///
    /// - server: 매 프레임 Health.CurrentHealth 변경 감지 → NetworkVariable write.
    /// - 비-server (게스트): OnValueChanged 받으면 Health.SetHealth(value).
    ///     또한 OnNetworkSpawn 에서 `Health.DamageDisabled()` 호출 → 비-server 측 자체 Damage 호출 차단
    ///     (게스트의 player hitbox 가 무심코 호출해도 무시).
    ///
    /// 사망 sync:
    ///   server 측 Damage 누적 → CurrentHealth=0 → Health.Kill() 호출 (자체 OnDeath 흐름)
    ///   → DestroyOnDeath 옵션이라면 GameObject.Destroy + NetworkObject.Despawn → 비-server 측 자동 sync 사라짐.
    ///
    /// 한계 (발표 후 follow-up):
    ///   게스트가 공격해도 데미지 0 (DamageDisabled 차단). 게스트 데미지도 적용하려면 player hitbox →
    ///   ServerRpc → host Damage 패턴이 별도로 필요.
    ///
    /// 부착: 몬스터 prefab 의 root (Health 컴포넌트 옆 + NetworkObject + NetworkTransform + MonsterNetSync 옆).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    [AddComponentMenu("Lost Memory/Networking/Monster Health Sync")]
    public sealed class MonsterHealthSync : NetworkBehaviour
    {
        [SerializeField, Tooltip("동기화할 Health 컴포넌트. 비워두면 Awake 에서 자동 검색.")]
        private Health health;

        [SerializeField, Tooltip("server 측 health 변경 감지 최소 단위. 너무 작으면 NetworkVariable broadcast 폭증.")]
        private float minDelta = 0.01f;

        [SerializeField, Tooltip("server 측 CurrentHealth=0 감지 후 NetworkObject.Despawn 까지 대기. " +
            "호스트 측 사망 애니메이션을 잠시 보여주고, 게스트도 거의 동시에 사라지도록 함.")]
        private float deathDespawnDelay = 2f;

        [SerializeField, Tooltip("OnNetworkSpawn / NetworkVariable write / OnValueChanged / Despawn 로그 출력.")]
        private bool verboseLog = false;

        private readonly NetworkVariable<float> _syncedHealth = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // Bug #36: MaximumHealth 도 NetworkVariable sync — server-side controller (EnemyDataRuntimeAdapter,
        // Skeleton*/Slime/Bat/Assassin/Bertha/Rena 등) 가 runtime 에 MaximumHealth 변경 시 client prefab default
        // 와 어긋남 → 체력바 비율 (CurrentHealth/MaximumHealth) 불일치.
        private readonly NetworkVariable<float> _syncedMaxHealth = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private bool _deathScheduled;

        private void Awake()
        {
            if (health == null) health = GetComponent<Health>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (health == null)
            {
                Debug.LogWarning($"[MonsterHealthSync] Health 컴포넌트 누락: {gameObject.name}", this);
                return;
            }

            _syncedHealth.OnValueChanged += HandleSyncedHealthChanged;
            _syncedMaxHealth.OnValueChanged += HandleSyncedMaxHealthChanged;

            if (IsServer)
            {
                _syncedHealth.Value = health.CurrentHealth;
                _syncedMaxHealth.Value = health.MaximumHealth;
                if (verboseLog) Debug.Log($"[MonsterHealthSync] OnNetworkSpawn SERVER {gameObject.name} initial={health.CurrentHealth} max={health.MaximumHealth}", this);
            }
            else
            {
                // 비-server: 자체 Damage 호출 차단 + 현재 sync 값 즉시 적용
                health.DamageDisabled();
                // Bug #36: MaximumHealth 우선 적용 — server-side controller 가 runtime scaling 한 경우 client prefab
                // default 와 다름. CurrentHealth sync 이전에 max 부터 적용해야 체력바 비율 (Cur/Max) 정확.
                if (_syncedMaxHealth.Value > 0f)
                {
                    health.MaximumHealth = _syncedMaxHealth.Value;
                    health.InitialHealth = _syncedMaxHealth.Value;
                }
                if (_syncedHealth.Value > 0f)
                {
                    health.SetHealth(_syncedHealth.Value);
                }
                if (verboseLog) Debug.Log($"[MonsterHealthSync] OnNetworkSpawn CLIENT {gameObject.name} synced={_syncedHealth.Value} max={_syncedMaxHealth.Value}", this);
            }
        }

        public override void OnNetworkDespawn()
        {
            _syncedHealth.OnValueChanged -= HandleSyncedHealthChanged;
            _syncedMaxHealth.OnValueChanged -= HandleSyncedMaxHealthChanged;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsServer || health == null) return;

            float current = health.CurrentHealth;
            if (Mathf.Abs(current - _syncedHealth.Value) >= minDelta)
            {
                if (verboseLog) Debug.Log($"[MonsterHealthSync] SERVER write {gameObject.name} {_syncedHealth.Value} -> {current}", this);
                _syncedHealth.Value = current;
            }

            // Bug #36: MaximumHealth 도 runtime 변경 감지 → sync. controller 가 spawn 후 scaling 하는 시점이
            // OnNetworkSpawn 보다 늦은 경우 (race) 대응 + 후속 buff/debuff 도 sync.
            float maxNow = health.MaximumHealth;
            if (Mathf.Abs(maxNow - _syncedMaxHealth.Value) >= minDelta)
            {
                if (verboseLog) Debug.Log($"[MonsterHealthSync] SERVER write max {gameObject.name} {_syncedMaxHealth.Value} -> {maxNow}", this);
                _syncedMaxHealth.Value = maxNow;
            }

            // 사망 시각 sync — server 측 CurrentHealth=0 후:
            //   1. 즉시 ClientRpc 발화 → 게스트 측에서 Animator SetTrigger("Death") + Collider disable
            //   2. deathDespawnDelay 후 NGO Despawn → 양측 GameObject 사라짐
            if (!_deathScheduled && current <= 0f)
            {
                _deathScheduled = true;
                if (verboseLog) Debug.Log($"[MonsterHealthSync] SERVER death detected. Broadcast visuals + Despawn in {deathDespawnDelay}s: {gameObject.name}", this);
                TriggerDeathVisualsClientRpc();
                Invoke(nameof(DespawnAfterDeathDelay), deathDespawnDelay);
            }
        }

        private void DespawnAfterDeathDelay()
        {
            if (NetworkObject == null || !NetworkObject.IsSpawned) return;
            if (verboseLog) Debug.Log($"[MonsterHealthSync] SERVER Despawn(destroy:true): {gameObject.name}", this);
            NetworkObject.Despawn(destroy: true);
        }

        private void HandleSyncedHealthChanged(float previous, float current)
        {
            // server 는 자체 Health 처리 (Damage / Kill) — NetworkVariable 은 write 용. 추가 set 불필요.
            if (IsServer) return;
            if (health == null) return;

            if (verboseLog) Debug.Log($"[MonsterHealthSync] CLIENT OnValueChanged {gameObject.name} {previous} -> {current}", this);
            health.SetHealth(current);

            // 체력바 가시화 — Health.SetHealth 안의 UpdateHealthBar(false) 는 _showBar 를 켜지 않아
            // AlwaysVisible=false 인 MMHealthBar 가 게스트 측에서 자동 표시되지 않음. show=true 로 한 번 더 호출.
            health.UpdateHealthBar(true);
        }

        private void HandleSyncedMaxHealthChanged(float previous, float current)
        {
            // server 는 자체 Health 처리. NetworkVariable 은 write 용.
            if (IsServer) return;
            if (health == null) return;
            if (current <= 0f) return;

            if (verboseLog) Debug.Log($"[MonsterHealthSync] CLIENT MaxHealth {gameObject.name} {previous} -> {current}", this);
            // Bug #36: server-side runtime scaling 결과를 client 에 적용. CurrentHealth 는 그대로 둠 — _syncedHealth 가 별도 sync.
            health.MaximumHealth = current;
            health.InitialHealth = current;
            health.UpdateHealthBar(true);
        }

        [ClientRpc]
        private void TriggerDeathVisualsClientRpc()
        {
            // 호스트는 자체 Health.Kill 흐름으로 처리. 비-server (게스트) 만 시각 효과 모방.
            if (IsServer) return;
            if (health == null) return;

            if (verboseLog) Debug.Log($"[MonsterHealthSync] CLIENT death visuals: {gameObject.name}", this);

            // TDE Health.Kill 의 시각 효과 모방. DestroyObject 는 호스트의 NGO Despawn sync 에 위임.
            Animator animator = health.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.SetTrigger("Death");
            }
            foreach (Collider2D col in health.GetComponentsInChildren<Collider2D>())
            {
                col.enabled = false;
            }
        }
    }
}
