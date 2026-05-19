using System;
using System.Collections.Generic;
using LostMemory.Rendering;
using LostMemory.TestKhi;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.Boss.Rena
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Boss/Rena/Rena Boss Thunder Strike Area")]
    public sealed class RenaBossThunderStrikeArea : MonoBehaviour
    {
        [SerializeField] private GameObject owner;
        [SerializeField] private Health ownerHealth;
        [SerializeField] private LayerMask targetLayerMask = 1 << 10;
        [SerializeField, Min(0.05f)] private float radius = 0.85f;
        [SerializeField, Min(0f)] private float damage = 13f;
        [SerializeField, Min(0f)] private float warningDuration = 0.85f;
        [SerializeField, Min(0f)] private float invincibilityDuration = 0.35f;
        [SerializeField, Min(0.01f)] private float visualScale = 1.2f;
        [SerializeField, Min(0f)] private float animationFrameRate = 18f;
        [SerializeField] private string sortingLayerName = "Foreground";
        [SerializeField] private int sortingOrder = 30;
        [SerializeField] private Sprite[] strikeFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] glowFrames = Array.Empty<Sprite>();
        [SerializeField] private bool applyHitStun;
        [SerializeField, Min(0f)] private float hitStunDuration = 0.35f;
        [SerializeField] private bool debugLogging;

        private static Material _warningMaterial;

        private readonly HashSet<Health> _hitTargets = new HashSet<Health>();
        private SpriteRenderer _strikeRenderer;
        private SpriteRenderer _glowRenderer;
        private LineRenderer _warningOutline;
        private float _startedAt;
        private float _impactAt;
        private float _animationElapsed;
        private bool _impacted;

        private void Update()
        {
            if (ShouldCancel())
            {
                Destroy(gameObject);
                return;
            }

            if (!_impacted)
            {
                if (Time.time >= _impactAt)
                {
                    ExecuteImpact();
                    return;
                }

                UpdateWarning();
                return;
            }

            UpdateImpactAnimation(Time.deltaTime);
        }

        public void Configure(
            GameObject configuredOwner,
            Health configuredOwnerHealth,
            LayerMask configuredTargetLayerMask,
            Vector2 center,
            float configuredRadius,
            float configuredDamage,
            float configuredWarningDuration,
            float configuredInvincibilityDuration,
            float configuredVisualScale,
            Sprite[] configuredStrikeFrames,
            Sprite[] configuredGlowFrames,
            float configuredAnimationFrameRate,
            string configuredSortingLayerName,
            int configuredSortingOrder,
            bool configuredDebugLogging = false,
            bool configuredApplyHitStun = false,
            float configuredHitStunDuration = 0f)
        {
            owner = configuredOwner;
            ownerHealth = configuredOwnerHealth;
            targetLayerMask = configuredTargetLayerMask;
            transform.position = new Vector3(center.x, center.y, 0f);
            radius = Mathf.Max(0.05f, configuredRadius);
            damage = Mathf.Max(0f, configuredDamage);
            warningDuration = Mathf.Max(0f, configuredWarningDuration);
            invincibilityDuration = Mathf.Max(0f, configuredInvincibilityDuration);
            visualScale = Mathf.Max(0.01f, configuredVisualScale);
            strikeFrames = configuredStrikeFrames ?? Array.Empty<Sprite>();
            glowFrames = configuredGlowFrames ?? Array.Empty<Sprite>();
            animationFrameRate = Mathf.Max(0f, configuredAnimationFrameRate);
            sortingLayerName = configuredSortingLayerName;
            sortingOrder = configuredSortingOrder;
            debugLogging = configuredDebugLogging;
            applyHitStun = configuredApplyHitStun;
            hitStunDuration = Mathf.Max(0f, configuredHitStunDuration);

            _startedAt = Time.time;
            _impactAt = Time.time + warningDuration;
            _animationElapsed = 0f;
            _impacted = false;
            _hitTargets.Clear();

            EnsureWarningOutline();
            EnsureVisualRenderers();
            SetVisualsActive(false);
            UpdateWarning();
        }

        private bool ShouldCancel()
        {
            return ownerHealth != null && ownerHealth.CurrentHealth <= 0f;
        }

        private void ExecuteImpact()
        {
            if (_impacted)
            {
                return;
            }

            _impacted = true;
            _animationElapsed = 0f;
            HideWarning();
            SetVisualsActive(true);
            ApplyFrame(0);
            DamageTargets();
            Log("Thunder strike impacted.");
        }

        private void DamageTargets()
        {
            if (damage <= 0f || radius <= 0f)
            {
                return;
            }

            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, radius, targetLayerMask);
            for (int i = 0; i < colliders.Length; i++)
            {
                Health targetHealth = ResolveHealth(colliders[i]);
                if (!CanDamage(targetHealth))
                {
                    continue;
                }

                _hitTargets.Add(targetHealth);
                Vector3 direction = targetHealth.transform.position - transform.position;
                direction.z = 0f;
                if (direction.sqrMagnitude <= 0.0001f)
                {
                    direction = Vector3.up;
                }

                targetHealth.Damage(
                    damage,
                    owner != null ? owner : gameObject,
                    invincibilityDuration,
                    invincibilityDuration,
                    direction.normalized);
                ApplyHitStun(targetHealth);
            }
        }

        private void ApplyHitStun(Health targetHealth)
        {
            if (!applyHitStun || hitStunDuration <= 0f || targetHealth == null)
            {
                return;
            }

            KhiHitStunController hitStun = targetHealth.GetComponentInParent<KhiHitStunController>();
            if (hitStun != null)
            {
                hitStun.TriggerHitStun(hitStunDuration);
            }
        }

        private bool CanDamage(Health targetHealth)
        {
            return targetHealth != null
                   && targetHealth != ownerHealth
                   && targetHealth.CurrentHealth > 0f
                   && targetHealth.CanTakeDamageThisFrame()
                   && !_hitTargets.Contains(targetHealth);
        }

        private static Health ResolveHealth(Collider2D collider)
        {
            return collider != null ? collider.GetComponentInParent<Health>() : null;
        }

        private void UpdateImpactAnimation(float deltaTime)
        {
            if (animationFrameRate <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            _animationElapsed += deltaTime;
            int frameIndex = Mathf.FloorToInt(_animationElapsed * animationFrameRate);
            int maxFrames = Mathf.Max(strikeFrames != null ? strikeFrames.Length : 0, glowFrames != null ? glowFrames.Length : 0);
            if (maxFrames <= 0 || frameIndex >= maxFrames)
            {
                Destroy(gameObject);
                return;
            }

            ApplyFrame(frameIndex);
        }

        private void ApplyFrame(int frameIndex)
        {
            if (_strikeRenderer != null && strikeFrames != null && strikeFrames.Length > 0)
            {
                _strikeRenderer.sprite = strikeFrames[Mathf.Clamp(frameIndex, 0, strikeFrames.Length - 1)];
            }

            if (_glowRenderer != null && glowFrames != null && glowFrames.Length > 0)
            {
                _glowRenderer.sprite = glowFrames[Mathf.Clamp(frameIndex, 0, glowFrames.Length - 1)];
            }
        }

        private void EnsureVisualRenderers()
        {
            if (_strikeRenderer == null)
            {
                GameObject strikeObject = new GameObject("Strike");
                strikeObject.transform.SetParent(transform, false);
                _strikeRenderer = strikeObject.AddComponent<SpriteRenderer>();
                RuntimeSpriteMaterialUtility.ApplySpriteMaterial(_strikeRenderer);
            }

            _strikeRenderer.sortingLayerName = sortingLayerName;
            _strikeRenderer.sortingOrder = sortingOrder;
            _strikeRenderer.transform.localScale = Vector3.one * visualScale;

            if (_glowRenderer == null && glowFrames != null && glowFrames.Length > 0)
            {
                GameObject glowObject = new GameObject("Glow");
                glowObject.transform.SetParent(transform, false);
                _glowRenderer = glowObject.AddComponent<SpriteRenderer>();
                RuntimeSpriteMaterialUtility.ApplyAdditiveSpriteMaterial(_glowRenderer);
            }

            if (_glowRenderer != null)
            {
                _glowRenderer.sortingLayerName = sortingLayerName;
                _glowRenderer.sortingOrder = sortingOrder - 1;
                _glowRenderer.transform.localScale = Vector3.one * visualScale;
            }
        }

        private void SetVisualsActive(bool active)
        {
            if (_strikeRenderer != null)
            {
                _strikeRenderer.gameObject.SetActive(active);
            }

            if (_glowRenderer != null)
            {
                _glowRenderer.gameObject.SetActive(active);
            }
        }

        private void EnsureWarningOutline()
        {
            if (_warningOutline != null)
            {
                return;
            }

            GameObject outlineObject = new GameObject("WarningOutline");
            outlineObject.transform.SetParent(transform, false);
            _warningOutline = outlineObject.AddComponent<LineRenderer>();
            _warningOutline.useWorldSpace = false;
            _warningOutline.loop = true;
            _warningOutline.alignment = LineAlignment.View;
            _warningOutline.textureMode = LineTextureMode.Stretch;
            _warningOutline.numCapVertices = 4;
            _warningOutline.numCornerVertices = 4;
            _warningOutline.sharedMaterial = ResolveWarningMaterial();
        }

        private void UpdateWarning()
        {
            if (_warningOutline == null)
            {
                return;
            }

            float progress = warningDuration <= 0f
                ? 1f
                : Mathf.Clamp01((Time.time - _startedAt) / warningDuration);
            Color color = Color.Lerp(
                new Color(0.35f, 0.85f, 1f, 0.25f),
                new Color(0.8f, 0.95f, 1f, 0.95f),
                progress);

            _warningOutline.enabled = true;
            _warningOutline.startColor = color;
            _warningOutline.endColor = color;
            _warningOutline.startWidth = Mathf.Lerp(0.035f, 0.075f, progress);
            _warningOutline.endWidth = _warningOutline.startWidth;
            _warningOutline.sortingLayerName = sortingLayerName;
            _warningOutline.sortingOrder = sortingOrder + 1;

            const int segmentCount = 72;
            _warningOutline.positionCount = segmentCount;
            for (int i = 0; i < segmentCount; i++)
            {
                float angle = Mathf.PI * 2f * i / segmentCount;
                _warningOutline.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
        }

        private void HideWarning()
        {
            if (_warningOutline != null)
            {
                _warningOutline.enabled = false;
            }
        }

        private static Material ResolveWarningMaterial()
        {
            if (_warningMaterial != null)
            {
                return _warningMaterial;
            }

            _warningMaterial = RuntimeSpriteMaterialUtility.GetSpriteMaterial();
            return _warningMaterial;
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[RenaBossThunderStrikeArea] " + message, this);
            }
        }
    }
}
