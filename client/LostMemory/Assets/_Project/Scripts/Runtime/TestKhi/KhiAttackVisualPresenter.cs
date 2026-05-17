using System.Collections;
using LostMemory.Data;
using Unity.Netcode.Components;
using UnityEngine;

namespace LostMemory.TestKhi
{
    public class KhiAttackVisualPresenter : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private NetworkAnimator networkAnimator;
        [SerializeField] private bool showTemporarySlash = true;
        [SerializeField] private int temporarySlashSortingOrder = 1001;
        [SerializeField] private Color firstSlashColor = new Color(1f, 1f, 1f, 0.78f);
        [SerializeField] private Color secondSlashColor = new Color(1f, 0.85f, 0.2f, 0.8f);
        [SerializeField] private Color thirdSlashColor = new Color(1f, 0.28f, 0.08f, 0.85f);
        [SerializeField] private float slashAreaScale = 0.95f;

        [Header("External Slash Sprites (optional — slot 비면 기존 procedural fallback)")]
        [SerializeField] private Sprite externalThinSlashSprite;
        [SerializeField] private Sprite externalWideSlashSprite;

        [Header("Trail (optional)")]
        [SerializeField] private bool useTrailRenderer = false;
        [SerializeField] private Transform trailAttachPoint;
        [SerializeField, Min(0f)] private float trailTime = 0.18f;
        [SerializeField, Min(0.001f)] private float trailWidth = 0.05f;
        [SerializeField] private Color trailStartColor = new Color(1f, 1f, 1f, 0.85f);
        [SerializeField] private Color trailEndColor = new Color(1f, 1f, 1f, 0f);

        private KhiMeleeComboController _comboController;
        private Sprite _thinSlashSprite;
        private Sprite _wideSlashSprite;
        private TrailRenderer _attachedTrail;

        private void Awake()
        {
            animator ??= GetComponentInChildren<Animator>();
            networkAnimator ??= GetComponentInChildren<NetworkAnimator>();
            _comboController = GetComponent<KhiMeleeComboController>();
            EnsureTrailRenderer();
        }

        private void EnsureTrailRenderer()
        {
            if (!useTrailRenderer || trailAttachPoint == null)
            {
                return;
            }

            _attachedTrail = trailAttachPoint.GetComponent<TrailRenderer>();
            if (_attachedTrail == null)
            {
                _attachedTrail = trailAttachPoint.gameObject.AddComponent<TrailRenderer>();
            }

            _attachedTrail.time = trailTime;
            _attachedTrail.startWidth = trailWidth;
            _attachedTrail.endWidth = 0f;
            _attachedTrail.startColor = trailStartColor;
            _attachedTrail.endColor = trailEndColor;
            _attachedTrail.minVertexDistance = 0.01f;

            if (_attachedTrail.material == null || _attachedTrail.material.shader == null)
            {
                _attachedTrail.material = new Material(Shader.Find("Sprites/Default"));
            }
        }

        private void OnEnable()
        {
            _comboController ??= GetComponent<KhiMeleeComboController>();
            if (_comboController == null)
            {
                return;
            }

            _comboController.AttackStarted += HandleAttackStarted;
        }

        private void OnDisable()
        {
            if (_comboController == null)
            {
                return;
            }

            _comboController.AttackStarted -= HandleAttackStarted;
        }

        public void HandleAttackStarted(KhiAttackRequest request, AttackStepData step)
        {
            ShowTemporarySlash(request, step);
            PlayAnimatorTrigger(step);
        }

        private void PlayAnimatorTrigger(AttackStepData step)
        {
            if (animator == null || string.IsNullOrEmpty(step.animatorTrigger))
            {
                return;
            }

            animator.ResetTrigger("Attack_1");
            animator.ResetTrigger("Attack_2");
            animator.ResetTrigger("Attack_3");

            // NetworkAnimator 가 있으면 SetTrigger 를 거쳐 다른 클라에 자동 sync.
            // 없으면 (싱글 또는 prefab 미부착) 일반 Animator 로 fallback.
            if (networkAnimator != null)
            {
                networkAnimator.SetTrigger(step.animatorTrigger);
            }
            else
            {
                animator.SetTrigger(step.animatorTrigger);
            }
        }

