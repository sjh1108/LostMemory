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

        private readonly NetworkVariable<float> _syncedHealth = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

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

            if (IsServer)
            {
                _syncedHealth.Value = health.CurrentHealth;
            }
            else
            {
                // 비-server: 자체 Damage 호출 차단 + 현재 sync 값 즉시 적용
                health.DamageDisabled();
                if (_syncedHealth.Value > 0f)
                {
                    health.SetHealth(_syncedHealth.Value);
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            _syncedHealth.OnValueChanged -= HandleSyncedHealthChanged;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsServer || health == null) return;

            float current = health.CurrentHealth;
            if (Mathf.Abs(current - _syncedHealth.Value) >= minDelta)
            {
                _syncedHealth.Value = current;
            }
        }

        private void HandleSyncedHealthChanged(float previous, float current)
        {
            // server 는 자체 Health 처리 (Damage / Kill) — NetworkVariable 은 write 용. 추가 set 불필요.
            if (IsServer) return;
            if (health == null) return;

            health.SetHealth(current);
            // 사망 sync 는 server 측 Health.Kill → NetworkObject.Despawn 자동 흐름에 위임.
            // 비-server 측에서 Kill 명시 호출 X (NGO unauthorized destroy 에러 회피).
        }
    }
}
