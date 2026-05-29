using Unity.Netcode;
using UnityEngine;

namespace LostMemory.Combat.Telegraph
{
    /// <summary>
    /// 몬스터 telegraph (빨간 장판) 시각을 *모든 클라이언트에* broadcast 하는 공통 컴포넌트.
    /// 단검/스태프/활 의 host-relay-ClientRpc visual broadcast 패턴과 동일.
    ///
    /// 사용법:
    ///   1. 몬스터 prefab root (NetworkObject 와 같은 GameObject) 에 부착.
    ///   2. 호스트 측 attack 로직에서 <see cref="BroadcastTelegraph"/> 호출 1줄.
    ///   3. 모든 클라 (호스트 포함) 가 ClientRpc 도착 시 visual prefab Instantiate.
    ///   4. 데미지 권위는 호출자 측 host-only 로컬 로직이 별도 처리 (visual 과 분리).
    ///
    /// 솔로/non-server 안전: <see cref="BroadcastTelegraph"/> 가 server 아닌 경우 즉시 return.
    /// 단, 솔로 (NetworkManager 비활성) 시에도 시각이 안 뜨면 안 되므로 fallback 으로 로컬 visual 재현.
    ///
    /// visual prefab 미할당 시: runtime 으로 <see cref="AttackTelegraph2DView"/> 컴포넌트 부착된 GameObject 자동 생성.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Telegraph/Monster Attack Broadcast")]
    public sealed class MonsterAttackBroadcast : NetworkBehaviour
    {
        [Header("Visual prefab (선택 — 비워두면 runtime 자동 생성)")]
        [Tooltip("게스트 화면에 보여줄 telegraph visual prefab. AttackTelegraph2DView 컴포넌트 필수. " +
                 "비워두면 코드가 새 GameObject + AttackTelegraph2DView 자동 생성 (sortingLayer 기본값 사용).")]
        [SerializeField] private GameObject telegraphVisualPrefab;

        [Header("Debug")]
        [Tooltip("진단 로그. 안정화 후 false 권장.")]
        [SerializeField] private bool verboseLog = false;

        /// <summary>
        /// 호스트(=server) 측에서 호출. ServerRpc 없이 직접 ClientRpc 발화 → 모든 클라가 visual 재현.
        /// 호스트도 ClientRpc 받아 같은 path 로 visual instantiate → host/guest 시각 일관성.
        /// </summary>
        public void BroadcastTelegraph(
            AttackTelegraphShape2D shape,
            Vector3 worldPosition,
            Vector2 size,
            float rotationDegrees,
            float warningDuration,
            float impactHoldDuration,
            Color color)
        {
            NetworkManager nm = NetworkManager.Singleton;
            bool networkActive = nm != null && nm.IsListening;

            if (!networkActive)
            {
                // Bug #37: 솔로 — enemy controller 의 AttackTelegraph2DView.Show 가 *원본 local visual* 처리.
                // 본 메서드의 local SpawnVisualClone fallback 호출 시 *중복 visual* (Show + clone) 발생 → skip.
                return;
            }

            if (!IsSpawned || !IsServer)
            {
                if (verboseLog) Debug.LogWarning($"[MonsterAttackBroadcast] BroadcastTelegraph ignored — IsSpawned={IsSpawned} IsServer={IsServer} mob={name}", this);
                return;
            }

            if (verboseLog) Debug.Log($"[DiagTelegraph-Spawn] mob={name} shape={shape} pos={worldPosition} size={size} warning={warningDuration:F2} impact={impactHoldDuration:F2} IsServer=True", this);

            BroadcastTelegraphClientRpc(shape, worldPosition, size, rotationDegrees, warningDuration, impactHoldDuration, color);
        }