        private void ShowTemporarySlash(KhiAttackRequest request, AttackStepData step)
        {
            if (!showTemporarySlash)
            {
                return;
            }

            TemporarySlashSpec spec = GetSlashSpec(step.comboStep);
            float directionAngle = request.AimAngleDegrees;
            Vector2 rotatedOffset = (Vector2)(Quaternion.Euler(0f, 0f, directionAngle) * (Vector3)step.hitboxOffset);
            // CL: WeaponData 의 globalPostRotationOffset 을 hitbox 와 동일하게 회전 후 적용 → 좌우 대칭 보장.
            Vector2 globalPostRotationOffset = (_comboController != null && _comboController.WeaponData != null)
                ? _comboController.WeaponData.GlobalHitboxPostRotationOffset
                : Vector2.zero;
            Vector2 center = (Vector2)request.Origin + rotatedOffset + globalPostRotationOffset;
            Vector2 areaSize = step.hitboxSize * slashAreaScale;

            GameObject slashObject = new GameObject($"Khi_TemporarySlash_{step.comboStep}");
            slashObject.transform.position = new Vector3(center.x, center.y, request.Origin.z);
            slashObject.transform.rotation = Quaternion.Euler(0f, 0f, directionAngle + spec.Angle);
            slashObject.transform.localScale = new Vector3(areaSize.x * spec.SizeMultiplier.x, areaSize.y * spec.SizeMultiplier.y, 1f);

            SpriteRenderer slashRenderer = slashObject.AddComponent<SpriteRenderer>();
            slashRenderer.sprite = GetSlashSprite(spec.SpriteKind);
            slashRenderer.color = spec.Color;
            CopySortingLayerFromOwner(slashRenderer);
            slashRenderer.sortingOrder = temporarySlashSortingOrder;

            StartCoroutine(FadeAndDestroySlash(slashObject, slashRenderer, spec.Duration));
        }

        private TemporarySlashSpec GetSlashSpec(int comboStep)
        {
            return comboStep switch
            {
                2 => new TemporarySlashSpec
                {
                    SpriteKind = TemporarySlashSpriteKind.Thin,
                    SizeMultiplier = new Vector2(0.92f, 0.9f),
                    Angle = -35f,
                    Duration = 0.09f,
                    Color = secondSlashColor
                },
                3 => new TemporarySlashSpec
                {
                    SpriteKind = TemporarySlashSpriteKind.Wide,
                    SizeMultiplier = new Vector2(1f, 1f),
                    Angle = 0f,
                    Duration = 0.13f,
                    Color = thirdSlashColor
                },
                _ => new TemporarySlashSpec
                {
                    SpriteKind = TemporarySlashSpriteKind.Thin,
                    SizeMultiplier = new Vector2(0.86f, 0.85f),
                    Angle = 35f,
                    Duration = 0.08f,
                    Color = firstSlashColor
                }
            };
        }

        private IEnumerator FadeAndDestroySlash(GameObject slashObject, SpriteRenderer slashRenderer, float duration)
        {
            if (slashRenderer == null)
            {
                Destroy(slashObject);
                yield break;
            }

            Color startColor = slashRenderer.color;
            float elapsed = 0f;
            float safeDuration = Mathf.Max(0.01f, duration);

            while (elapsed < safeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(startColor.a, 0f, elapsed / safeDuration);
                slashRenderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                yield return null;
            }

            Destroy(slashObject);
        }

