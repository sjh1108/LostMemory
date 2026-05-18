using UnityEngine;

namespace LostMemory.Stage
{
    // 막힌 출구 등에 붙이는 SpriteRenderer 알파 펄스.
    // RoomExitWall 자식으로 빈 GameObject + SpriteRenderer(흰 사각 sprite, 빨간 Color) 두고
    // 이 컴포넌트를 붙이면, 부모가 SetActive(true) 될 때마다 알파가 sin 으로 호흡.
    // RenderExitWall 의 식별 책임과 분리되어 다른 차단 표시에도 재사용 가능.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [AddComponentMenu("Lost Memory/Stage/Blocked Pulse")]
    public sealed class BlockedPulse : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float alphaMin = 0.35f;
        [SerializeField, Range(0f, 1f)] private float alphaMax = 0.65f;
        [SerializeField, Min(0.01f)] private float periodSeconds = 1.2f;

        private SpriteRenderer _renderer;
        private float _elapsed;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            _elapsed = 0f;
            ApplyAlpha(alphaMin);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float phase = (_elapsed / periodSeconds) * Mathf.PI * 2f;
            float t = (Mathf.Sin(phase) + 1f) * 0.5f;
            ApplyAlpha(Mathf.Lerp(alphaMin, alphaMax, t));
        }

        private void ApplyAlpha(float a)
        {
            if (_renderer == null) return;
            Color c = _renderer.color;
            c.a = a;
            _renderer.color = c;
        }
    }
}
