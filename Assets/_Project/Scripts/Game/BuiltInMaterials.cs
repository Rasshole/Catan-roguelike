using UnityEngine;
using UnityEngine.Rendering;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// Built-in RP material helper. This project stays on Standard
    /// (see docs/DESIGN_RENDERING.md). Do not look up URP shaders.
    /// </summary>
    public static class BuiltInMaterials
    {
        public const string StandardShaderName = "Standard";
        public const string UnlitColorShaderName = "Unlit/Color";
        public const string UnlitTransparentShaderName = "Unlit/Transparent";

        public static Shader Standard
        {
            get
            {
                var shader = Shader.Find(StandardShaderName);
                if (shader == null)
                    throw new System.InvalidOperationException(
                        "Built-in Standard shader not found. Project uses Built-in RP.");
                return shader;
            }
        }

        public static Material Create(Color color)
        {
            var mat = new Material(Standard);
            mat.color = color;
            return mat;
        }

        /// <summary>Opaque Standard surface (token chips, board props).</summary>
        public static Material CreateOpaque(Color color)
        {
            var mat = Create(color);
            mat.SetFloat("_Mode", 0f);
            mat.SetInt("_SrcBlend", (int)BlendMode.One);
            mat.SetInt("_DstBlend", (int)BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Geometry;
            return mat;
        }

        public static Material CreateUnlit(Color color, bool zWrite = true)
        {
            var shader = Shader.Find(UnlitColorShaderName);
            if (shader == null)
                return Create(color);

            var mat = new Material(shader) { color = color };
            mat.SetInt("_ZWrite", zWrite ? 1 : 0);
            mat.renderQueue = zWrite ? 3500 : 3080;
            return mat;
        }

        public static Material CreateUnlitTransparent(Texture2D texture, bool zWrite = false)
        {
            var shader = Shader.Find(UnlitTransparentShaderName);
            if (shader == null)
                throw new System.InvalidOperationException("Unlit/Transparent shader not found.");

            var mat = new Material(shader);
            if (mat.HasProperty("_MainTex"))
                mat.mainTexture = texture;
            mat.SetInt("_ZWrite", zWrite ? 1 : 0);
            mat.renderQueue = zWrite ? 3000 : 3085;
            return mat;
        }

        public static bool TryGetMainTextureName(Material material, out string textureName)
        {
            textureName = "null";
            if (material == null || material.shader == null || !material.HasProperty("_MainTex"))
                return false;

            var tex = material.mainTexture;
            if (tex == null)
                return false;

            textureName = tex.name;
            return true;
        }

        public static bool IsUnlitMaterial(Material material) =>
            material != null
            && material.shader != null
            && material.shader.name.Contains("Unlit");

        /// <summary>Translucent Standard material for placement hover silhouettes.</summary>
        public static Material CreateGhost(Color color)
        {
            var mat = Create(color);
            mat.SetFloat("_Mode", 3f);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            return mat;
        }
    }
}
