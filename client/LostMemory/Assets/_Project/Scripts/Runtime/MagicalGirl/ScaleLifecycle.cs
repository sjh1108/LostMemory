using UnityEngine;

namespace LostMemory.MagicalGirl
{
    /// <summary>
    /// CL-204: 단일 사이클 grow → hold → shrink 스케일 애니메이션.
    /// 일회성 효과의 *생성 → 유지 → 소멸* 라이프사이클 표현.
    /// PulseScale (지속 박동) 과 다름 — 한 번만 진행 후 endScale 고정.
    ///
    /// 사용:
    /// - AOE / 효과 prefab 의 시각 자식 (예: Circle, Spiral) 에 부착
    /// - 총 lifetime = introDuration + holdDuration + outroDuration
    /// - AOE Duration 과 일치시켜야 자연스러움 (예: 4s = 0.3 + 3.0 + 0.7)
    ///
    /// CL-204 B9 확장:
    /// - Hold Pulse: hold phase 동안 미세한 sin 펄스 (살아있는 느낌)
    /// - Intro Style: Smooth (균일 grow) 또는 CRT (옛 TV 켜지는 — X 먼저 가로선, Y 나중 펼침)
    /// - Outro Style: Smooth (균일 shrink) 또는 CRT (옛 TV 꺼지는 — Y 먼저 짜부, X 나중)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScaleLifecycle : MonoBehaviour
    {
        public enum IntroStyle
        {
            Smooth, // 균일 uniform grow (startScale → peakScale)
            CRT,    // 옛 TV 켜지는 — 전반 X 펼침 (0 → peak, Y crtSquishY 유지), 후반 Y 펼침 (crtSquishY → peak)
        }

        public enum OutroStyle
        {
            Smooth, // 균일 uniform shrink (peakScale → endScale)
            CRT,    // 옛 TV 짜부 — 전반 Y 짜부 (peak → crtSquishY), 후반 X 짜부 (peak → 0)
        }

        [Tooltip("성장 시간 (초). startScale → peakScale.")]
        [SerializeField, Min(0f)] private float introDuration = 0.3f;

        [Tooltip("유지 시간 (초). peakScale 유지.")]
        [SerializeField, Min(0f)] private float holdDuration = 3f;

        [Tooltip("축소 시간 (초). peakScale → endScale.")]
        [SerializeField, Min(0f)] private float outroDuration = 0.7f;

        [Tooltip("시작 스케일 (multiplier). baseScale × startScale 으로 시작.")]
        [SerializeField, Min(0f)] private float startScale = 0.2f;

        [Tooltip("최대 스케일 (multiplier).")]
        [SerializeField, Min(0f)] private float peakScale = 1f;

        [Tooltip("종료 스케일 (multiplier). 0 = 완전 소멸.")]
        [SerializeField, Min(0f)] private float endScale = 0f;

        [Tooltip("ON 시 SmoothStep 보간 — 부드럽게 가속/감속. OFF 시 Linear.")]
        [SerializeField] private bool smoothEase = true;

        [Header("Hold Pulse (선택)")]
        [Tooltip("Hold phase 중 미세 sin 펄스 적용. ON 시 정적이지 않고 살아있는 느낌.")]
        [SerializeField] private bool holdPulseEnabled = true;

        [Tooltip("Hold pulse 진폭. 0.05 = ±5% 변화 (subtle).")]
        [SerializeField, Range(0f, 0.5f)] private float holdPulseAmount = 0.05f;

        [Tooltip("Hold pulse 속도 (사이클/초). 0.8 = 1.25초당 1번 호흡.")]
        [SerializeField, Min(0.01f)] private float holdPulseSpeed = 0.8f;

        [Header("Intro Style")]
        [Tooltip("시작 phase 스타일. Smooth = 기본 균일 grow. CRT = 옛 TV 켜지는 효과.")]
        [SerializeField] private IntroStyle introStyle = IntroStyle.Smooth;

        [Header("Outro Style")]
        [Tooltip("종료 phase 의 짜부 스타일. Smooth = 기본 균일 shrink. CRT = 옛 TV 꺼지는 효과.")]
        [SerializeField] private OutroStyle outroStyle = OutroStyle.Smooth;

        [Tooltip("CRT 모드 시 Y 짜부 최저점 (multiplier). 0.05 = baseScale.y × 5%. 너무 0 에 가까우면 사라지듯 보임. Intro/Outro 공유.")]
        [SerializeField, Range(0f, 0.5f)] private float crtSquishY = 0.05f;

        private Vector3 _baseScale;
        private float _t;

        private void Awake() { _baseScale = transform.localScale; }

        private void OnEnable() { _t = 0f; }

        private void Update()
        {
            _t += Time.deltaTime;

            // 모든 phase 의 결과를 비균일 multiplier (Vector3) 로 계산
            // 균일 phase 는 (k,k,k), CRT outro 는 (xMul, yMul, 1)
            Vector3 scaleMul = Vector3.one * peakScale;

            if (_t < introDuration)
            {
                // Intro: 스타일에 따라 분기
                float introP = _t / Mathf.Max(0.001f, introDuration);
                if (introStyle == IntroStyle.CRT)
                {
                    // CRT: 전반 X 가로선 펼침 (0 → peak, Y squish 유지), 후반 Y 펼침 (squish → peak)
                    if (introP < 0.5f)
                    {
                        float p = introP / 0.5f;
                        if (smoothEase) p = Mathf.SmoothStep(0f, 1f, p);
                        scaleMul.x = Mathf.LerpUnclamped(0f, peakScale, p);
                        scaleMul.y = crtSquishY;
                    }
                    else
                    {
                        float p = (introP - 0.5f) / 0.5f;
                        if (smoothEase) p = Mathf.SmoothStep(0f, 1f, p);
                        scaleMul.x = peakScale;
                        scaleMul.y = Mathf.LerpUnclamped(crtSquishY, peakScale, p);
                    }
                }
                else // Smooth
                {
                    float p = introP;
                    if (smoothEase) p = Mathf.SmoothStep(0f, 1f, p);
                    float k = Mathf.LerpUnclamped(startScale, peakScale, p);
                    scaleMul = Vector3.one * k;
                }
            }
            else if (_t < introDuration + holdDuration)
            {
                // Hold: peakScale 유지 + 선택적 미세 펄스
                float k = peakScale;
                if (holdPulseEnabled && holdPulseAmount > 0f)
                {
                    float pt = _t - introDuration;
                    k *= 1f + Mathf.Sin(pt * holdPulseSpeed * Mathf.PI * 2f) * holdPulseAmount;
                }
                scaleMul = Vector3.one * k;
            }
            else if (_t < introDuration + holdDuration + outroDuration)
            {
                // Outro: 스타일에 따라 분기
                float outroP = (_t - introDuration - holdDuration) / Mathf.Max(0.001f, outroDuration);
                if (outroStyle == OutroStyle.CRT)
                {
                    // CRT: 전반 Y 짜부, 후반 X 짜부
                    if (outroP < 0.5f)
                    {
                        float p = outroP / 0.5f;
                        if (smoothEase) p = Mathf.SmoothStep(0f, 1f, p);
                        scaleMul.x = peakScale;
                        scaleMul.y = Mathf.LerpUnclamped(peakScale, crtSquishY, p);
                    }
                    else
                    {
                        float p = (outroP - 0.5f) / 0.5f;
                        if (smoothEase) p = Mathf.SmoothStep(0f, 1f, p);
                        scaleMul.x = Mathf.LerpUnclamped(peakScale, 0f, p);
                        scaleMul.y = crtSquishY;
                    }
                }
                else // Smooth
                {
                    float p = outroP;
                    if (smoothEase) p = Mathf.SmoothStep(0f, 1f, p);
                    float k = Mathf.LerpUnclamped(peakScale, endScale, p);
                    scaleMul = Vector3.one * k;
                }
            }
            else
            {
                // 만료: endScale 고정 (CRT 의 경우 0)
                scaleMul = Vector3.one * endScale;
            }

            transform.localScale = Vector3.Scale(_baseScale, scaleMul);
        }
    }
}
