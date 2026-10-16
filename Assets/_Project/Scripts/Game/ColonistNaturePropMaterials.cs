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
            _treeBark ??= BuiltInMaterials.Create(LoadTex("Art/Models/Nature/Textures/NormalTree_Bark"), new Color(0.55f, 0.38f, 0.22f));

        private static Material TreeLeaves() =>
            _treeLeaves ??= BuiltInMaterials.Create(LoadTex("Art/Models/Nature/Textures/NormalTree_Leaves"), new Color(0.32f, 0.72f, 0.34f));

        private static Material Rock() =>
            _rock ??= BuiltInMaterials.Create(LoadTex("Art/Models/Nature/Textures/Rocks"), new Color(0.62f, 0.64f, 0.68f));

        private static Material Flower() =>
            _flower ??= BuiltInMaterials.Create(LoadTex("Art/Models/Nature/Textures/Flowers"), new Color(0.95f, 0.82f, 0.28f));

        private static Material Grass() =>
            BuiltInMaterials.Create(LoadTex("Art/Models/Nature/Textures/Grass"), new Color(0.45f, 0.78f, 0.32f));

        private static Texture2D LoadTex(string path) => Resources.Load<Texture2D>(path);
    }
}
