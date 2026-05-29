using Unity.Netcode;
using UnityEngine;

namespace LostMemory.MagicalGirl
{
    /// <summary>
    /// 마법소녀 (Magical Girl) visual 을 *모든 클라이언트에* broadcast 하는 컴포넌트.
    /// 단검/스태프/활/몬스터 telegraph 와 같은 host-relay-ClientRpc visual broadcast 패턴.
    ///
    /// 멀티 모델:
    ///   - 마법소녀는 *각 플레이어 본인* 의 inventory 에 들어온 유물로 spawn 됨 → 플레이어별 set 보유
    ///   - 본 컴포넌트는 player NetworkObject root 에 부착 (MagicalGirlSpawner 와 같은 GameObject)
    ///   - owner(자기 플레이어 클라) 가 spawn 한 girl 을 다른 클라가 *visual-only clone* 으로 재현
    ///   - AI/데미지 권위는 owner 측 인스턴스 단독 — 다른 클라의 clone 은 sprite + follower 만
    ///
    /// 사용법 (MagicalGirlSpawner 내부):
    ///   1. host-only 가드 제거 (또는 owner-aware 로 교체) → 각 owner 가 자기 client 에 본체 spawn
    ///   2. AddGirlByVisual 끝에 <see cref="NotifyLocalGirlSpawned"/> 호출
    ///   3. ClientRpc 가 non-owner 클라에서 <see cref="MagicalGirlSpawner.SpawnVisualOnlyClone"/> 호출
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MagicalGirlBroadcast : NetworkBehaviour
    {
        [Tooltip("같은 player 의 MagicalGirlSpawner. 비워두면 자동 resolve.")]
        [SerializeField] private MagicalGirlSpawner spawner;
        [Tooltip("진단 로그 출력. 안정화 후 false 권장.")]
        [SerializeField] private bool verboseLog = false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            ResolveSpawner();
        }

        private void ResolveSpawner()
        {
            if (spawner != null) return;
            spawner = GetComponent<MagicalGirlSpawner>();
            if (spawner == null) spawner = GetComponentInChildren<MagicalGirlSpawner>(true);
        }

        /// <summary>
        /// 로컬에서 미소녀를 spawn 한 직후 *owner 클라 측에서만* 호출.
        /// 다른 클라들에 ClientRpc 로 broadcast → 그들이 visual-only clone Instantiate.
        /// </summary>
        public void NotifyLocalGirlSpawned(MagicalGirlVisual visual)
        {
            if (!IsSpawned || !IsOwner)
            {
                // 솔로 (NM 비활성) / non-owner 호출 → broadcast 의미 없음, 자기 클라만 visual 보임.
                return;
            }
            if (verboseLog) Debug.Log($"[MagicalGirl-Broadcast] NotifyLocal visual={visual} OwnerClientId={OwnerClientId} → ServerRpc", this);
            NotifyLocalGirlSpawnedServerRpc((int)visual);
        }

        [ServerRpc]
        private void NotifyLocalGirlSpawnedServerRpc(int visualEnumValue)
        {
            BroadcastGirlSpawnClientRpc(visualEnumValue);
        }

        [ClientRpc]
        private void BroadcastGirlSpawnClientRpc(int visualEnumValue)
        {
            MagicalGirlVisual visual = (MagicalGirlVisual)visualEnumValue;
            if (verboseLog) Debug.Log($"[MagicalGirl-Broadcast] ClientRpc visual={visual} IsOwner={IsOwner} (skip if owner)", this);
            // owner 는 이미 로컬 spawn 함 → 중복 방지.
            if (IsOwner) return;

            ResolveSpawner();
            if (spawner == null)
            {
                if (verboseLog) Debug.LogWarning($"[MagicalGirl-Broadcast] ClientRpc — spawner null 로 clone skip. visual={visual}", this);
                return;
            }
            spawner.SpawnVisualOnlyClone(visual);
        }

        // ── Projectile sync (2026-05-28) — KhiArrowProjectile + AttackBroadcast 패턴 동일 복제 ──

        /// <summary>
        /// owner 측 MagicalGirlAI.SpawnProjectile 직후 호출. ServerRpc → ClientRpc → non-owner 측이 visual-only clone Instantiate.
        /// damage 권위는 owner 측 단독, non-owner clone 은 SetVisualOnly(true) 로 시각만 재현.
        /// projectileId: MagicalGirlProjectile.AllocateProjectileId() 로 owner 발급. owner hit broadcast 시 매칭 key.
        /// </summary>
        public void RelayMagicalGirlProjectileSpawn(int visualEnumValue, Vector3 spawnPos, Vector2 direction, float speed, float lifetime, int projectileId)
        {
            if (!IsSpawned || !IsOwner) return;
            if (verboseLog) Debug.Log($"[MagicalGirl-Broadcast] NotifyProjectile visual={(MagicalGirlVisual)visualEnumValue} pos={spawnPos} dir={direction} id={projectileId} → ServerRpc", this);
            RelayMagicalGirlProjectileSpawnServerRpc(visualEnumValue, spawnPos, direction, speed, lifetime, projectileId);
        }

        [ServerRpc]
        private void RelayMagicalGirlProjectileSpawnServerRpc(int visualEnumValue, Vector3 spawnPos, Vector2 direction, float speed, float lifetime, int projectileId)
        {
            BroadcastMagicalGirlProjectileSpawnClientRpc(visualEnumValue, spawnPos, direction, speed, lifetime, projectileId);
        }

        [ClientRpc]
        private void BroadcastMagicalGirlProjectileSpawnClientRpc(int visualEnumValue, Vector3 spawnPos, Vector2 direction, float speed, float lifetime, int projectileId)
        {
            if (verboseLog) Debug.Log($"[MagicalGirl-Broadcast] ProjectileSpawn ClientRpc visual={(MagicalGirlVisual)visualEnumValue} pos={spawnPos} id={projectileId} IsOwner={IsOwner} (skip if owner)", this);
            if (IsOwner) return;

            ResolveSpawner();
            if (spawner == null)
            {
                if (verboseLog) Debug.LogWarning($"[MagicalGirl-Broadcast] ProjectileSpawn ClientRpc — spawner null 로 skip. id={projectileId}", this);
                return;
            }
            spawner.SpawnVisualOnlyProjectile((MagicalGirlVisual)visualEnumValue, spawnPos, direction, speed, lifetime, projectileId);
        }

        /// <summary>owner 측 projectile 적중 시 호출. 모든 client (owner 포함) 의 매칭 clone destroy + hit VFX 표시.</summary>
        public void RelayMagicalGirlProjectileHit(int projectileId)
        {
            if (!IsSpawned || !IsOwner) return;
            if (projectileId <= 0) return;
            RelayMagicalGirlProjectileHitServerRpc(projectileId);
        }

        [ServerRpc]
        private void RelayMagicalGirlProjectileHitServerRpc(int projectileId)
        {
            BroadcastMagicalGirlProjectileHitClientRpc(projectileId);
        }

        [ClientRpc]
        private void BroadcastMagicalGirlProjectileHitClientRpc(int projectileId)
        {
            if (verboseLog) Debug.Log($"[MagicalGirl-Broadcast] ProjectileHit ClientRpc id={projectileId} IsOwner={IsOwner} (skip if owner)", this);
            if (IsOwner) return;  // owner 는 자기 hit 시점에 이미 local destroy
            MagicalGirlProjectile.DespawnVisualOnlyCloneById(projectileId);
        }
    }
}
