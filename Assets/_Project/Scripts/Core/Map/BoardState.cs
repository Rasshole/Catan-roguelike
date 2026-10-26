using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Hex;

namespace CatanRoguelike.Core.Map
{
    [Serializable]
    public sealed class BoardState
    {
        /// <summary>Land hexes only (map size / act expansion counts use <see cref="Tiles"/>).</summary>
        public Dictionary<HexCoord, HexTileData> Tiles { get; } = new();

        /// <summary>Coastal sea fishing grounds (Fishermen); not counted in map hex size.</summary>
        public Dictionary<HexCoord, HexTileData> SeaFishGrounds { get; } = new();

        /// <summary>Sea-ring hex occupied by each port badge (canonical port vertex → sea coord).</summary>
        public Dictionary<HexMath.Vertex, HexCoord> PortVertexToSeaSlot { get; } = new();

        public Dictionary<HexMath.Edge, PlayerId> Roads { get; } = new();
        public Dictionary<HexMath.Vertex, (BuildingType type, PlayerId owner)> VertexBuildings { get; } = new();
        public HashSet<HexMath.Edge> DisabledRoads { get; } = new();

        /// <summary>
        /// v145 Seafarers: playable sea hexes (between and around islands). Empty on every classic map, which is
        /// what keeps ships and the pirate off them.
        /// </summary>
        public HashSet<HexCoord> SeaHexes { get; } = new();

        /// <summary>v145 Seafarers: land hexes where the setup settlements may go (the home island). Empty = anywhere.</summary>
        public HashSet<HexCoord> SetupLandHexes { get; } = new();

        /// <summary>v145 Seafarers: ships on sea edges (canonical edge → owner).</summary>
        public Dictionary<HexMath.Edge, PlayerId> Ships { get; } = new();

        /// <summary>v145 Seafarers: the pirate's sea hex (null = not on the board yet).</summary>
        public HexCoord? PirateHex { get; set; }

        /// <summary>v145: true when the board has playable sea, i.e. ships and the pirate are in play.</summary>
        public bool IsSeafarers => SeaHexes.Count > 0;

        public HexCoord? RobberTile { get; private set; }
        public int DayNumber { get; set; } = 1;

        public static bool IsSeaFishGround(HexTileData tile) => tile.IsFishTile && !tile.IsDesert;

        public int LandTileCount => Tiles.Count;

        public HexTileData GetTile(HexCoord coord) => Tiles[coord];

        public bool TryGetTile(HexCoord coord, out HexTileData tile) => Tiles.TryGetValue(coord, out tile);

        public bool TryGetHexData(HexCoord coord, out HexTileData tile)
        {
            if (Tiles.TryGetValue(coord, out tile))
                return true;
            return SeaFishGrounds.TryGetValue(coord, out tile);
        }

        public bool TryGetSeaSlotForPort(HexMath.Vertex portVertex, out HexCoord seaCoord)
        {
            portVertex = VertexGraph.Canonicalize(portVertex);
            return PortVertexToSeaSlot.TryGetValue(portVertex, out seaCoord);
        }

        public void PlaceRobber(HexCoord coord)
        {
            if (RobberTile.HasValue && Tiles.TryGetValue(RobberTile.Value, out var old))
                old.HasRobber = false;

            RobberTile = coord;
            if (Tiles.TryGetValue(coord, out var tile))
                tile.HasRobber = true;
        }

        public void ClearRobber()
        {
            if (RobberTile.HasValue && Tiles.TryGetValue(RobberTile.Value, out var old))
                old.HasRobber = false;
            RobberTile = null;
        }

        public IEnumerable<HexTileData> GetTilesForResource(ResourceType resource) =>
            Tiles.Values.Where(t => t.Resource == resource);

        public int CountBuildings(PlayerId player, BuildingType type)
        {
            return VertexBuildings.Values.Count(v => v.owner == player && v.type == type);
        }

        public int CountRoads(PlayerId player) =>
            Roads.Values.Count(o => o == player);

        public int CountShips(PlayerId player) =>
            Ships.Values.Count(o => o == player);
    }
}
