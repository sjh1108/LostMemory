using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// 패링 창/성공/실패를 임시로 보여주는 시각 피드백.
    /// 실제 아트 이펙트는 후속 CL에서 교체한다.
    /// LineRenderer가 없으면 런타임에 간단한 원형 링을 생성한다.
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

        private float _flashExpiresAt = -1f;
        private Color _flashColor = Color.white;

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
            float alpha = Mathf.Clamp01(remaining / 0.16f);
            Color c = windowColor;
            c.a *= alpha;
            ring.startColor = c;
            ring.endColor = c;
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

            if (Time.time >= _flashExpiresAt)
            {
                SetSpriteAlpha(flashSprite, 0f);
                _flashExpiresAt = -1f;
                return;
            }

            float total = flashDuration <= 0f ? 0.0001f : flashDuration;
            float t = Mathf.Clamp01((_flashExpiresAt - Time.time) / total);
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
            BeginFlash(successColor);
        }

        private void HandleParryFailed()
        {
            BeginFlash(failureColor);
        }

        private void HandleParryEnded()
        {
            if (ring != null)
            {
                ring.enabled = false;
            }
        }

        private void BeginFlash(Color color)
        {
            if (flashSprite == null)
            {
                return;
            }

            _flashColor = color;
            _flashExpiresAt = Time.time + flashDuration;
            flashSprite.color = color;
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
            int points = Mathf.Max(8, ringSegments) + 1;
            lr.positionCount = points;
            for (int i = 0; i < points; i++)
            {
                float angle = (i / (float)(points - 1)) * Mathf.PI * 2f;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * ringRadius, Mathf.Sin(angle) * ringRadius, 0f);
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
