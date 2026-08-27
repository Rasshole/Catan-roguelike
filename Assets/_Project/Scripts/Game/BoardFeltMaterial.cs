using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// Matte felt material for the runtime <see cref="BoardView"/> tablecloth disk under the sea ring.
    /// </summary>
    public static class BoardFeltMaterial
    {
        public static readonly Color FeltAlbedo = new(0.16f, 0.28f, 0.18f);
        public const float Smoothness = 0.08f;
        public const float Metallic = 0f;

        public static Material Create()
        {
            var mat = new Material(BuiltInMaterials.Standard);
            mat.color = FeltAlbedo;
            mat.SetFloat("_Metallic", Metallic);
            mat.SetFloat("_Glossiness", Smoothness);
            return mat;
        }
    }
}
