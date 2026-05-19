using LostMemory.VFX;
using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// 패링 창/성공/실패 전용 시각·청각 피드백.
    /// 성공은 링 펄스 + 강화 플래시 + 스파크 프리팹 + SFX 로 확장 연출.
    /// 실패는 기존 단순 플래시 유지.
    /// 히트스톱 중에도 진행되도록 unscaled time 사용.
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Parry Feedback Presenter")]
    public class KhiParryFeedbackPresenter : MonoBehaviour
    {
        [SerializeField] private KhiParryController controller;
        [SerializeField] private LineRenderer ring;
        [SerializeField] private SpriteRenderer flashSprite;

        [Header("Ring")]
        [SerializeField, Min(8)] private int ringSegments = 48;
        [SerializeField, Min(0.05f)] private float ringRadius = 0.7f;
        [SerializeField, Min(0.01f)] private float ringWidth = 0.06f;

        [Header("Colors")]
        [SerializeField] private Color windowColor = new Color(0.3f, 0.9f, 1f, 1f);
        [SerializeField] private Color successColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color failureColor = new Color(1f, 0.3f, 0.2f, 1f);

        [Header("Flash")]
        [SerializeField, Min(0f)] private float flashDuration = 0.15f;

        [Header("Success - Flash")]
        [SerializeField, Min(0f)] private float successFlashDuration = 0.22f;
        [SerializeField, Min(0f)] private float successFlashIntensity = 1.2f;

        [Header("Success - Ring Pulse")]
        [SerializeField, Min(1f)] private float successRingExpansion = 2.0f;
        [SerializeField, Min(0f)] private float successRingDuration = 0.25f;
        [SerializeField] private Color successRingColor = new Color(0.8f, 0.95f, 1f, 1f);

        [Header("Success - Spark VFX (optional)")]
        [SerializeField, Tooltip("패링 성공 시 player 에 attach 되어 spawn 될 prefab. ParticleSystem/SpriteRenderer 포함. 빈 칸이면 spawn 안 함.")]
        private GameObject successSparkPrefab;
        [SerializeField, Min(0f), Tooltip("자동 destroy 시간 (초). 0 이면 prefab 자체가 처리 / VFXSpawner 미destroy.")]
        private float sparkLifetime = 0.4f;
        [SerializeField, Tooltip("attach 시 local offset. 0,0,0 이면 player 정중앙.")]
        private Vector3 sparkLocalOffset = Vector3.zero;

        [Header("Success - SFX (optional)")]
        [SerializeField] private AudioClip successSfx;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

        private float _flashExpiresAt = -1f;
        private float _flashTotalDuration;
        private Color _flashColor = Color.white;
        private float _successPulseStartedAt = -1f;

        private void Awake()
        {
            controller ??= GetComponentInParent<KhiParryController>();
            EnsureRing();
            if (ring != null)
            {
                ring.enabled = false;
            }
            if (flashSprite != null)
            {
                SetSpriteAlpha(flashSprite, 0f);
            }
            // SFX 는 AudioSource.PlayClipAtPoint 로 처리 — 별도 AudioSource 불필요.
        }

        private void OnEnable()
        {
            if (controller == null)
            {
                return;
            }

            controller.ParryStarted += HandleParryStarted;
            controller.ParrySucceeded += HandleParrySucceeded;
            controller.ParryFailed += HandleParryFailed;
            controller.ParryEnded += HandleParryEnded;
        }

        private void OnDisable()
        {
            if (controller == null)
            {
                return;
            }

            controller.ParryStarted -= HandleParryStarted;
            controller.ParrySucceeded -= HandleParrySucceeded;
            controller.ParryFailed -= HandleParryFailed;
            controller.ParryEnded -= HandleParryEnded;
        }

        private void Update()
        {
            UpdateRing();
            UpdateFlash();
        }

        private void UpdateRing()
        {
            if (ring == null || controller == null)
            {
                return;
            }

            // 성공 펄스가 진행 중이면 윈도우 링 로직보다 우선.
            if (_successPulseStartedAt >= 0f)
            {
                float total = Mathf.Max(successRingDuration, 0.0001f);
                float elapsed = Time.unscaledTime - _successPulseStartedAt;
                if (elapsed < total)
                {
                    float t = elapsed / total;
                    float radius = Mathf.Lerp(ringRadius, ringRadius * successRingExpansion, t);
                    BuildRingGeometryAtRadius(ring, radius);
                    Color c = successRingColor;
                    c.a *= 1f - t;
                    ring.startColor = c;
                    ring.endColor = c;
                    ring.enabled = true;
                    return;
                }

                _successPulseStartedAt = -1f;
                BuildRingGeometry(ring); // 기본 반경 복원
            }

            if (controller.CurrentState != KhiParryState.ParryWindow)
            {
                if (ring.enabled)
                {
                    ring.enabled = false;
                }
                return;
            }

            ring.enabled = true;
            float remaining = controller.CurrentStateRemaining;
            // remaining이 클수록 알파 1에 가깝게 유지. 끝에 갈수록 희미해짐.
            float windowAlpha = Mathf.Clamp01(remaining / 0.16f);
            Color wc = windowColor;
            wc.a *= windowAlpha;
            ring.startColor = wc;
            ring.endColor = wc;
        }

        private void UpdateFlash()
        {
            if (flashSprite == null)
            {
                return;
            }

            if (_flashExpiresAt < 0f)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now >= _flashExpiresAt)
            {
                SetSpriteAlpha(flashSprite, 0f);
                _flashExpiresAt = -1f;
                return;
            }

            float total = _flashTotalDuration <= 0f ? 0.0001f : _flashTotalDuration;
            float t = Mathf.Clamp01((_flashExpiresAt - now) / total);
            Color c = _flashColor;
            c.a *= t;
            flashSprite.color = c;
        }

        private void HandleParryStarted()
        {
            if (ring == null)
            {
                return;
            }

            ring.enabled = true;
            ring.startColor = windowColor;
            ring.endColor = windowColor;
        }

        private void HandleParrySucceeded()
        {
            BeginFlash(successColor, successFlashDuration, successFlashIntensity);
            StartSuccessPulse();
            SpawnSuccessSpark();
            PlaySuccessSfx();
        }

        private void HandleParryFailed()
        {
            BeginFlash(failureColor, flashDuration, 1f);
        }

        private void HandleParryEnded()
        {
            if (ring != null && _successPulseStartedAt < 0f)
            {
                ring.enabled = false;
            }
        }

        private void BeginFlash(Color color, float duration, float intensity)
        {
            if (flashSprite == null)
            {
                return;
            }

            Color applied = color;
            applied.a = Mathf.Clamp01(color.a * Mathf.Max(0f, intensity));
            _flashColor = applied;
            _flashTotalDuration = Mathf.Max(duration, 0f);
            _flashExpiresAt = Time.unscaledTime + _flashTotalDuration;
            flashSprite.color = applied;
        }

        private void StartSuccessPulse()
        {
            if (ring == null || successRingDuration <= 0f)
            {
                return;
            }

            _successPulseStartedAt = Time.unscaledTime;
            ring.enabled = true;
        }

        private void SpawnSuccessSpark()
        {
            if (successSparkPrefab == null)
            {
                return;
            }

            // 프로젝트 표준 VFX 진입점. 풀링 도입 시에도 호출측 변경 X.
            // SpawnAttached: player 의 transform 자식으로 부착 — 이동 따라감.
            VFXSpawner.SpawnAttached(successSparkPrefab, transform, sparkLocalOffset, sparkLifetime);
        }

        private void PlaySuccessSfx()
        {
            if (successSfx == null)
            {
                return;
            }

            // PlayClipAtPoint: AudioSource 의존성 제거 + 짧은 effect 라 매번 spawn OK.
            // 일관성: RelicEffectRegistry 등 다른 SFX 트리거와 동일 패턴.
            // per-clip balance multiplier (SfxBalanceWindow 로 조정) 적용. 증폭 (gain > 1) 허용.
            float perClipGain = LostMemory.Audio.SfxClipVolumeBalance.GetGain(successSfx);
            float finalVol = Mathf.Clamp(sfxVolume * perClipGain, 0f, LostMemory.Audio.SfxClipVolumeBalance.MaxGain);
            AudioSource.PlayClipAtPoint(successSfx, transform.position, finalVol);
        }

        private void EnsureRing()
        {
            if (ring != null)
            {
                BuildRingGeometry(ring);
                return;
            }

            GameObject go = new GameObject("KhiParryRing");
            go.transform.SetParent(transform, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = false;
            lr.widthMultiplier = ringWidth;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = windowColor;
            lr.endColor = windowColor;
            ring = lr;
            BuildRingGeometry(ring);
        }

        private void BuildRingGeometry(LineRenderer lr)
        {
            BuildRingGeometryAtRadius(lr, ringRadius);
        }

        private void BuildRingGeometryAtRadius(LineRenderer lr, float radius)
        {
            int points = Mathf.Max(8, ringSegments) + 1;
            if (lr.positionCount != points)
            {
                lr.positionCount = points;
            }
            for (int i = 0; i < points; i++)
            {
                float angle = (i / (float)(points - 1)) * Mathf.PI * 2f;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                lr.SetPosition(i, pos);
            }
        }

        private static void SetSpriteAlpha(SpriteRenderer sr, float alpha)
        {
            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }
}
