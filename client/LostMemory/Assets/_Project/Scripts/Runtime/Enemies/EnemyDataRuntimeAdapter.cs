using System;
using System.Collections;
using System.Collections.Generic;
using LostMemory.Combat.Telegraph;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Enemy Data Runtime Adapter")]
    public class EnemyDataRuntimeAdapter : MonoBehaviour
    {
        private const string ChargeDamageAreaName = "ChargeDamageArea";
        private const int DeferredApplyFrameCount = 3;
        private static readonly HashSet<EnemyDataRuntimeAdapter> ActiveAdapters = new HashSet<EnemyDataRuntimeAdapter>();

        [SerializeField] private EnemyData data;
        private Coroutine _deferredApplyRoutine;

        public static EnemyDataRuntimeAdapter Resolve(GameObject instance)
        {
            if (instance == null)
            {
                return null;
            }

            EnemyDataRuntimeAdapter adapter = instance.GetComponent<EnemyDataRuntimeAdapter>();
            if (adapter == null)
            {
                adapter = instance.AddComponent<EnemyDataRuntimeAdapter>();
            }

            return adapter;
        }

        public static void ApplyTo(GameObject instance, EnemyData data)
        {
            if (instance == null || data == null)
            {
                return;
            }

            EnemyDataRuntimeAdapter adapter = Resolve(instance);
            adapter?.Apply(data);
        }

        public static void ApplySavedAsset(EnemyData savedAsset)
        {
            if (savedAsset == null || ActiveAdapters.Count == 0)
            {
                return;
            }

            List<EnemyDataRuntimeAdapter> adapters = new List<EnemyDataRuntimeAdapter>(ActiveAdapters);
            for (int i = 0; i < adapters.Count; i++)
            {
                EnemyDataRuntimeAdapter adapter = adapters[i];
                if (adapter == null || adapter.data != savedAsset)
                {
                    continue;
                }

                adapter.Apply(savedAsset);
            }
        }

        private void OnEnable()
        {
            ActiveAdapters.Add(this);
        }

        private void OnDisable()
        {
            ActiveAdapters.Remove(this);
        }

        public virtual void Apply(EnemyData data)
        {
            if (data == null)
            {
                return;
            }

            this.data = data;
            ApplyImmediate(data);

            if (_deferredApplyRoutine != null)
            {
                StopCoroutine(_deferredApplyRoutine);
            }

            _deferredApplyRoutine = StartCoroutine(ApplyDeferred(data));
        }

        protected virtual void ApplyImmediate(EnemyData data)
        {
            ApplyHealth(data.MaxHealth);
            ApplyMovement(data.MoveSpeed);
            ApplySlimeController(data);
            if (data.TryGetAttackDamage(EnemyAttackType.Charge, out float chargeDamage))
            {
                ApplyNamedDamageSource(ChargeDamageAreaName, chargeDamage);
            }

            if (data.TryGetAttackDamage(EnemyAttackType.Slam, out float slamDamage))
            {
                ApplyChobombSelfDestructDamage(slamDamage);
            }
        }

        private void ApplySlimeController(EnemyData data)
        {
            SlimeEnemyController slime = GetComponent<SlimeEnemyController>();
            if (slime == null)
            {
                return;
            }

            slime.ApplyRuntimeData(data);
        }

        protected virtual IEnumerator ApplyDeferred(EnemyData data)
        {
            for (int attempt = 0; attempt < DeferredApplyFrameCount; attempt++)
            {
                yield return null;

                if (data == null)
                {
                    _deferredApplyRoutine = null;
                    yield break;
                }

                bool hasMeleeDamage = data.TryGetAttackDamage(EnemyAttackType.Melee, out float meleeDamage);
                if (hasMeleeDamage)
                {
                    ApplyMeleeDamage(meleeDamage);
                }

                if (data.TryGetAttackDamage(EnemyAttackType.Projectile, out float projectileDamage))
                {
                    ApplyProjectileDamage(projectileDamage);
                }

                bool hasSlamDamage = data.TryGetAttackDamage(EnemyAttackType.Slam, out float slamDamage);
                if (hasMeleeDamage || hasSlamDamage)
                {
                    float primaryTelegraphDamage = hasMeleeDamage ? meleeDamage : slamDamage;
                    float followUpTelegraphDamage = hasSlamDamage ? slamDamage : primaryTelegraphDamage;
                    ApplyTelegraphedDamage(primaryTelegraphDamage, followUpTelegraphDamage);
                }
            }

            _deferredApplyRoutine = null;
        }

        private void ApplyHealth(float maxHealth)
        {
            Health health = GetComponent<Health>();
            if (health == null)
            {
                return;
            }

            float resolvedMaxHealth = Mathf.Max(0f, maxHealth);
            health.InitialHealth = resolvedMaxHealth;
            health.MaximumHealth = resolvedMaxHealth;
            health.SetHealth(resolvedMaxHealth);
        }

        private void ApplyMovement(float moveSpeed)
        {
            CharacterMovement movement = GetComponent<CharacterMovement>();
            if (movement == null)
            {
                return;
            }

            float resolvedMoveSpeed = Mathf.Max(0f, moveSpeed);
            movement.WalkSpeed = resolvedMoveSpeed;
            movement.MovementSpeed = resolvedMoveSpeed;
        }

        private void ApplyNamedDamageSource(string sourceName, float damage)
        {
            DamageOnTouch[] damageSources = GetComponentsInChildren<DamageOnTouch>(includeInactive: true);
            if (damageSources == null || damageSources.Length == 0)
            {
                return;
            }

            float resolvedDamage = Mathf.Max(0f, damage);
            for (int i = 0; i < damageSources.Length; i++)
            {
                DamageOnTouch damageSource = damageSources[i];
                if (damageSource == null
                    || !string.Equals(damageSource.gameObject.name, sourceName, StringComparison.Ordinal))
                {
                    continue;
                }

                ApplyDamageOnTouchValue(damageSource, resolvedDamage);
            }
        }

        private void ApplyChobombSelfDestructDamage(float damage)
        {
            ChobombSelfDestructController[] controllers =
                GetComponentsInChildren<ChobombSelfDestructController>(includeInactive: true);
            if (controllers == null || controllers.Length == 0)
            {
                return;
            }

            float resolvedDamage = Mathf.Max(0f, damage);
            for (int i = 0; i < controllers.Length; i++)
            {
                ChobombSelfDestructController controller = controllers[i];
                if (controller == null)
                {
                    continue;
                }

                controller.SetExplosionDamage(resolvedDamage);
            }
        }

        private void ApplyMeleeDamage(float meleeDamage)
        {
            CharacterHandleWeapon[] handleWeapons = GetComponentsInChildren<CharacterHandleWeapon>(includeInactive: true);
            if (handleWeapons == null || handleWeapons.Length == 0)
            {
                return;
            }

            float resolvedMeleeDamage = Mathf.Max(0f, meleeDamage);
            for (int i = 0; i < handleWeapons.Length; i++)
            {
                CharacterHandleWeapon handleWeapon = handleWeapons[i];
                if (handleWeapon == null || handleWeapon.CurrentWeapon == null)
                {
                    continue;
                }

                MeleeWeapon meleeWeapon = handleWeapon.CurrentWeapon as MeleeWeapon;
                if (meleeWeapon == null)
                {
                    meleeWeapon = handleWeapon.CurrentWeapon.GetComponent<MeleeWeapon>();
                }

                if (meleeWeapon == null)
                {
                    continue;
                }

                meleeWeapon.MinDamageCaused = resolvedMeleeDamage;
                meleeWeapon.MaxDamageCaused = resolvedMeleeDamage;
                ApplyDamageOnTouchValues(meleeWeapon.gameObject, resolvedMeleeDamage);
            }
        }

        private void ApplyProjectileDamage(float projectileDamage)
        {
            CharacterHandleWeapon[] handleWeapons = GetComponentsInChildren<CharacterHandleWeapon>(includeInactive: true);
            if (handleWeapons == null || handleWeapons.Length == 0)
            {
                return;
            }

            float resolvedProjectileDamage = Mathf.Max(0f, projectileDamage);
            for (int i = 0; i < handleWeapons.Length; i++)
            {
                CharacterHandleWeapon handleWeapon = handleWeapons[i];
                if (handleWeapon == null || handleWeapon.CurrentWeapon == null)
                {
                    continue;
                }

                ProjectileWeapon projectileWeapon = handleWeapon.CurrentWeapon as ProjectileWeapon;
                if (projectileWeapon == null)
                {
                    projectileWeapon = handleWeapon.CurrentWeapon.GetComponent<ProjectileWeapon>();
                }

                if (projectileWeapon == null)
                {
                    continue;
                }

                ApplyProjectilePoolDamage(projectileWeapon, resolvedProjectileDamage);
            }
        }

        private static void ApplyProjectilePoolDamage(ProjectileWeapon projectileWeapon, float projectileDamage)
        {
            if (projectileWeapon == null)
            {
                return;
            }

            MMSimpleObjectPooler simplePooler = projectileWeapon.ObjectPooler as MMSimpleObjectPooler;
            if (simplePooler == null)
            {
                simplePooler = projectileWeapon.GetComponent<MMSimpleObjectPooler>();
            }

            if (simplePooler == null)
            {
                return;
            }

            if (simplePooler.GameObjectToPool != null && !simplePooler.GameObjectToPool.scene.IsValid())
            {
                GameObject runtimeProjectileTemplate = MMGameObjectExtensions.MMInstantiateDisabled(
                    simplePooler.GameObjectToPool,
                    projectileWeapon.transform,
                    false);
                runtimeProjectileTemplate.name = simplePooler.GameObjectToPool.name;
                simplePooler.GameObjectToPool = runtimeProjectileTemplate;
            }

            if (simplePooler.GameObjectToPool != null)
            {
                ApplyDamageOnTouchValues(simplePooler.GameObjectToPool, projectileDamage);
            }

            string poolName = simplePooler.GameObjectToPool != null
                ? "[SimpleObjectPooler] " + simplePooler.GameObjectToPool.name
                : string.Empty;
            MMObjectPool objectPool = !string.IsNullOrEmpty(poolName)
                ? simplePooler.ExistingPool(poolName)
                : null;

            if (objectPool?.PooledGameObjects == null)
            {
                return;
            }

            for (int i = 0; i < objectPool.PooledGameObjects.Count; i++)
            {
                ApplyDamageOnTouchValues(objectPool.PooledGameObjects[i], projectileDamage);
            }
        }

        private void ApplyTelegraphedDamage(float meleeDamage, float slamDamage)
        {
            TelegraphedAreaAttackController[] controllers =
                GetComponentsInChildren<TelegraphedAreaAttackController>(includeInactive: true);
            if (controllers == null || controllers.Length == 0)
            {
                return;
            }

            float resolvedMeleeDamage = Mathf.Max(0f, meleeDamage);
            float resolvedSlamDamage = slamDamage > 0f
                ? Mathf.Max(0f, slamDamage)
                : resolvedMeleeDamage;

            for (int i = 0; i < controllers.Length; i++)
            {
                TelegraphedAreaAttackController controller = controllers[i];
                if (controller == null)
                {
                    continue;
                }

                controller.SetDamageValues(resolvedMeleeDamage, resolvedSlamDamage);
            }
        }

        private static void ApplyDamageOnTouchValues(GameObject root, float damage)
        {
            if (root == null)
            {
                return;
            }

            DamageOnTouch[] damageSources = root.GetComponentsInChildren<DamageOnTouch>(includeInactive: true);
            if (damageSources == null || damageSources.Length == 0)
            {
                return;
            }

            float resolvedDamage = Mathf.Max(0f, damage);
            for (int i = 0; i < damageSources.Length; i++)
            {
                ApplyDamageOnTouchValue(damageSources[i], resolvedDamage);
            }
        }

        private static void ApplyDamageOnTouchValue(DamageOnTouch damageSource, float damage)
        {
            if (damageSource == null)
            {
                return;
            }

            damageSource.MinDamageCaused = damage;
            damageSource.MaxDamageCaused = damage;
        }
    }
}
