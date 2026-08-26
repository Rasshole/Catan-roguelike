using UnityEngine;

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
    }
}
