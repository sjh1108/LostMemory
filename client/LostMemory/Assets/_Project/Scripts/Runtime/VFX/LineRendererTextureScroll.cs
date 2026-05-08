using UnityEngine;

namespace LostMemory.VFX
{
    /// <summary>
    /// CL-201: LineRenderer 머티리얼의 메인 텍스처 UV 오프셋을 매 프레임 갱신해
    /// 텍스처가 라인을 따라 흐르는 효과를 낸다 (번개/에너지 빔용).
    ///
    /// LineRenderer Texture Mode 가 Tile 또는 RepeatPerSegment 일 때 효과가 가장 큼.
    /// Awake 에서 .material 접근 → 머티리얼 인스턴스 생성. GameObject Destroy 시 함께 정리됨.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    [AddComponentMenu("Lost Memory/VFX/Line Renderer Texture Scroll")]
    public sealed class LineRendererTextureScroll : MonoBehaviour
    {
        [Tooltip("X(라인 진행) 방향 스크롤 속도. 음수 = 번개가 origin 쪽으로 흐르는 느낌.")]
        [SerializeField] private float _scrollSpeedX = -3f;

        [Tooltip("Y 방향 스크롤 속도. 일반적으론 0.")]
        [SerializeField] private float _scrollSpeedY = 0f;

        [Tooltip("켜면 unscaled time 사용 — hitstop 중에도 애니메이션 진행.")]
        [SerializeField] private bool _useUnscaledTime = false;

        private Material _mat;
        private Vector2 _offset;

        private void Awake()
        {
            // .material 은 인스턴스 사본 생성. GameObject Destroy 시 함께 GC.
            _mat = GetComponent<LineRenderer>().material;
        }

        private void Update()
        {
            if (_mat == null) return;

            float dt = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            _offset.x += _scrollSpeedX * dt;
            _offset.y += _scrollSpeedY * dt;
            _mat.mainTextureOffset = _offset;
        }
    }
}
