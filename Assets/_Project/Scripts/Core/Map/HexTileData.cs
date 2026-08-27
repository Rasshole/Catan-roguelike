using System;
using CatanRoguelike.Core.Hex;

namespace CatanRoguelike.Core.Map
{
    [Serializable]
    public sealed class HexTileData
    {
        public HexCoord Coord;
        public ResourceType Resource;
        public bool HasRobber;
        public BuildingType Building = BuildingType.None;
        public PlayerId? Owner;
        public int VertexIndex = -1;

        public bool IsEmpty => Building == BuildingType.None;
        public bool IsCoastal;
        /// <summary>Desert / no-production tile (classic Catan center). Yields nothing in setup bonus.</summary>
        public bool IsDesert;

        /// <summary>Classic Catan number token (2–12). Null = no token (desert or unassigned).</summary>
        public int? NumberToken;

        public HexTileData(HexCoord coord, ResourceType resource, bool isCoastal = false)
        {
            Coord = coord;
            Resource = resource;
            IsCoastal = isCoastal;
        }
    }
}
