using UnityEngine;

namespace LostMemory.TestKhi
{
    internal static class KhiRuntimeVisualMaterialUtility
    {
        private static Material _spriteMaterial;
        private static Material _trailMaterial;

        public static void ApplySpriteMaterial(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            Material material = GetOrCreateSpriteMaterial();
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        public static void ApplyTrailMaterial(TrailRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            Material material = GetOrCreateTrailMaterial();
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static Material GetOrCreateSpriteMaterial()
        {
            if (_spriteMaterial != null)
            {
                return _spriteMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                return null;
            }

            _spriteMaterial = new Material(shader)
            {
                name = "KhiRuntimeSprite_Unlit",
                hideFlags = HideFlags.HideAndDontSave
            };

            return _spriteMaterial;
        }

        private static Material GetOrCreateTrailMaterial()
        {
            if (_trailMaterial != null)
            {
                return _trailMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                return null;
            }

            _trailMaterial = new Material(shader)
            {
                name = "KhiRuntimeTrail_Unlit",
                hideFlags = HideFlags.HideAndDontSave
            };

            return _trailMaterial;
        }
    }
}
