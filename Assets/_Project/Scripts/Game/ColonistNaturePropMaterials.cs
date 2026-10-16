using CatanRoguelike.Core;
using UnityEngine;

namespace CatanRoguelike.Game
{
    internal static class ColonistNaturePropMaterials
    {
        private static Material _treeBark;
        private static Material _treeLeaves;
        private static Material _rock;
        private static Material _flower;
        private static Material _grass;

        public static void ApplyToRenderer(Renderer renderer, ResourceType resource)
        {
            if (renderer == null)
                return;

            string name = renderer.gameObject.name.ToLowerInvariant();
            Material mat = resource switch
            {
                ResourceType.Wood when name.Contains("leave") => TreeLeaves(),
                ResourceType.Wood => TreeBark(),
                ResourceType.Stone => Rock(),
                ResourceType.Wheat when name.Contains("flower") || name.Contains("petal") => Flower(),
                ResourceType.Wheat => Grass(),
                _ => null
            };

            if (mat != null)
                renderer.sharedMaterial = mat;
        }

        private static Material TreeBark() =>
            _treeBark ??= OpaqueColor(new Color(0.42f, 0.28f, 0.16f));

        private static Material TreeLeaves() =>
            _treeLeaves ??= OpaqueColor(new Color(0.22f, 0.58f, 0.26f));

        private static Material Rock() =>
            _rock ??= OpaqueColor(new Color(0.55f, 0.57f, 0.60f));

        private static Material Flower() =>
            _flower ??= OpaqueColor(new Color(0.92f, 0.78f, 0.22f));

        private static Material Grass() =>
            _grass ??= OpaqueColor(new Color(0.40f, 0.72f, 0.30f));

        private static Material OpaqueColor(Color color)
        {
            var tex = LoadTex("Art/Models/Nature/Textures/NormalTree_Leaves");
            if (tex != null)
                return BuiltInMaterials.Create(tex, color);
            return BuiltInMaterials.CreateOpaque(color);
        }

        private static Texture2D LoadTex(string path) => Resources.Load<Texture2D>(path);
    }
}
