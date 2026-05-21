using LostMemory.Rendering;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Combat
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Enemy Death Visual Cleanup")]
    public sealed class EnemyDeathVisualCleanup : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private bool applyRuntimeSpriteMaterialOnEnable;
        [SerializeField] private bool applyRuntimeSpriteMaterialOnDeath = true;
        [SerializeField] private SpriteRenderer[] renderersToHideOnDeath = System.Array.Empty<SpriteRenderer>();

        private bool _deathHandled;
        private bool[] _initialRendererEnabledStates;

        private void Reset()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            CacheRendererStates();
            RestoreConfiguredRenderers();
            _deathHandled = false;

            if (applyRuntimeSpriteMaterialOnEnable)
            {
                ApplyRuntimeSpriteMaterials();
            }

            if (health != null)
            {
                health.OnDeath += HandleDeath;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.OnDeath -= HandleDeath;
            }
        }

        private void ResolveReferences()
        {
            health ??= GetComponent<Health>();
        }

        private void HandleDeath()
        {
            if (_deathHandled)
            {
                return;
            }

            _deathHandled = true;

            if (applyRuntimeSpriteMaterialOnDeath)
            {
                ApplyRuntimeSpriteMaterials();
            }

            HideConfiguredRenderers();
        }

        private void ApplyRuntimeSpriteMaterials()
        {
            Transform root = visualRoot != null ? visualRoot : transform;
            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
            {
                RuntimeSpriteMaterialUtility.ApplySpriteMaterial(renderers[i]);
            }
        }

        private void HideConfiguredRenderers()
        {
            if (renderersToHideOnDeath == null)
            {
                return;
            }

            for (int i = 0; i < renderersToHideOnDeath.Length; i++)
            {
                SpriteRenderer renderer = renderersToHideOnDeath[i];
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }
        }

        private void CacheRendererStates()
        {
            if (renderersToHideOnDeath == null)
            {
                _initialRendererEnabledStates = System.Array.Empty<bool>();
                return;
            }

            if (_initialRendererEnabledStates != null
                && _initialRendererEnabledStates.Length == renderersToHideOnDeath.Length)
            {
                return;
            }

            _initialRendererEnabledStates = new bool[renderersToHideOnDeath.Length];
            for (int i = 0; i < renderersToHideOnDeath.Length; i++)
            {
                SpriteRenderer renderer = renderersToHideOnDeath[i];
                _initialRendererEnabledStates[i] = renderer == null || renderer.enabled;
            }
        }

        private void RestoreConfiguredRenderers()
        {
            if (renderersToHideOnDeath == null || _initialRendererEnabledStates == null)
            {
                return;
            }

            int count = Mathf.Min(renderersToHideOnDeath.Length, _initialRendererEnabledStates.Length);
            for (int i = 0; i < count; i++)
            {
                SpriteRenderer renderer = renderersToHideOnDeath[i];
                if (renderer != null)
                {
                    renderer.enabled = _initialRendererEnabledStates[i];
                }
            }
        }
    }
}