        private Sprite GetSlashSprite(TemporarySlashSpriteKind spriteKind)
        {
            if (spriteKind == TemporarySlashSpriteKind.Wide)
            {
                if (externalWideSlashSprite != null)
                {
                    return externalWideSlashSprite;
                }
                _wideSlashSprite ??= CreateSlashSprite("Khi_TemporaryWideSlash", 96, 96, true);
                return _wideSlashSprite;
            }

            if (externalThinSlashSprite != null)
            {
                return externalThinSlashSprite;
            }
            _thinSlashSprite ??= CreateSlashSprite("Khi_TemporaryThinSlash", 128, 64, false);
            return _thinSlashSprite;
        }

        private static Sprite CreateSlashSprite(string spriteName, int width, int height, bool wide)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = spriteName + "Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color clear = new Color(1f, 1f, 1f, 0f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    texture.SetPixel(x, y, clear);
                }
            }

            if (wide)
            {
                DrawBrushStroke(texture, new Vector2(0.16f, 0.7f), new Vector2(0.85f, 0.26f), 0.16f, 1f);
                DrawBrushStroke(texture, new Vector2(0.08f, 0.48f), new Vector2(0.78f, 0.72f), 0.12f, 0.82f);
                DrawBrushStroke(texture, new Vector2(0.22f, 0.22f), new Vector2(0.92f, 0.6f), 0.1f, 0.72f);
            }
            else
            {
                DrawBrushStroke(texture, new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.24f), 0.14f, 1f);
                DrawBrushStroke(texture, new Vector2(0.18f, 0.52f), new Vector2(0.78f, 0.42f), 0.07f, 0.6f);
            }

            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), width);
            sprite.name = spriteName + "Sprite";
            return sprite;
        }

        private static void DrawBrushStroke(Texture2D texture, Vector2 start01, Vector2 end01, float thickness01, float alphaMultiplier)
        {
            int width = texture.width;
            int height = texture.height;
            Vector2 start = new Vector2(start01.x * (width - 1), start01.y * (height - 1));
            Vector2 end = new Vector2(end01.x * (width - 1), end01.y * (height - 1));
            Vector2 segment = end - start;
            float segmentLengthSquared = Mathf.Max(segment.sqrMagnitude, 0.0001f);
            float thicknessPixels = thickness01 * Mathf.Min(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2 pixel = new Vector2(x, y);
                    float t = Mathf.Clamp01(Vector2.Dot(pixel - start, segment) / segmentLengthSquared);
                    Vector2 closest = start + segment * t;
                    float distance = Vector2.Distance(pixel, closest);
                    float jaggedEdge = 0.68f + DeterministicNoise(x, y) * 0.55f;
                    float localThickness = thicknessPixels * jaggedEdge * Mathf.Sin(t * Mathf.PI);
                    if (distance > localThickness)
                    {
                        continue;
                    }

                    float centerAlpha = 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, localThickness));
                    float headFade = Mathf.SmoothStep(0f, 1f, Mathf.Sin(t * Mathf.PI));
                    float alpha = Mathf.Clamp01((0.3f + centerAlpha * 0.85f) * headFade * alphaMultiplier);
                    Color current = texture.GetPixel(x, y);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(current.a, alpha)));
                }
            }
        }

        private void CopySortingLayerFromOwner(SpriteRenderer targetRenderer)
        {
            SpriteRenderer ownerRenderer = GetComponentInChildren<SpriteRenderer>();
            if (ownerRenderer == null)
            {
                return;
            }

            targetRenderer.sortingLayerID = ownerRenderer.sortingLayerID;
        }

        private static float DeterministicNoise(int x, int y)
        {
            int n = x * 73856093 ^ y * 19349663;
            n = (n << 13) ^ n;
            return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f;
        }

        private struct TemporarySlashSpec
        {
            public TemporarySlashSpriteKind SpriteKind;
            public Vector2 SizeMultiplier;
            public float Angle;
            public float Duration;
            public Color Color;
        }

        private enum TemporarySlashSpriteKind
        {
            Thin,
            Wide
        }
    }
}
