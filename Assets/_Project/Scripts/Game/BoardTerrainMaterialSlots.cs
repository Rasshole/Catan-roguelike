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

        /// <summary>
        /// v132: deterministic per-tile colour variation for same-terrain painted tiles (brightness
        /// 0.93-1.05 and a slight warm/cool shift) so a board with three forests does not look stamped.
        /// Only the material colour changes: no rotation, flip or UV offset (v129 uniform orientation).
        /// </summary>
        public static Color PerTileTint(CatanRoguelike.Core.Hex.HexCoord coord)
        {
            uint h = (uint)(coord.Q * 73856093) ^ (uint)(coord.R * 19349663) ^ 0x9E3779B9u;
            h ^= h >> 13;
            h *= 0x5bd1e995u;
            h ^= h >> 15;
            float a = (h & 0xFFFF) / 65535f;
            float b = ((h >> 16) & 0xFFFF) / 65535f;
            float brightness = Mathf.Lerp(0.93f, 1.05f, a);
            float warm = Mathf.Lerp(-0.035f, 0.035f, b);
            return new Color(
                Mathf.Clamp01(brightness + warm),
                Mathf.Clamp01(brightness + warm * 0.3f),
                Mathf.Clamp01(brightness - warm),
                1f);
        }

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
