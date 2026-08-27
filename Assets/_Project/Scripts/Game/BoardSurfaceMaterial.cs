using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// Matte wood material for the runtime <see cref="BoardView"/> table disk.
    /// </summary>
    public static class BoardSurfaceMaterial
    {
        public static readonly Color WoodAlbedo = new(0.45f, 0.32f, 0.18f);
        public const float Smoothness = 0.1f;
        public const float Metallic = 0f;

        public static Material Create()
        {
            var mat = new Material(BuiltInMaterials.Standard);
            mat.color = WoodAlbedo;
            mat.SetFloat("_Metallic", Metallic);
            mat.SetFloat("_Glossiness", Smoothness);
            return mat;
        }
    }
}
