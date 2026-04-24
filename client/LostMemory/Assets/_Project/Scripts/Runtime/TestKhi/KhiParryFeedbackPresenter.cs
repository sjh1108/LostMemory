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
        [SerializeField] private GameObject successSparkPrefab;
        [SerializeField, Min(0f)] private float sparkLifetime = 0.4f;

        [Header("Success - SFX (optional)")]
        [SerializeField] private AudioClip successSfx;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

        private float _flashExpiresAt = -1f;
        private float _flashTotalDuration;
        private Color _flashColor = Color.white;
        private float _successPulseStartedAt = -1f;
        private AudioSource _audioSource;

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
            EnsureAudioSource();
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

            GameObject spark = Instantiate(successSparkPrefab, transform.position, Quaternion.identity);
            if (sparkLifetime > 0f)
            {
                Destroy(spark, sparkLifetime);
            }
        }

        private void PlaySuccessSfx()
        {
            if (successSfx == null || _audioSource == null)
            {
                return;
            }

            _audioSource.PlayOneShot(successSfx, sfxVolume);
        }

        private void EnsureAudioSource()
        {
            if (_audioSource != null)
            {
                return;
            }

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 0f;
            }
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
