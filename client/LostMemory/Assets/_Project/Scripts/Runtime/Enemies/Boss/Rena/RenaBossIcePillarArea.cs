using System;
using System.Collections.Generic;
using LostMemory.Rendering;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.Boss.Rena
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Boss/Rena/Rena Boss Ice Pillar Area")]
    public sealed class RenaBossIcePillarArea : MonoBehaviour
    {
        [SerializeField] private GameObject owner;
        [SerializeField] private Health ownerHealth;
        [SerializeField] private LayerMask targetLayerMask = 1 << 10;
        [SerializeField] private Vector2 areaSize = new Vector2(2f, 3f);
        [SerializeField, Min(0f)] private float damage = 18f;
        [SerializeField, Min(0f)] private float damageDelay = 0.08f;
        [SerializeField, Min(0f)] private float invincibilityDuration = 0.35f;
        [SerializeField] private Vector2 visualOffset = Vector2.zero;
        [SerializeField, Min(1)] private int visualRows = 1;
        [SerializeField, Min(0.01f)] private float visualScale = 1.4f;
        [SerializeField, Min(0f)] private float animationFrameRate = 18f;
        [SerializeField] private string sortingLayerName = "Foreground";
        [SerializeField] private int sortingOrder = 28;
        [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();
        [SerializeField] private bool debugLogging;

        private readonly HashSet<Health> _hitTargets = new HashSet<Health>();
        private readonly List<SpriteRenderer> _renderers = new List<SpriteRenderer>();
        private float _elapsed;
        private bool _damaged;

        private void Update()
        {
            if (ownerHealth != null && ownerHealth.CurrentHealth <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            _elapsed += Time.deltaTime;
            if (!_damaged && _elapsed >= damageDelay)
            {
                _damaged = true;
                DamageTargets();
            }

            UpdateAnimation();
        }

        public void Configure(
            GameObject configuredOwner,
            Health configuredOwnerHealth,
            LayerMask configuredTargetLayerMask,
            Vector2 center,
            Vector2 configuredAreaSize,
            float configuredDamage,
            float configuredDamageDelay,
            float configuredInvincibilityDuration,
            Vector2 configuredVisualOffset,
            int configuredVisualRows,
            float configuredVisualScale,
            Sprite[] configuredFrames,
            float configuredAnimationFrameRate,
            string configuredSortingLayerName,
            int configuredSortingOrder,
            bool configuredDebugLogging = false)
        {
            owner = configuredOwner;
            ownerHealth = configuredOwnerHealth;
            targetLayerMask = configuredTargetLayerMask;
            transform.position = new Vector3(center.x, center.y, 0f);
            areaSize = new Vector2(
                Mathf.Max(0.05f, configuredAreaSize.x),
                Mathf.Max(0.05f, configuredAreaSize.y));
            damage = Mathf.Max(0f, configuredDamage);
            damageDelay = Mathf.Max(0f, configuredDamageDelay);
            invincibilityDuration = Mathf.Max(0f, configuredInvincibilityDuration);
            visualOffset = configuredVisualOffset;
            visualRows = Mathf.Max(1, configuredVisualRows);
            visualScale = Mathf.Max(0.01f, configuredVisualScale);
            frames = configuredFrames ?? Array.Empty<Sprite>();
            animationFrameRate = Mathf.Max(0f, configuredAnimationFrameRate);
            sortingLayerName = configuredSortingLayerName;
            sortingOrder = configuredSortingOrder;
            debugLogging = configuredDebugLogging;

            _elapsed = 0f;
            _damaged = false;
            _hitTargets.Clear();
            EnsureRenderers();
            ApplyVisualTransforms();
            ApplyFrame(0);
        }

        private void DamageTargets()
        {
            if (damage <= 0f || targetLayerMask.value == 0)
            {
                return;
            }

            Collider2D[] colliders = Physics2D.OverlapBoxAll(transform.position, areaSize, 0f, targetLayerMask);
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
            }

            Log("Ice pillar area damaged targets.");
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

        private void EnsureRenderers()
        {
            while (_renderers.Count < visualRows)
            {
                GameObject visualObject = new GameObject("Visual");
                visualObject.transform.SetParent(transform, false);
                SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
                RuntimeSpriteMaterialUtility.ApplySpriteMaterial(renderer);
                _renderers.Add(renderer);
            }

            for (int i = _renderers.Count - 1; i >= visualRows; i--)
            {
                SpriteRenderer renderer = _renderers[i];
                if (renderer != null)
                {
                    Destroy(renderer.gameObject);
                }

                _renderers.RemoveAt(i);
            }
        }

        private void ApplyVisualTransforms()
        {
            for (int i = 0; i < _renderers.Count; i++)
            {
                SpriteRenderer renderer = _renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                float rowProgress = visualRows <= 1 ? 0.5f : i / (visualRows - 1f);
                float rowOffsetY = visualRows <= 1 ? 0f : Mathf.Lerp(-0.24f, 0.24f, rowProgress) * areaSize.y;
                float staggerX = visualRows <= 1 ? 0f : ((i % 2 == 0) ? -0.06f : 0.06f) * areaSize.x;

                renderer.transform.localPosition = new Vector3(visualOffset.x + staggerX, visualOffset.y + rowOffsetY, 0f);
                renderer.transform.localScale = Vector3.one * visualScale;
                renderer.sortingLayerName = sortingLayerName;
                renderer.sortingOrder = sortingOrder;
            }
        }

        private void UpdateAnimation()
        {
            if (animationFrameRate <= 0f || frames == null || frames.Length == 0)
            {
                Destroy(gameObject);
                return;
            }

            int frameIndex = Mathf.FloorToInt(_elapsed * animationFrameRate);
            if (frameIndex >= frames.Length)
            {
                Destroy(gameObject);
                return;
            }

            ApplyFrame(frameIndex);
        }

        private void ApplyFrame(int frameIndex)
        {
            if (frames == null || frames.Length == 0)
            {
                return;
            }

            Sprite frame = frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];
            for (int i = 0; i < _renderers.Count; i++)
            {
                SpriteRenderer renderer = _renderers[i];
                if (renderer != null)
                {
                    renderer.sprite = frame;
                }
            }
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[RenaBossIcePillarArea] " + message, this);
            }
        }
    }
}
