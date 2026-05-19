using UnityEngine;

namespace LostMemory.Rendering
{
    internal static class RuntimeSpriteMaterialUtility
    {
        private static Material _spriteMaterial;

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
    }
}