        [ClientRpc]
        private void BroadcastTelegraphClientRpc(
            AttackTelegraphShape2D shape,
            Vector3 worldPosition,
            Vector2 size,
            float rotationDegrees,
            float warningDuration,
            float impactHoldDuration,
            Color color)
        {
            // Bug #31 진단 강화 — 게스트 측에 ClientRpc 가 도착하는지 + 어느 client 가 받는지 명시.
            // localId + netObjId 까지 dump → host/guest 양쪽 콘솔에서 같은 netObjId 의 mob 추적 가능.
            if (verboseLog)
            {
                var nm = Unity.Netcode.NetworkManager.Singleton;
                Debug.Log(
                    $"[DiagTelegraph-RpcRecv] mob={name} shape={shape} pos={worldPosition} size={size} " +
                    $"IsHost={IsHost} IsServer={IsServer} IsClient={IsClient} " +
                    $"localId={(nm != null ? nm.LocalClientId : 0)} netObjId={NetworkObjectId} 도착", this);
            }

            // Bug #37: host 측은 enemy controller 의 *원본 telegraphView.Show* 가 visual 이미 표시 중.
            //   ClientRpc 의 SpawnVisualClone 까지 또 작동하면 host 화면에 중복 sprite → skip.
            //   게스트 (비-host) 만 SpawnVisualClone 으로 별도 GameObject + AttackTelegraph2DView 생성.
            if (IsHost) return;

            SpawnVisualClone(shape, worldPosition, size, rotationDegrees, warningDuration, impactHoldDuration, color);
        }

        /// <summary>
        /// visual prefab Instantiate (또는 runtime GameObject 생성) + AttackTelegraph2DView.Show + 자동 destroy.
        /// 호스트/게스트 동일 path — telegraph 시각 일관성.
        /// </summary>
        private void SpawnVisualClone(
            AttackTelegraphShape2D shape,
            Vector3 worldPosition,
            Vector2 size,
            float rotationDegrees,
            float warningDuration,
            float impactHoldDuration,
            Color color)
        {
            Quaternion rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            GameObject visualGo;

            if (telegraphVisualPrefab != null)
            {
                visualGo = Instantiate(telegraphVisualPrefab, worldPosition, rotation);
            }
            else
            {
                // Fallback: runtime GameObject 자동 생성 + AttackTelegraph2DView 부착.
                // prefab 인스펙터 작업 없이도 동작 — 새 몬스터 추가 시 컴포넌트만 부착하면 시각 sync 즉시 가능.
                visualGo = new GameObject($"MonsterTelegraphClone_{shape}");
                visualGo.transform.SetPositionAndRotation(worldPosition, rotation);
                visualGo.AddComponent<AttackTelegraph2DView>();
            }

            AttackTelegraph2DView view = visualGo.GetComponent<AttackTelegraph2DView>();
            if (view == null) view = visualGo.GetComponentInChildren<AttackTelegraph2DView>(true);
            if (view == null)
            {
                if (verboseLog) Debug.LogWarning($"[MonsterAttackBroadcast] visual prefab '{telegraphVisualPrefab?.name}' lacks AttackTelegraph2DView. cleanup + skip.", this);
                Destroy(visualGo);
                return;
            }

            // Direction 은 rotationDegrees 기반 (Box shape 의 방향성 표시용 — Circle 은 무시).
            float rad = rotationDegrees * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            float effectiveWarning = Mathf.Max(0.01f, warningDuration);
            AttackTelegraphRequest2D request = new AttackTelegraphRequest2D
            {
                Shape = shape,
                Center = worldPosition,
                Direction = direction,
                Size = size,
                Color = color.a > 0f ? color : new Color(1f, 0.12f, 0.05f, 0.38f),
                Duration = effectiveWarning,
            };
            view.Show(request);

            // AttackTelegraph2DView 가 자체 Duration 만료 시 HideImmediate 호출 → SpriteRenderer 만 비활성.
            // 시각 + impact phase 모두 종료 후 GameObject 자체 destroy 로 leakage 방지.
            float totalLifetime = effectiveWarning + Mathf.Max(0f, impactHoldDuration);
            Destroy(visualGo, totalLifetime);
        }
    }
}
