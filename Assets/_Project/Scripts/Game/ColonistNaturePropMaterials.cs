using CatanRoguelike.Core;
using UnityEngine;

namespace CatanRoguelike.Game
{
    internal static class ColonistNaturePropMaterials
    {
        private static Material _treeBark;
        private static Material _treeLeaves;
        private static Material _treeLeavesHighlight;
        private static Material _rock;
        private static Material _rockDark;
        private static Material _terracotta;
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
                ResourceType.Stone => RockDark(),
                ResourceType.Brick => Terracotta(),
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
                    ? (src.Contains("leaf") || src.Contains("leaves")
                        ? PickTreeLeafMaterial(renderer)
                        : src.Contains("bark") || src.Contains("trunk") || src.Contains("wood") ? TreeBark()
                        : mat)
                    : mat;
            }

            renderer.sharedMaterials = mats;
        }

        private static Material TreeBark() =>
            _treeBark ??= BuiltInMaterials.CreateMatteOpaque(new Color(0.42f, 0.28f, 0.16f));

        private static Material TreeLeaves() =>
            _treeLeaves ??= BuiltInMaterials.CreateMatteOpaque(new Color(0.118f, 0.353f, 0.180f));

        private static Material TreeLeavesHighlight() =>
            _treeLeavesHighlight ??= BuiltInMaterials.CreateMatteOpaque(new Color(0.184f, 0.420f, 0.227f));

        private static Material PickTreeLeafMaterial(Renderer renderer)
        {
            float y = renderer.bounds.center.y;
            return PositiveHash(renderer.GetInstanceID() + (int)(y * 100f)) % 3 == 0
                ? TreeLeavesHighlight()
                : TreeLeaves();
        }

        private static int PositiveHash(int value) => (value * 1103515245 + 12345) & 0x7fffffff;

        private static Material Rock() =>
            _rock ??= BuiltInMaterials.CreateMatteOpaque(new Color(0.55f, 0.57f, 0.60f));

        private static Material RockDark() =>
            _rockDark ??= BuiltInMaterials.CreateMatteOpaque(new Color(0.38f, 0.40f, 0.44f));

        private static Material Terracotta() =>
            _terracotta ??= BuiltInMaterials.CreateMatteOpaque(new Color(0.72f, 0.36f, 0.24f));

        private static Material Flower() =>
            _flower ??= BuiltInMaterials.CreateMatteOpaque(new Color(0.92f, 0.78f, 0.22f));

        private static Material Grass() =>
            _grass ??= BuiltInMaterials.CreateMatteOpaque(new Color(0.40f, 0.72f, 0.30f));
    }
}
