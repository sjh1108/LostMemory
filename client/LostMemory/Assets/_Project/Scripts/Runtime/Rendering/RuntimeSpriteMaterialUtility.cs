using UnityEngine;

namespace LostMemory.Rendering
{
    internal static class RuntimeSpriteMaterialUtility
    {
        private static Material _spriteMaterial;
        private static Material _additiveSpriteMaterial;

        public static void ApplySpriteMaterial(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            Material material = GetSpriteMaterial();
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        public static Material GetSpriteMaterial()
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
                name = "RuntimeSprite_Unlit",
                hideFlags = HideFlags.HideAndDontSave
            };

            return _spriteMaterial;
        }

        public static void ApplyAdditiveSpriteMaterial(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            Material material = GetAdditiveSpriteMaterial();
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        public static Material GetAdditiveSpriteMaterial()
        {
            if (_additiveSpriteMaterial != null)
            {
                return _additiveSpriteMaterial;
            }

            Shader shader = Shader.Find("LostMemory/Sprites/Additive Glow");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                return null;
            }

            _additiveSpriteMaterial = new Material(shader)
            {
                name = "RuntimeSprite_AdditiveGlow",
                hideFlags = HideFlags.HideAndDontSave
            };

            return _additiveSpriteMaterial;
        }
    }
}
