using LostMemory.Rendering;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.Boss.Bertha
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Boss/Bertha/Bertha Projectile Burst Emitter")]
    public sealed class BerthaProjectileBurstEmitter : MonoBehaviour
    {
        private static Sprite _fallbackProjectileSprite;

        [SerializeField] private Transform projectileRoot;
        [SerializeField] private Transform projectileSpawnOrigin;
        [SerializeField] private SpriteRenderer sortingReference;
        [SerializeField] private LayerMask targetLayerMask = 1 << 10;
        [SerializeField] private LayerMask obstacleLayerMask = (1 << 8) | (1 << 24);
        [SerializeField] private Sprite[] projectileAnimationFrames = System.Array.Empty<Sprite>();
        [SerializeField, Min(0f)] private float projectileAnimationFrameRate = 12f;
        [SerializeField, Min(1)] private int projectileMaximumHits = 8;
        [SerializeField] private bool destroyProjectileOnHit = true;
        [SerializeField] private bool faceProjectileDirection = true;
        [SerializeField] private int sortingOrderOffset = 1;
        [SerializeField] private bool debugLogging;

        private void Reset()
        {
            projectileSpawnOrigin = transform;
            sortingReference = GetComponentInChildren<SpriteRenderer>(true);
        }

        private void OnValidate()
        {
            projectileSpawnOrigin ??= transform;
            projectileMaximumHits = Mathf.Max(1, projectileMaximumHits);
            sortingReference ??= GetComponentInChildren<SpriteRenderer>(true);
        }

        public void Configure(
            Transform configuredProjectileRoot,
            Transform configuredProjectileSpawnOrigin,
            SpriteRenderer configuredSortingReference,
            LayerMask configuredTargetLayerMask,
            LayerMask configuredObstacleLayerMask,
            Sprite[] configuredProjectileAnimationFrames,
            float configuredProjectileAnimationFrameRate,
            int configuredProjectileMaximumHits,
            bool configuredDestroyProjectileOnHit,
            bool configuredFaceProjectileDirection,
            int configuredSortingOrderOffset)
        {
            projectileRoot = configuredProjectileRoot;
            projectileSpawnOrigin = configuredProjectileSpawnOrigin != null ? configuredProjectileSpawnOrigin : transform;
            sortingReference = configuredSortingReference;
            targetLayerMask = configuredTargetLayerMask;
            obstacleLayerMask = configuredObstacleLayerMask;
            projectileAnimationFrames = configuredProjectileAnimationFrames ?? System.Array.Empty<Sprite>();
            projectileAnimationFrameRate = Mathf.Max(0f, configuredProjectileAnimationFrameRate);
            projectileMaximumHits = Mathf.Max(1, configuredProjectileMaximumHits);
            destroyProjectileOnHit = configuredDestroyProjectileOnHit;
            faceProjectileDirection = configuredFaceProjectileDirection;
            sortingOrderOffset = configuredSortingOrderOffset;
        }

        public void EmitRadial(
            GameObject owner,
            int projectileCount,
            float speed,
            float lifetime,
            float damage,
            float targetInvincibilityDuration,
            float hitRadius,
            float baseAngleDegrees,
            float spawnDistance,
            Transform homingTarget = null,
            float homingTurnSpeedDegrees = 0f,
            float homingDuration = 0f)
        {
            if (projectileCount <= 0)
            {
                return;
            }

            float angleStep = 360f / projectileCount;
            for (int i = 0; i < projectileCount; i++)
            {
                float angle = baseAngleDegrees + (angleStep * i);
                Vector2 direction = AngleToDirection(angle);
                SpawnProjectile(
                    owner,
                    direction,
                    speed,
                    lifetime,
                    damage,
                    targetInvincibilityDuration,
                    hitRadius,
                    spawnDistance,
                    homingTarget,
                    homingTurnSpeedDegrees,
                    homingDuration);
            }
        }

        public void EmitFan(
            GameObject owner,
            Vector2 facingDirection,
            int projectileCount,
            float spreadAngleDegrees,
            float speed,
            float lifetime,
            float damage,
            float targetInvincibilityDuration,
            float hitRadius,
            float angleOffsetDegrees,
            float spawnDistance,
            Transform homingTarget = null,
            float homingTurnSpeedDegrees = 0f,
            float homingDuration = 0f)
        {
            if (projectileCount <= 0)
            {
                return;
            }

            Vector2 baseDirection = facingDirection.sqrMagnitude > 0.0001f ? facingDirection.normalized : Vector2.right;
            float originAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg + angleOffsetDegrees;

            if (projectileCount == 1)
            {
                SpawnProjectile(
                    owner,
                    AngleToDirection(originAngle),
                    speed,
                    lifetime,
                    damage,
                    targetInvincibilityDuration,
                    hitRadius,
                    spawnDistance,
                    homingTarget,
                    homingTurnSpeedDegrees,
                    homingDuration);
                return;
            }

            float startAngle = originAngle - (spreadAngleDegrees * 0.5f);
            float angleStep = spreadAngleDegrees / (projectileCount - 1);

            for (int i = 0; i < projectileCount; i++)
            {
                float angle = startAngle + (angleStep * i);
                SpawnProjectile(
                    owner,
                    AngleToDirection(angle),
                    speed,
                    lifetime,
                    damage,
                    targetInvincibilityDuration,
                    hitRadius,
                    spawnDistance,
                    homingTarget,
                    homingTurnSpeedDegrees,
                    homingDuration);
            }
        }

        private void SpawnProjectile(
            GameObject owner,
            Vector2 direction,
            float speed,
            float lifetime,
            float damage,
            float targetInvincibilityDuration,
            float hitRadius,
            float spawnDistance,
            Transform homingTarget,
            float homingTurnSpeedDegrees,
            float homingDuration)
        {
            Vector2 normalizedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            Vector3 spawnOrigin = projectileSpawnOrigin != null ? projectileSpawnOrigin.position : transform.position;
            Vector3 spawnPosition = spawnOrigin + (Vector3)(normalizedDirection * Mathf.Max(0f, spawnDistance));

            GameObject projectileObject = new GameObject("BerthaProjectile");
            projectileObject.layer = gameObject.layer;
            projectileObject.transform.position = spawnPosition;
            projectileObject.transform.rotation = Quaternion.identity;

            if (projectileRoot != null && !IsInOwnerHierarchy(owner, projectileRoot))
            {
                projectileObject.transform.SetParent(projectileRoot, true);
            }

            SpriteRenderer projectileRenderer = projectileObject.AddComponent<SpriteRenderer>();
            RuntimeSpriteMaterialUtility.ApplySpriteMaterial(projectileRenderer);
            ApplySorting(projectileRenderer);
            if ((projectileAnimationFrames == null || projectileAnimationFrames.Length == 0) && projectileRenderer.sprite == null)
            {
                projectileRenderer.sprite = GetOrCreateFallbackSprite();
            }

            BerthaBossProjectile projectile = projectileObject.AddComponent<BerthaBossProjectile>();
            projectile.Configure(
                owner,
                projectileRenderer,
                normalizedDirection,
                targetLayerMask,
                obstacleLayerMask,
                speed,
                lifetime,
                damage,
                targetInvincibilityDuration,
                hitRadius,
                projectileMaximumHits,
                destroyProjectileOnHit,
                faceProjectileDirection,
                projectileAnimationFrames,
                projectileAnimationFrameRate,
                homingTarget,
                homingTurnSpeedDegrees,
                homingDuration);

            Log("Spawned projectile.");
        }

        private static bool IsInOwnerHierarchy(GameObject owner, Transform target)
        {
            if (owner == null || target == null)
            {
                return false;
            }

            return target == owner.transform || target.IsChildOf(owner.transform);
        }

        private void ApplySorting(SpriteRenderer projectileRenderer)
        {
            if (projectileRenderer == null)
            {
                return;
            }

            if (sortingReference != null)
            {
                projectileRenderer.sortingLayerID = sortingReference.sortingLayerID;
                projectileRenderer.sortingOrder = sortingReference.sortingOrder + sortingOrderOffset;
                return;
            }

            projectileRenderer.sortingOrder = sortingOrderOffset;
        }

        private static Vector2 AngleToDirection(float angleDegrees)
        {
            float angleRadians = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians));
        }

        private static Sprite GetOrCreateFallbackSprite()
        {
            if (_fallbackProjectileSprite != null)
            {
                return _fallbackProjectileSprite;
            }

            Texture2D texture = new Texture2D(8, 8, TextureFormat.RGBA32, false)
            {
                name = "BerthaProjectileFallbackTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[8 * 8];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            texture.SetPixels(pixels);
            texture.Apply();

            _fallbackProjectileSprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 8f);
            _fallbackProjectileSprite.name = "BerthaProjectileFallbackSprite";
            return _fallbackProjectileSprite;
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[BerthaProjectileBurstEmitter] " + message, this);
            }
        }
    }
}
