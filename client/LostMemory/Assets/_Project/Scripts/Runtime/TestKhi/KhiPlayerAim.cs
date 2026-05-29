using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// 플레이어 aim 방향 산출 + owner→other 동기화.
    /// owner: 마우스 위치 기반 방향 계산 + <see cref="_syncedDirection"/> NetworkVariable 에 write.
    /// non-owner: <see cref="_syncedDirection"/> 의 sync 된 값 반환 — 다른 클라이언트 측에서도
    /// 해당 플레이어가 같은 방향 바라보도록 한다 (Bug #22).
    /// </summary>
    public class KhiPlayerAim : NetworkBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Vector2 fallbackDirection = Vector2.right;
        [SerializeField] private KhiDownController downController;

        private Vector2 _lastDirection = Vector2.right;

        // owner write, everyone read — non-owner 측에서 GetAimDirection() 호출 시 이 값 반환
        private readonly NetworkVariable<Vector2> _syncedDirection = new(
            Vector2.right,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        public Vector2 CurrentDirection => (IsSpawned && !IsOwner) ? _syncedDirection.Value : _lastDirection;

        private void Awake()
        {
            if (downController == null)
            {
                KhiPlayerActionGate.TryResolveDownController(this, out downController);
            }
        }

        public Vector2 GetAimDirection()
        {
            // non-owner: NGO 가 sync 한 owner 의 aim 방향 반환
            if (IsSpawned && !IsOwner)
            {
                Vector2 synced = _syncedDirection.Value;
                if (synced.sqrMagnitude > Mathf.Epsilon)
                {
                    _lastDirection = synced;
                    return synced;
                }
                return GetCurrentOrFallbackDirection();
            }

            // owner / 미스폰 (싱글 플레이) — 기존 마우스 기반 계산
            if (KhiPlayerActionGate.IsBlocked(downController))
            {
                return GetCurrentOrFallbackDirection();
            }

            Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
            Mouse mouse = Mouse.current;
            if (cameraToUse == null || mouse == null)
            {
                return GetCurrentOrFallbackDirection();
            }

            Vector2 screenPosition = mouse.position.ReadValue();
            Vector3 worldPosition = cameraToUse.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -cameraToUse.transform.position.z));
            Vector2 direction = (Vector2)(worldPosition - transform.position);
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return GetCurrentOrFallbackDirection();
            }

            _lastDirection = direction.normalized;

            // owner — NetworkVariable 에 write. NGO 가 dirty 감지해 다른 클라에 자동 broadcast.
            if (IsSpawned && IsOwner)
            {
                _syncedDirection.Value = _lastDirection;

                // [DiagAim-Write] 1초 throttle 로 owner 측 write 가시화. non-owner 측 [DiagAim-Read] 와 매칭 비교용.
                // 진단 종료 — _diagAimLogEnabled = true 로 바꾸면 다시 활성화.
                if (_diagAimLogEnabled && Time.unscaledTime >= _diagNextAimWriteAt)
                {
                    _diagNextAimWriteAt = Time.unscaledTime + 1f;
                    Debug.Log($"[DiagAim-Write] dir={_lastDirection} angle={Mathf.Atan2(_lastDirection.y, _lastDirection.x) * Mathf.Rad2Deg:F1} IsOwner=True IsSpawned={IsSpawned} OwnerClientId={OwnerClientId}", this);
                }
            }

            return _lastDirection;
        }

        // [DiagAim-Write] throttle 상태.
        private float _diagNextAimWriteAt;
        // 진단 토글 — 재진단 필요 시 true. 평시 false (콘솔 노이즈 차단).
        private const bool _diagAimLogEnabled = false;

        private Vector2 GetCurrentOrFallbackDirection()
        {
            return _lastDirection.sqrMagnitude > Mathf.Epsilon ? _lastDirection : fallbackDirection.normalized;
        }
    }
}
