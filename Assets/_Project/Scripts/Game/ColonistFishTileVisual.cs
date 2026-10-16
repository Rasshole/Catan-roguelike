using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Small fish icon on coastal fishing grounds (Fishermen of Catan).</summary>
    public static class ColonistFishTileVisual
    {
        private const float IconDiameter = 0.22f;

        public static void Apply(GameObject hexRoot, HexTileData data, float hexScale)
        {
            if (hexRoot == null || data == null || !data.IsFishTile || data.IsDesert)
                return;

            var existing = hexRoot.transform.Find("FishGroundIcon");
            if (existing != null)
                return;

            float local = IconDiameter / Mathf.Max(hexScale, 0.001f);
            var icon = ColonistFlatBoardQuad.Create(
                hexRoot.transform,
                "FishGroundIcon",
                new Vector3(0.35f, HexPrismMesh.TopY + 0.05f, -0.2f),
                local,
                local * 0.55f,
                ColonistResourceIconTextures.GetForPortBadge(Core.ResourceType.Wheat),
                3050,
                placeAbove: null,
                alignment: ColonistFlatBoardQuad.AlignmentMode.ColonistCamera);
            ColonistFlatBoardQuad.AlignToColonistCamera(icon);
        }
    }
}
