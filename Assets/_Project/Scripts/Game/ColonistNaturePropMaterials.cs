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
                ResourceType.Wood when name.Contains("bark") || name.Contains("trunk") => TreeBark(),
                ResourceType.Wood => TreeLeaves(),
                ResourceType.Stone => Rock(),
                ResourceType.Wheat when name.Contains("flower") || name.Contains("petal") => Flower(),
                ResourceType.Wheat => Grass(),
                _ => null
            };

            if (mat == null)
                return;

            // Quaternius FBX trees are one mesh with several sub-meshes (bark + leaves). Assigning
            // sharedMaterial only replaced slot 0, leaving the 0.8-grey "NormalTree_Leaves" slot that
            // rendered as white blobs. Style every slot by its source material name.
            var mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                string src = mats[i] != null ? mats[i].name.ToLowerInvariant() : string.Empty;
                mats[i] = resource == ResourceType.Wood
                    ? (src.Contains("leaf") || src.Contains("leaves") ? TreeLeaves()
                        : src.Contains("bark") || src.Contains("trunk") || src.Contains("wood") ? TreeBark()
                        : mat)
                    : mat;
            }

            renderer.sharedMaterials = mats;
        }

        private static Material TreeBark() =>
            _treeBark ??= BuiltInMaterials.CreateOpaque(new Color(0.42f, 0.28f, 0.16f));

        private static Material TreeLeaves() =>
            _treeLeaves ??= BuiltInMaterials.CreateOpaque(new Color(0.16f, 0.55f, 0.20f));

        private static Material Rock() =>
            _rock ??= BuiltInMaterials.CreateOpaque(new Color(0.55f, 0.57f, 0.60f));

        private static Material Flower() =>
            _flower ??= BuiltInMaterials.CreateOpaque(new Color(0.92f, 0.78f, 0.22f));

        private static Material Grass() =>
            _grass ??= BuiltInMaterials.CreateOpaque(new Color(0.40f, 0.72f, 0.30f));
    }
}
