using UnityEngine;

namespace LostMemory.UI.Minimap
{
    /// <summary>
    /// 방 prefab 의 trigger Collider2D (보통 RoomEntryZone) 와 같은 GameObject 에 부착.
    /// <para>
    /// 플레이어(PlayerLocal MinimapAgent) 가 trigger 영역에 진입하면 자기 Collider bounds 전체를
    /// MinimapFog 에 reveal 요청. 영구 reveal 이라 1회만 실행 (re-entry 시 재호출 없음).
    /// </para>
    /// <para>
    /// 통로가 방 collider 에 포함된 구조 가정 — 통로용 별도 trigger 불필요.
    /// 통로가 별도 prefab/GameObject 면 그쪽에도 본 컴포넌트 부착.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("Lost Memory/UI/Minimap/Minimap Room Reveal")]
    public sealed class MinimapRoomReveal : MonoBehaviour
    {
        [SerializeField, Tooltip("비어있으면 OnTriggerEnter 시점에 FindObjectOfType<MinimapFog> 로 자동 탐색 + 캐시.")]
        private MinimapFog fog;

        [SerializeField, Tooltip("이 컴포넌트가 사용할 Collider2D. 비어있으면 같은 GameObject 의 첫 Collider2D 자동 선택.")]
        private Collider2D boundsSource;

        [SerializeField, Tooltip("디버그 로그.")]
        private bool logOnReveal = false;

        private bool _revealed;
        private static MinimapFog _cachedFog;

        private void Awake()
        {
            if (boundsSource == null)
            {
                boundsSource = GetComponent<Collider2D>();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_revealed) return;

            // PlayerLocal MinimapAgent 가 부착된 GameObject 만 reveal 트리거
            MinimapAgent agent = other.GetComponentInParent<MinimapAgent>();
            if (agent == null || agent.Kind != MinimapAgent.AgentKind.PlayerLocal) return;

            MinimapFog targetFog = ResolveFog();
            if (targetFog == null || boundsSource == null) return;

            targetFog.RevealBounds(boundsSource.bounds);
            _revealed = true;

            if (logOnReveal)
            {
                Debug.Log($"[MinimapRoomReveal] {name} revealed bounds={boundsSource.bounds}", this);
            }
        }

        private MinimapFog ResolveFog()
        {
            if (fog != null) return fog;
            if (_cachedFog != null) return _cachedFog;

            _cachedFog = FindObjectOfType<MinimapFog>();
            return _cachedFog;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (boundsSource == null) boundsSource = GetComponent<Collider2D>();
            if (boundsSource == null) return;

            Gizmos.color = new Color(0.2f, 1f, 0.5f, 0.5f);
            Bounds b = boundsSource.bounds;
            Gizmos.DrawWireCube(b.center, b.size);
        }
#endif
    }
}
