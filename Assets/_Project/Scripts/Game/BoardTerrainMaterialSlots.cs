using CatanRoguelike.Core;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Per-terrain material slots for painted board art (textures loaded via <see cref="ColonistBoardTextureLoader"/>).</summary>
    public static class BoardTerrainMaterialSlots
    {
        public static Material GetTileMaterial(HexTileData data)
        {
            if (data.IsFishTile && !data.IsDesert)
                return ColonistBoardTextureLoader.GetTerrainMaterial(BoardTerrainSlot.SeaLake);

            if (data.IsDesert)
                return ColonistBoardTextureLoader.GetTerrainMaterial(BoardTerrainSlot.Desert);

            return ColonistBoardTextureLoader.GetTerrainMaterial(ResourceToSlot(data.Resource));
        }

        /// <summary>Unique material per hex so production dimming cannot mutate shared terrain caches.</summary>
        public static Material GetTileMaterialInstance(HexTileData data) =>
            new Material(GetTileMaterial(data));

        public static BoardTerrainSlot ResourceToSlot(ResourceType resource) => resource switch
        {
            ResourceType.Wood => BoardTerrainSlot.Wood,
            ResourceType.Brick => BoardTerrainSlot.Brick,
            ResourceType.Wheat => BoardTerrainSlot.Wheat,
            ResourceType.Sheep => BoardTerrainSlot.Sheep,
            ResourceType.Stone => BoardTerrainSlot.Ore,
            _ => BoardTerrainSlot.Wheat
        };
    }

    public enum BoardTerrainSlot
    {
        Wood,
        Brick,
        Wheat,
        Sheep,
        Ore,
        Desert,
        SeaLake,
        CoastSand,
        FishShoal
    }
}
