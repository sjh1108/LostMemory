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
        [SerializeField] private bool verboseLog = true;

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
    }
}
