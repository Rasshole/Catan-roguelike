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

        public static Material CreateUnlit(Color color)
        {
            var shader = Shader.Find("Unlit/Color");
            if (shader == null)
                return Create(color);

            var mat = new Material(shader) { color = color };
            mat.renderQueue = 3500;
            return mat;
        }

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
