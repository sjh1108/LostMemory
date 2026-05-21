using UnityEngine;

namespace LostMemory.Stage
{
    /// <summary>
    /// 방의 출구를 막는 벽.
    ///
    /// 멀티 안전화 (Phase 2):
    /// - GameObject.SetActive 토글 X. GameObject 자체는 항상 active 유지.
    /// - 상태는 부모 RoomEntryRuntimeController.IsLocked (NetworkVariable) 가 권위.
    /// - Awake 에서 parent controller 의 ExitLockedStateChanged 구독.
    /// - 변화 시 자기 트리의 모든 Collider2D.enabled + Renderer.enabled 동시 토글.
    /// - 초기 상태: false (안 보이고 안 막힘) → 진입 전 자유 통과 보장.
    ///
    /// Collider2D / Renderer 가 자식 GameObject 에 분산되어 있어도 모두 토글
    /// (GetComponentsInChildren). Inspector 에서 수동 할당하면 그것만 토글.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Stage/Room Exit Wall")]
    public sealed class RoomExitWall : MonoBehaviour
    {
        [SerializeField] private string exitTag = string.Empty;

        [Header("Toggle Targets (비할당이면 Awake 에서 자기 트리 전체 자동 탐색)")]
        [Tooltip("lock 상태에 따라 enabled 토글. 비어있으면 GetComponentsInChildren<Collider2D> 로 자동 탐색.")]
        [SerializeField] private Collider2D[] _blockColliders;
        [Tooltip("lock 상태에 따라 enabled 토글 (SpriteRenderer / TilemapRenderer 등 포함). 비어있으면 GetComponentsInChildren<Renderer> 로 자동 탐색.")]
        [SerializeField] private Renderer[] _renderers;

        private RoomEntryRuntimeController _controller;

        public string ExitTag => exitTag;

        public bool Matches(string candidateTag)
        {
            return !string.IsNullOrEmpty(exitTag) && exitTag == candidateTag;
        }

        private void Awake()
        {
            if (_blockColliders == null || _blockColliders.Length == 0)
            {
                _blockColliders = GetComponentsInChildren<Collider2D>(includeInactive: true);
            }
            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            }

            // 초기 상태: false (collider/renderer 둘 다 disabled). 진입 전 자유 통과 + 안 보임.
            ApplyLocked(false);

            _controller = GetComponentInParent<RoomEntryRuntimeController>(includeInactive: true);
            if (_controller != null)
            {
                _controller.ExitLockedStateChanged += ApplyLocked;
                // controller 가 이미 NetworkSpawn 됐을 수 있음 (late init 순서). 현재 값으로 즉시 1회 sync.
                if (_controller.IsSpawned)
                {
                    ApplyLocked(_controller.IsLocked.Value);
                }
            }
        }

        private void OnDestroy()
        {
            if (_controller != null)
            {
                _controller.ExitLockedStateChanged -= ApplyLocked;
                _controller = null;
            }
        }

        private void ApplyLocked(bool locked)
        {
            if (_blockColliders != null)
            {
                for (int i = 0; i < _blockColliders.Length; i++)
                {
                    if (_blockColliders[i] != null) _blockColliders[i].enabled = locked;
                }
            }
            if (_renderers != null)
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] != null) _renderers[i].enabled = locked;
                }
            }
        }
    }
}
