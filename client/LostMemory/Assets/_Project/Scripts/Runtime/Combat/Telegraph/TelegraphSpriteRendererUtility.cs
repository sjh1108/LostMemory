using UnityEngine;

namespace LostMemory.Combat.Telegraph
{
    internal static class TelegraphSpriteRendererUtility
    {
        private static Material _telegraphMaterial;

        public static void ApplyTelegraphMaterial(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            Material material = GetOrCreateTelegraphMaterial();
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static Material GetOrCreateTelegraphMaterial()
        {
            if (_telegraphMaterial != null)
            {
                return _telegraphMaterial;
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

            _telegraphMaterial = new Material(shader)
            {
                name = "AttackTelegraph_Unlit_Runtime",
                hideFlags = HideFlags.HideAndDontSave
            };

            return _telegraphMaterial;
        }
    }
}
