using System.Collections.Generic;
using LostMemory.Combat.Telegraph;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Skeleton Mage Area Attack")]
    public sealed class SkeletonMageAreaAttack : MonoBehaviour
    {
        [Header("Bindings")]
        [SerializeField] private CircleCollider2D hitCollider;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Animator impactAnimator;
        [SerializeField] private AttackTelegraph2DView telegraphView;

        [Header("Warning")]
        [SerializeField] private Color warningColor = new Color(1f, 0.12f, 0.05f, 0.38f);
        [SerializeField] private bool showWarningTimingMarker;
        [SerializeField] private Color warningTimingMarkerColor = new Color(1f, 0.92f, 0.55f, 0.75f);
        [SerializeField, Range(0.01f, 0.25f)] private float warningTimingMarkerThickness = 0.055f;

        [Header("Timing Outline")]
        [SerializeField] private bool showWarningOutline = true;
        [Tooltip("Color of the closing outline used as the area attack timing cue.")]
        [SerializeField] private Color warningOutlineColor = new Color(1f, 0.92f, 0.55f, 0.9f);
        [SerializeField, Min(3)] private int warningOutlineSegments = 96;
        [SerializeField, Min(0.001f)] private float warningOutlineWidth = 0.05f;
        [SerializeField] private string warningOutlineSortingLayerName = "Foreground";
        [SerializeField] private int warningOutlineSortingOrder = 51;

        [Header("Defaults")]
        [SerializeField, Min(0f)] private float defaultRadius = 1.15f;
        [SerializeField, Min(0f)] private float defaultDamage = 13f;
        [SerializeField, Min(0f)] private float defaultWarningDuration = 0.62f;
        [SerializeField, Min(0f)] private float defaultLifetime = 1.1f;
        [SerializeField, Min(0.01f)] private float impactVisualDuration = 0.75f;
        [SerializeField, Min(0f)] private float defaultVisualScale = 1.2f;
        [SerializeField, Min(0f)] private float defaultInvincibilityDuration = 0.45f;
        [SerializeField] private bool defaultApplyKnockback = true;
        [SerializeField, Min(0f)] private float defaultKnockbackForce = 180f;
        [SerializeField] private LayerMask defaultTargetLayerMask = 1 << 10;
        [SerializeField] private bool defaultCancelIfOwnerDies = true;

        private static Material _warningOutlineMaterial;

        private readonly HashSet<Health> _hitTargets = new HashSet<Health>();
        private GameObject _owner;
        private Health _ownerHealth;
        private LayerMask _targetLayerMask;
        private float _radius;
        private float _damage;
        private float _warningDuration;
        private float _impactAt;
        private float _destroyAt;
        private float _visualScale;
        private float _invincibilityDuration;
        private bool _applyKnockback;
        private float _knockbackForce;
        private bool _cancelIfOwnerDies;
        private bool _impacted;
        private bool _initialized;
        private LineRenderer _warningOutline;

        private void Reset()
        {
            ResolveBindings();
        }

        private void Awake()
        {
            ResolveBindings();
        }

        private void OnEnable()
        {
            _hitTargets.Clear();
            _impacted = false;
            if (!_initialized)
            {
                InitializeDefaults();
            }
        }

        private void OnDisable()
        {
            if (telegraphView != null)
            {
                telegraphView.Hide();
            }

            HideWarningOutline();
        }

        private void OnValidate()
        {
            warningTimingMarkerThickness = Mathf.Clamp(warningTimingMarkerThickness, 0.01f, 0.25f);
            warningOutlineSegments = Mathf.Max(3, warningOutlineSegments);
            warningOutlineWidth = Mathf.Max(0.001f, warningOutlineWidth);
            defaultRadius = Mathf.Max(0f, defaultRadius);
            defaultDamage = Mathf.Max(0f, defaultDamage);
            defaultWarningDuration = Mathf.Max(0f, defaultWarningDuration);
            defaultLifetime = Mathf.Max(0f, defaultLifetime);
            impactVisualDuration = Mathf.Max(0.01f, impactVisualDuration);
            defaultVisualScale = Mathf.Max(0f, defaultVisualScale);
            defaultInvincibilityDuration = Mathf.Max(0f, defaultInvincibilityDuration);
            defaultKnockbackForce = Mathf.Max(0f, defaultKnockbackForce);
        }

        public void Initialize(
            GameObject owner,
            Health ownerHealth,
            LayerMask targetLayerMask,
            float radius,
            float damage,
            float warningDuration,
            float lifetime,
            float visualScale,
            float invincibilityDuration,
            bool applyKnockback,
            float knockbackForce)
        {
            ResolveBindings();
            _initialized = true;
            _owner = owner;
            _ownerHealth = ownerHealth;
            _targetLayerMask = targetLayerMask;
            _radius = Mathf.Max(0f, radius);
            _damage = Mathf.Max(0f, damage);
            _warningDuration = Mathf.Max(0f, warningDuration);
            _impactAt = Time.time + _warningDuration;
            _destroyAt = _impactAt + Mathf.Max(0.01f, lifetime);
            _visualScale = Mathf.Max(0f, visualScale);
            _invincibilityDuration = Mathf.Max(0f, invincibilityDuration);
            _applyKnockback = applyKnockback;
            _knockbackForce = Mathf.Max(0f, knockbackForce);
            _cancelIfOwnerDies = defaultCancelIfOwnerDies;
            _impacted = false;

            if (hitCollider != null)
            {
                hitCollider.radius = _radius;
            }

            SetImpactVisualActive(false);
            ShowWarning();
        }

        private void InitializeDefaults()
        {
            _owner = null;
            _ownerHealth = null;
            _targetLayerMask = defaultTargetLayerMask;
            _radius = defaultRadius;
            _damage = defaultDamage;
            _warningDuration = Mathf.Max(0f, defaultWarningDuration);
            _impactAt = Time.time + _warningDuration;
            _destroyAt = _impactAt + Mathf.Max(0.01f, defaultLifetime);
            _visualScale = Mathf.Max(0f, defaultVisualScale);
            _invincibilityDuration = defaultInvincibilityDuration;
            _applyKnockback = defaultApplyKnockback;
            _knockbackForce = defaultKnockbackForce;
            _cancelIfOwnerDies = defaultCancelIfOwnerDies;
            _impacted = false;

            if (hitCollider != null)
            {
                hitCollider.radius = _radius;
            }

            SetImpactVisualActive(false);
            ShowWarning();
        }

        private void Update()
        {
            if (!_impacted)
            {
                if (ShouldCancelBeforeImpact())
                {
                    Destroy(gameObject);
                    return;
                }

                if (Time.time >= _impactAt)
                {
                    ExecuteImpact();
                }
                else
                {
                    RefreshWarning();
                }
            }

            if (_impacted && Time.time >= _destroyAt)
            {
                Destroy(gameObject);
            }
        }

        private void ShowWarning()
        {
            if (telegraphView == null || _warningDuration <= 0f)
            {
                UpdateWarningOutline(1f);
                return;
            }

            telegraphView.Show(BuildWarningRequest());
            UpdateWarningOutline(0f);
        }

        private void RefreshWarning()
        {
            float progress = ResolveWarningProgress();
            if (telegraphView == null || _warningDuration <= 0f)
            {
                UpdateWarningOutline(progress);
                return;
            }

            telegraphView.Refresh(BuildWarningRequest());
            UpdateWarningOutline(progress);
        }

        private AttackTelegraphRequest2D BuildWarningRequest()
        {
            float diameter = Mathf.Max(0f, _radius * 2f);
            float remaining = Mathf.Max(0f, _impactAt - Time.time);

            return new AttackTelegraphRequest2D
            {
                Shape = AttackTelegraphShape2D.Circle,
                Center = ResolveHitCenter(),
                Direction = Vector2.right,
                Size = new Vector2(diameter, diameter),
                Color = warningColor,
                Duration = remaining + Time.deltaTime,
                ShowTimingMarker = showWarningTimingMarker,
                TimingProgress = ResolveWarningProgress(),
                TimingMarkerColor = warningTimingMarkerColor,
                TimingMarkerThickness = warningTimingMarkerThickness
            };
        }

        private float ResolveWarningProgress()
        {
            if (_warningDuration <= 0f)
            {
                return 1f;
            }

            float remaining = Mathf.Max(0f, _impactAt - Time.time);
            return Mathf.Clamp01(1f - remaining / _warningDuration);
        }

        private bool ShouldCancelBeforeImpact()
        {
            return _cancelIfOwnerDies
                   && _ownerHealth != null
                   && _ownerHealth.CurrentHealth <= 0f;
        }

        private void ExecuteImpact()
        {
            if (_impacted)
            {
                return;
            }

            _impacted = true;

            if (telegraphView != null)
            {
                telegraphView.Hide();
            }

            HideWarningOutline();
            SetImpactVisualActive(true);
            RestartImpactAnimator();
            DamageTargets();
            _destroyAt = Mathf.Min(_destroyAt, Time.time + impactVisualDuration);
        }

        private void SetImpactVisualActive(bool active)
        {
            if (visualRoot == null)
            {
                return;
            }

            visualRoot.localScale = Vector3.one * Mathf.Max(0f, _visualScale);
            visualRoot.gameObject.SetActive(active);
        }

        private void UpdateWarningOutline(float progress)
        {
            if (!showWarningOutline || _radius <= 0f)
            {
                HideWarningOutline();
                return;
            }

            EnsureWarningOutline();
            if (_warningOutline == null)
            {
                return;
            }

            _warningOutline.enabled = true;
            _warningOutline.startColor = warningOutlineColor;
            _warningOutline.endColor = warningOutlineColor;
            _warningOutline.startWidth = warningOutlineWidth;
            _warningOutline.endWidth = warningOutlineWidth;
            _warningOutline.sortingLayerName = warningOutlineSortingLayerName;
            _warningOutline.sortingOrder = warningOutlineSortingOrder;

            int segmentCount = Mathf.Max(3, warningOutlineSegments);
            int drawnSegments = Mathf.Clamp(Mathf.CeilToInt(segmentCount * Mathf.Clamp01(progress)), 1, segmentCount);
            int pointCount = drawnSegments + 1;
            _warningOutline.positionCount = pointCount;

            const float startAngle = 90f;
            for (int i = 0; i < pointCount; i++)
            {
                float angle = (startAngle - 360f * i / segmentCount) * Mathf.Deg2Rad;
                Vector3 point = new Vector3(Mathf.Cos(angle) * _radius, Mathf.Sin(angle) * _radius, 0f);
                _warningOutline.SetPosition(i, point);
            }
        }

        private void EnsureWarningOutline()
        {
            if (_warningOutline != null)
            {
                return;
            }

            GameObject outlineObject = new GameObject("SkeletonMageAreaWarningOutline");
            outlineObject.layer = gameObject.layer;
            Transform outlineTransform = outlineObject.transform;
            outlineTransform.SetParent(transform, false);
            outlineTransform.localPosition = Vector3.zero;
            outlineTransform.localRotation = Quaternion.identity;
            outlineTransform.localScale = Vector3.one;

            _warningOutline = outlineObject.AddComponent<LineRenderer>();
            _warningOutline.useWorldSpace = false;
            _warningOutline.loop = false;
            _warningOutline.textureMode = LineTextureMode.Stretch;
            _warningOutline.alignment = LineAlignment.View;
            _warningOutline.numCapVertices = 4;
            _warningOutline.numCornerVertices = 4;
            _warningOutline.sharedMaterial = ResolveWarningOutlineMaterial();
        }

        private void HideWarningOutline()
        {
            if (_warningOutline != null)
            {
                _warningOutline.enabled = false;
            }
        }

        private static Material ResolveWarningOutlineMaterial()
        {
            if (_warningOutlineMaterial != null)
            {
                return _warningOutlineMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return null;
            }

            _warningOutlineMaterial = new Material(shader)
            {
                name = "SkeletonMageAreaWarningOutlineMaterial",
                hideFlags = HideFlags.HideAndDontSave
            };
            return _warningOutlineMaterial;
        }

        private void DamageTargets()
        {
            if (_damage <= 0f || _radius <= 0f)
            {
                return;
            }

            Collider2D[] targetColliders = Physics2D.OverlapCircleAll(ResolveHitCenter(), _radius, _targetLayerMask);
            for (int i = 0; i < targetColliders.Length; i++)
            {
                Health targetHealth = ResolveHealth(targetColliders[i]);
                if (!CanDamage(targetHealth))
                {
                    continue;
                }

                Damage(targetHealth);
            }
        }

        private Vector2 ResolveHitCenter()
        {
            return hitCollider != null
                ? hitCollider.transform.TransformPoint(hitCollider.offset)
                : transform.position;
        }

        private bool CanDamage(Health targetHealth)
        {
            return targetHealth != null
                   && targetHealth != _ownerHealth
                   && targetHealth.CurrentHealth > 0f
                   && targetHealth.CanTakeDamageThisFrame()
                   && !_hitTargets.Contains(targetHealth);
        }

        private void Damage(Health targetHealth)
        {
            Vector3 direction = targetHealth.transform.position - transform.position;
            direction.z = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector3.up;
            }

            direction.Normalize();
            _hitTargets.Add(targetHealth);
            targetHealth.Damage(_damage, _owner, 0f, _invincibilityDuration, direction);
            ApplyKnockback(targetHealth, direction);
        }

        private void ApplyKnockback(Health targetHealth, Vector3 direction)
        {
            if (!_applyKnockback
                || _knockbackForce <= 0f
                || targetHealth == null
                || !targetHealth.CanGetKnockback(null))
            {
                return;
            }

            TopDownController controller = targetHealth.GetComponent<TopDownController>();
            if (controller == null)
            {
                controller = targetHealth.GetComponentInParent<TopDownController>();
            }

            if (controller == null)
            {
                return;
            }

            Vector3 knockback = direction.normalized * _knockbackForce;
            knockback *= targetHealth.KnockbackForceMultiplier;
            knockback = targetHealth.ComputeKnockbackForce(knockback);
            if (knockback.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            controller.Impact(knockback.normalized, knockback.magnitude);
        }

        private void ResolveBindings()
        {
            if (hitCollider == null)
            {
                hitCollider = GetComponent<CircleCollider2D>();
            }

            if (visualRoot == null && transform.childCount > 0)
            {
                visualRoot = transform.GetChild(0);
            }

            if (telegraphView == null)
            {
                telegraphView = GetComponent<AttackTelegraph2DView>();
            }

            if (impactAnimator == null && visualRoot != null)
            {
                impactAnimator = visualRoot.GetComponentInChildren<Animator>(true);
            }
        }

        private void RestartImpactAnimator()
        {
            if (impactAnimator == null)
            {
                return;
            }

            impactAnimator.Play(0, 0, 0f);
            impactAnimator.Update(0f);
        }

        private static Health ResolveHealth(Component target)
        {
            if (target == null)
            {
                return null;
            }

            Health targetHealth = target.GetComponent<Health>();
            return targetHealth != null ? targetHealth : target.GetComponentInParent<Health>();
        }
    }
}
