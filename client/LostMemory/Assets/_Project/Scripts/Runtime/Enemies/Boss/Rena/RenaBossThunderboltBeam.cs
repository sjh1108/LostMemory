using System;
using System.Collections.Generic;
using LostMemory.Rendering;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.Boss.Rena
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Boss/Rena/Rena Boss Thunderbolt Beam")]
    public sealed class RenaBossThunderboltBeam : MonoBehaviour
    {
        [SerializeField] private GameObject owner;
        [SerializeField] private Health ownerHealth;
        [SerializeField] private Transform originTransform;
        [SerializeField] private LayerMask targetLayerMask = 1 << 10;
        [SerializeField, Min(0f)] private float damage = 6f;
        [SerializeField, Min(0f)] private float buildDuration = 0.55f;
        [SerializeField, Min(0.05f)] private float duration = 2.4f;
        [SerializeField, Min(0.1f)] private float beamLength = 15f;
        [SerializeField, Min(0.05f)] private float beamWidth = 1.05f;
        [SerializeField, Min(0.1f)] private float coreWidthMultiplier = 0.95f;
        [SerializeField, Min(1f)] private float glowWidthMultiplier = 1.45f;
        [SerializeField, Range(0f, 1f)] private float glowAlpha = 0.65f;
        [SerializeField, Range(0f, 0.8f)] private float segmentOverlap = 0.25f;
        [SerializeField] private float rotationDegrees = 360f;
        [SerializeField, Min(0f)] private float invincibilityDuration = 0.35f;
        [SerializeField, Min(0f)] private float damageInterval = 0.35f;
        [SerializeField, Min(0f)] private float animationFrameRate = 18f;
        [SerializeField] private string sortingLayerName = "Foreground";
        [SerializeField] private int sortingOrder = 31;
        [SerializeField] private Sprite[] coreFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] glowFrames = Array.Empty<Sprite>();
        [SerializeField] private bool debugLogging;

        private readonly Dictionary<Health, float> _nextDamageAllowedAt = new Dictionary<Health, float>();
        private readonly List<SpriteRenderer> _coreRenderers = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _glowRenderers = new List<SpriteRenderer>();
        private Vector2 _initialDirection = Vector2.right;
        private float _startAngle;
        private float _startedAt;
        private float _currentAngle;

        private void Update()
        {
            if (ShouldCancel())
            {
                Destroy(gameObject);
                return;
            }

            float elapsed = Time.time - _startedAt;
            if (elapsed >= buildDuration + duration)
            {
                Destroy(gameObject);
                return;
            }

            UpdateOrigin();

            if (elapsed < buildDuration)
            {
                float buildProgress = buildDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / buildDuration);
                UpdateRotation(0f);
                UpdateSegments(Mathf.SmoothStep(0f, 1f, buildProgress), elapsed, true);
                return;
            }

            float rotationElapsed = elapsed - buildDuration;
            UpdateRotation(rotationElapsed);
            UpdateSegments(1f, rotationElapsed, false);
            DamageTargets();
        }

        public void Configure(
            GameObject configuredOwner,
            Health configuredOwnerHealth,
            Transform configuredOriginTransform,
            LayerMask configuredTargetLayerMask,
            Vector2 configuredInitialDirection,
            float configuredDamage,
            float configuredBuildDuration,
            float configuredDuration,
            float configuredBeamLength,
            float configuredBeamWidth,
            float configuredRotationDegrees,
            float configuredInvincibilityDuration,
            float configuredDamageInterval,
            Sprite[] configuredCoreFrames,
            Sprite[] configuredGlowFrames,
            float configuredAnimationFrameRate,
            string configuredSortingLayerName,
            int configuredSortingOrder,
            bool configuredDebugLogging = false)
        {
            owner = configuredOwner;
            ownerHealth = configuredOwnerHealth;
            originTransform = configuredOriginTransform;
            targetLayerMask = configuredTargetLayerMask;
            _initialDirection = configuredInitialDirection.sqrMagnitude > 0.0001f
                ? configuredInitialDirection.normalized
                : Vector2.right;
            damage = Mathf.Max(0f, configuredDamage);
            buildDuration = Mathf.Max(0f, configuredBuildDuration);
            duration = Mathf.Max(0.05f, configuredDuration);
            beamLength = Mathf.Max(0.1f, configuredBeamLength);
            beamWidth = Mathf.Max(0.05f, configuredBeamWidth);
            rotationDegrees = configuredRotationDegrees;
            invincibilityDuration = Mathf.Max(0f, configuredInvincibilityDuration);
            damageInterval = Mathf.Max(0f, configuredDamageInterval);
            coreFrames = configuredCoreFrames ?? Array.Empty<Sprite>();
            glowFrames = configuredGlowFrames ?? Array.Empty<Sprite>();
            animationFrameRate = Mathf.Max(0f, configuredAnimationFrameRate);
            sortingLayerName = configuredSortingLayerName;
            sortingOrder = configuredSortingOrder;
            debugLogging = configuredDebugLogging;

            _startedAt = Time.time;
            _startAngle = Mathf.Atan2(_initialDirection.y, _initialDirection.x) * Mathf.Rad2Deg;
            _currentAngle = _startAngle;
            _nextDamageAllowedAt.Clear();

            UpdateOrigin();
            UpdateRotation(0f);
            UpdateSegments(0f, 0f, true);
            Log("Thunderbolt beam started.");
        }

        private bool ShouldCancel()
        {
            return ownerHealth != null && ownerHealth.CurrentHealth <= 0f;
        }

        private void UpdateOrigin()
        {
            if (originTransform == null)
            {
                return;
            }

            Vector3 origin = originTransform.position;
            transform.position = new Vector3(origin.x, origin.y, origin.z);
        }

        private void UpdateRotation(float elapsed)
        {
            float progress = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            _currentAngle = _startAngle + rotationDegrees * progress;
            transform.rotation = Quaternion.Euler(0f, 0f, _currentAngle);
        }

        private void UpdateSegments(float lengthMultiplier, float elapsed, bool isBuilding)
        {
            if (!HasAnyFrames())
            {
                return;
            }

            int segmentCount = ResolveSegmentCount();
            EnsureSegmentCapacity(segmentCount);

            float visibleLength = beamLength * Mathf.Clamp01(lengthMultiplier);
            float segmentSpacing = ResolveSegmentSpacing();
            float buildProgress = buildDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / buildDuration);

            for (int i = 0; i < segmentCount; i++)
            {
                float segmentStart = i * segmentSpacing;
                bool active = visibleLength > 0.01f && segmentStart <= visibleLength;
                Sprite coreFrame = SelectSegmentFrame(coreFrames, elapsed, i, isBuilding, buildProgress);
                Sprite glowFrame = SelectSegmentFrame(glowFrames, elapsed, i, isBuilding, buildProgress);
                ApplySegment(_coreRenderers[i], coreFrame ?? glowFrame, active, segmentStart, coreWidthMultiplier, Color.white);
                ApplySegment(
                    _glowRenderers[i],
                    glowFrame ?? coreFrame,
                    active,
                    segmentStart,
                    glowWidthMultiplier,
                    new Color(1f, 1f, 1f, glowAlpha));
            }

            SetExtraSegmentsInactive(segmentCount);
        }

        private bool HasAnyFrames()
        {
            return HasFrames(coreFrames) || HasFrames(glowFrames);
        }

        private static bool HasFrames(Sprite[] frames)
        {
            return frames != null && frames.Length > 0;
        }

        private Sprite SelectSegmentFrame(Sprite[] frames, float elapsed, int segmentIndex, bool isBuilding, float buildProgress)
        {
            if (!HasFrames(frames))
            {
                return null;
            }

            int frameIndex = isBuilding
                ? Mathf.FloorToInt(buildProgress * (frames.Length - 1) + segmentIndex)
                : Mathf.FloorToInt(elapsed * animationFrameRate + segmentIndex);
            return frames[PositiveModulo(frameIndex, frames.Length)];
        }

        private int ResolveSegmentCount()
        {
            float spacing = ResolveSegmentSpacing();
            return Mathf.Clamp(Mathf.CeilToInt(beamLength / spacing) + 2, 1, 96);
        }

        private float ResolveSegmentSpacing()
        {
            Sprite frame = FirstFrame(coreFrames) != null ? FirstFrame(coreFrames) : FirstFrame(glowFrames);
            if (frame == null || frame.bounds.size.x <= 0.0001f)
            {
                return 1f;
            }

            return Mathf.Max(0.1f, frame.bounds.size.x * (1f - segmentOverlap));
        }

        private static Sprite FirstFrame(Sprite[] frames)
        {
            return HasFrames(frames) ? frames[0] : null;
        }

        private void EnsureSegmentCapacity(int segmentCount)
        {
            while (_coreRenderers.Count < segmentCount)
            {
                int index = _coreRenderers.Count;
                _coreRenderers.Add(CreateSegmentRenderer("Core_" + index, sortingOrder, false, 1f));
                _glowRenderers.Add(CreateSegmentRenderer("Glow_" + index, sortingOrder - 1, true, glowAlpha));
            }
        }

        private SpriteRenderer CreateSegmentRenderer(string objectName, int configuredSortingOrder, bool additive, float alpha)
        {
            GameObject segmentObject = new GameObject(objectName);
            segmentObject.transform.SetParent(transform, false);
            SpriteRenderer renderer = segmentObject.AddComponent<SpriteRenderer>();
            if (additive)
            {
                RuntimeSpriteMaterialUtility.ApplyAdditiveSpriteMaterial(renderer);
            }
            else
            {
                RuntimeSpriteMaterialUtility.ApplySpriteMaterial(renderer);
            }

            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = configuredSortingOrder;
            renderer.color = new Color(1f, 1f, 1f, alpha);
            renderer.enabled = false;
            return renderer;
        }

        private void ApplySegment(
            SpriteRenderer renderer,
            Sprite frame,
            bool active,
            float segmentStart,
            float widthMultiplier,
            Color color)
        {
            if (renderer == null)
            {
                return;
            }

            if (!active || frame == null || frame.bounds.size.x <= 0.0001f || frame.bounds.size.y <= 0.0001f)
            {
                renderer.enabled = false;
                return;
            }

            renderer.enabled = true;
            renderer.sprite = frame;
            renderer.color = color;
            renderer.sortingLayerName = sortingLayerName;
            renderer.transform.localPosition = new Vector3(segmentStart + frame.bounds.size.x * 0.5f, 0f, 0f);
            renderer.transform.localRotation = Quaternion.identity;
            renderer.transform.localScale = new Vector3(
                1f,
                beamWidth * widthMultiplier / frame.bounds.size.y,
                1f);
        }

        private void SetExtraSegmentsInactive(int activeSegmentCount)
        {
            for (int i = activeSegmentCount; i < _coreRenderers.Count; i++)
            {
                if (_coreRenderers[i] != null)
                {
                    _coreRenderers[i].enabled = false;
                }

                if (_glowRenderers[i] != null)
                {
                    _glowRenderers[i].enabled = false;
                }
            }
        }

        private void DamageTargets()
        {
            if (damage <= 0f || targetLayerMask.value == 0)
            {
                return;
            }

            Vector2 direction = CurrentDirection();
            Vector2 center = (Vector2)transform.position + direction * (beamLength * 0.5f);
            Collider2D[] colliders = Physics2D.OverlapBoxAll(center, new Vector2(beamLength, beamWidth), _currentAngle, targetLayerMask);
            for (int i = 0; i < colliders.Length; i++)
            {
                Health targetHealth = ResolveHealth(colliders[i]);
                if (!CanDamage(targetHealth))
                {
                    continue;
                }

                _nextDamageAllowedAt[targetHealth] = Time.time + damageInterval;
                targetHealth.Damage(
                    damage,
                    owner != null ? owner : gameObject,
                    invincibilityDuration,
                    invincibilityDuration,
                    new Vector3(direction.x, direction.y, 0f));
            }
        }

        private bool CanDamage(Health targetHealth)
        {
            if (targetHealth == null
                || targetHealth == ownerHealth
                || targetHealth.CurrentHealth <= 0f
                || IsOwnedByAttacker(targetHealth)
                || !targetHealth.CanTakeDamageThisFrame())
            {
                return false;
            }

            return !_nextDamageAllowedAt.TryGetValue(targetHealth, out float nextAllowedAt)
                   || Time.time >= nextAllowedAt;
        }

        private bool IsOwnedByAttacker(Health targetHealth)
        {
            return owner != null
                   && (targetHealth.gameObject == owner || targetHealth.transform.IsChildOf(owner.transform));
        }

        private static Health ResolveHealth(Collider2D collider)
        {
            return collider != null ? collider.GetComponentInParent<Health>() : null;
        }

        private Vector2 CurrentDirection()
        {
            float radians = _currentAngle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
        }

        private static int PositiveModulo(int value, int length)
        {
            int modulo = value % length;
            return modulo < 0 ? modulo + length : modulo;
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[RenaBossThunderboltBeam] " + message, this);
            }
        }
    }
}
