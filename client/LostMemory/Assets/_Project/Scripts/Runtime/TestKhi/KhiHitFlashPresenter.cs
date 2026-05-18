using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// CL-017 피격 플래시 프레젠터.
    /// MaterialPropertyBlock으로 SpriteRenderer._Color를 일시 오버라이드 → ease-out 페이드.
    /// 원본 머티리얼/컬러 손상 없음. 히트스톱 중에도 진행되도록 unscaled time 기준.
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Hit Flash Presenter")]
    [DefaultExecutionOrder(310)]
    public class KhiHitFlashPresenter : MonoBehaviour
    {
        [Header("Targets")]
        [Tooltip("비우면 Awake에서 GetComponentsInChildren<SpriteRenderer>()로 자동 탐색")]
        [SerializeField] private SpriteRenderer[] targetRenderers;

        [Header("Defaults (외부에서 Flash 호출 시 override 가능)")]
        [SerializeField] private Color defaultFlashColor = Color.white;
        [SerializeField, Min(0f)] private float defaultFlashDuration = 0.1f;

        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

        private MaterialPropertyBlock _mpb;
        private Color _currentColor = Color.white;
        private float _flashStartUnscaled;
        private float _flashDuration;
        private bool _isFlashing;

        private void Awake()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            }

            _mpb = new MaterialPropertyBlock();
        }

        /// <summary>
        /// 외부 호출 진입점. color/duration 중 default 원하면 그 오버로드.
        /// 이미 진행 중이면 새 요청이 덮어씀 (가장 최근 이벤트 우선).
        /// </summary>
        public void Flash(Color color, float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            _currentColor = color;
            _flashDuration = duration;
            _flashStartUnscaled = Time.unscaledTime;
            _isFlashing = true;
        }

        public void Flash()
        {
            Flash(defaultFlashColor, defaultFlashDuration);
        }

        private void Update()
        {
            if (!_isFlashing || targetRenderers == null || targetRenderers.Length == 0)
            {
                return;
            }

            float elapsed = Time.unscaledTime - _flashStartUnscaled;
            if (elapsed >= _flashDuration)
            {
                ClearFlash();
                return;
            }

            float intensity = 1f - (elapsed / _flashDuration);
            Color tint = Color.Lerp(Color.white, _currentColor, intensity);

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                SpriteRenderer r = targetRenderers[i];
                if (r == null)
                {
                    continue;
                }

                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(ColorPropertyId, tint);
                r.SetPropertyBlock(_mpb);
            }
        }

        private void ClearFlash()
        {
            _isFlashing = false;

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                SpriteRenderer r = targetRenderers[i];
                if (r == null)
                {
                    continue;
                }

                r.GetPropertyBlock(_mpb);
                _mpb.Clear();
                r.SetPropertyBlock(_mpb);
            }
        }

        private void OnDisable()
        {
            if (_isFlashing)
            {
                ClearFlash();
            }
        }
    }
}
