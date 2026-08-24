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

        public HexTileData(HexCoord coord, ResourceType resource, bool isCoastal = false)
        {
            Coord = coord;
            Resource = resource;
            IsCoastal = isCoastal;
        }
    }
}
