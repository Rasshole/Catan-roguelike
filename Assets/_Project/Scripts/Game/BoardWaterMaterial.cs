using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// Dark sea material for the runtime <see cref="BoardView"/> water ring around the wood table disk.
    /// </summary>
    public static class BoardWaterMaterial
    {
        public static readonly Color SeaAlbedo = new(0.06f, 0.12f, 0.20f);
        public const float Smoothness = 0.18f;
        public const float Metallic = 0f;

        public static Material Create()
        {
            var mat = new Material(BuiltInMaterials.Standard);
            mat.color = SeaAlbedo;
            mat.SetFloat("_Metallic", Metallic);
            mat.SetFloat("_Glossiness", Smoothness);
            return mat;
        }
    }
}
