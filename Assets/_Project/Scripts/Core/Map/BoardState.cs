using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Hex;

namespace CatanRoguelike.Core.Map
{
    [Serializable]
    public sealed class BoardState
    {
        public Dictionary<HexCoord, HexTileData> Tiles { get; } = new();
        public Dictionary<HexMath.Edge, PlayerId> Roads { get; } = new();
        public Dictionary<HexMath.Vertex, (BuildingType type, PlayerId owner)> VertexBuildings { get; } = new();
        public HashSet<HexMath.Edge> DisabledRoads { get; } = new();

        public HexCoord? RobberTile { get; private set; }
        public int DayNumber { get; set; } = 1;

        public HexTileData GetTile(HexCoord coord) => Tiles[coord];

        public bool TryGetTile(HexCoord coord, out HexTileData tile) => Tiles.TryGetValue(coord, out tile);

        public void PlaceRobber(HexCoord coord)
        {
            if (RobberTile.HasValue && Tiles.TryGetValue(RobberTile.Value, out var old))
                old.HasRobber = false;

            RobberTile = coord;
            if (Tiles.TryGetValue(coord, out var tile))
                tile.HasRobber = true;
        }

        public IEnumerable<HexTileData> GetTilesForResource(ResourceType resource) =>
            Tiles.Values.Where(t => t.Resource == resource);

        public int CountBuildings(PlayerId player, BuildingType type)
        {
            return VertexBuildings.Values.Count(v => v.owner == player && v.type == type);
        }

        public int CountRoads(PlayerId player) =>
            Roads.Values.Count(o => o == player);
    }
}
