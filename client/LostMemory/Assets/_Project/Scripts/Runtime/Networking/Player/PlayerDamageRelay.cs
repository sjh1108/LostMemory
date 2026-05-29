using LostMemory.Combat;
using MoreMountains.TopDownEngine;
using Unity.Netcode;
using UnityEngine;

namespace LostMemory.Networking.Player
{
    /// <summary>
    /// 플레이어 측 damage 의 server-authoritative relay.
    ///
    /// 흐름:
    ///   - 모든 클라가 자기 hitbox 자체 검사 (KhiMeleeHitbox.Sample 등)
    ///   - hit 발생 시 본 컴포넌트의 RelayDamage 호출
    ///   - server (호스트): 직접 target Health.Damage 호출
    ///   - 비-server (게스트): ServerRpc 로 host 에 알림 → host 가 Damage 적용
    ///
    /// 부착: player prefab 의 root (NetworkObject 있는 자리).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Networking/Player Damage Relay")]
    public sealed class PlayerDamageRelay : NetworkBehaviour
    {
        [Tooltip("Phase D 진단: guest 공격 sync 추적용. 안정화 후 false 권장.")]
        [SerializeField] private bool verboseLog = false;

        /// <summary>
        /// KhiMeleeHitbox / KhiArrowProjectile / KhiMeteor 등 player 측 damage source 가 호출.
        /// 솔로 (NGO 미스폰) 환경: 그냥 직접 호출.
        /// 멀티 host: 직접 호출.
        /// 멀티 비-host: ServerRpc 로 host 에 위임.
        /// </summary>
        public void RelayDamage(Health target, float damage, GameObject attacker, float flickerDuration, float invincibilityDuration, Vector2 damageDirection)
        {
            if (target == null) return;

            // PvP 미상정 — 모든 player(자기/팀원 무관) 차단. 4인까지 자동 확장.
            if (CombatTargetable.IsFriendlyPlayer(target))
            {
                if (verboseLog) Debug.Log($"[PlayerDamageRelay] FriendlyFire blocked at RelayDamage — target={target.gameObject.name}", this);
                return;
            }

            if (!IsSpawned)
            {
                // 솔로 / NGO 미스폰
                if (verboseLog) Debug.Log($"[PlayerDamageRelay] Solo (IsSpawned=false) — direct Damage {damage} -> {target.gameObject.name}", this);
                target.Damage(damage, attacker, flickerDuration, invincibilityDuration, damageDirection);
                return;
            }

            if (IsServer)
            {
                if (verboseLog) Debug.Log($"[PlayerDamageRelay] Host (IsServer) — direct Damage {damage} -> {target.gameObject.name}", this);
                target.Damage(damage, attacker, flickerDuration, invincibilityDuration, damageDirection);
                return;
            }

            // 비-server: target 의 NetworkObjectId 로 ServerRpc 위임
            NetworkObject targetNo = target.GetComponentInParent<NetworkObject>();
            if (targetNo == null)
            {
                if (verboseLog) Debug.LogWarning($"[PlayerDamageRelay] target 에 NetworkObject 없음 — 게스트 측 damage 무시: {target.gameObject.name}", this);
                return;
            }

            if (verboseLog) Debug.Log($"[PlayerDamageRelay] Guest -> ServerRpc Damage {damage} -> {target.gameObject.name} (NoId={targetNo.NetworkObjectId})", this);
            RequestDamageServerRpc(
                targetNo.NetworkObjectId,
                damage,
                flickerDuration,
                invincibilityDuration,
                damageDirection);
        }

        [ServerRpc]
        private void RequestDamageServerRpc(
            ulong targetNetworkObjectId,
            float damage,
            float flickerDuration,
            float invincibilityDuration,
            Vector2 damageDirection)
        {
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetNo) || targetNo == null)
            {
                if (verboseLog) Debug.LogWarning($"[PlayerDamageRelay] ServerRpc — targetNetworkObjectId={targetNetworkObjectId} 의 NetworkObject 찾을 수 없음.", this);
                return;
            }

            Health targetHealth = targetNo.GetComponentInChildren<Health>();
            if (targetHealth == null)
            {
                if (verboseLog) Debug.LogWarning($"[PlayerDamageRelay] ServerRpc — target NetworkObject 에 Health 없음: {targetNo.name}", this);
                return;
            }

            // PvP 미상정 — 서버 측에서도 다시 검증 (게스트 보낸 ServerRpc 가 우회 가능성 차단).
            if (CombatTargetable.IsFriendlyPlayer(targetHealth))
            {
                if (verboseLog) Debug.LogWarning($"[PlayerDamageRelay] FriendlyFire blocked at ServerRpc — target={targetNo.name} requester={gameObject.name}", this);
                return;
            }

            // server (host) 측에서 직접 Damage 호출. attacker 는 본 NetworkBehaviour 의 GameObject (= 요청한 게스트의 player).
            if (verboseLog) Debug.Log($"[PlayerDamageRelay] ServerRpc received — Damage {damage} -> {targetNo.name} (NoId={targetNetworkObjectId})", this);
            targetHealth.Damage(damage, gameObject, flickerDuration, invincibilityDuration, damageDirection);
        }
    }
}
