using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Yield;

namespace CatanRoguelike.Core.Data
{
    public static class MapPresets
    {
        /// <summary>7-hex flower — tutorial / fast runs.</summary>
        public static IReadOnlyList<(HexCoord coord, ResourceType resource)> SmallSevenHex()
        {
            return new List<(HexCoord, ResourceType)>
            {
                (new HexCoord(0, 0), ResourceType.Wood),
                (new HexCoord(1, 0), ResourceType.Brick),
                (new HexCoord(1, -1), ResourceType.Wheat),
                (new HexCoord(0, -1), ResourceType.Sheep),
                (new HexCoord(-1, 0), ResourceType.Stone),
                (new HexCoord(-1, 1), ResourceType.Wood),
                (new HexCoord(0, 1), ResourceType.Wheat),
            };
        }

        /// <summary>
        /// 13-hex board — center + ring 1 (6) + 6 alternating tiles from ring 2.
        /// Classic mid-size shape (not the full 19-hex Catan ring).
        /// </summary>
        public static IReadOnlyList<(HexCoord coord, ResourceType resource)> MediumThirteenHex()
        {
            var resources = new[]
            {
                ResourceType.Wood, ResourceType.Brick, ResourceType.Wheat,
                ResourceType.Sheep, ResourceType.Stone,
                ResourceType.Wood, ResourceType.Brick, ResourceType.Wheat,
                ResourceType.Sheep, ResourceType.Stone,
                ResourceType.Wheat, ResourceType.Sheep, ResourceType.Stone
            };

            return BuildFromCoords(ThirteenHexCoords(), resources);
        }

        /// <summary>
        /// 19-hex classic Catan shape (radius 2 ring + center).
        /// Resource counts: 4 wood, 4 wheat, 4 sheep, 4 brick, 3 stone (19 tiles).
        /// Center stone = desert/robber tile equivalent.
        /// </summary>
        public static IReadOnlyList<(HexCoord coord, ResourceType resource)> LargeNineteenHex()
        {
            var resources = new[]
            {
                ResourceType.Stone,  // center — robber starts here
                // ring 1 (clockwise from east)
                ResourceType.Wood, ResourceType.Wheat, ResourceType.Sheep,
                ResourceType.Brick, ResourceType.Wood, ResourceType.Wheat,
                // ring 2
                ResourceType.Sheep, ResourceType.Brick, ResourceType.Wood,
                ResourceType.Wheat, ResourceType.Stone, ResourceType.Sheep,
                ResourceType.Brick, ResourceType.Wood, ResourceType.Wheat,
                ResourceType.Sheep, ResourceType.Stone, ResourceType.Brick
            };

            return BuildFromSpiral(2, resources);
        }

        /// <summary>
        /// 13 tiles: classic compact Catan — full 19-hex radius-2 board minus the 6 outer corner tiles.
        /// Outer edge keeps flat sides (4 board neighbors); corners (3 board neighbors) are removed.
        /// </summary>
        public static IEnumerable<HexCoord> ThirteenHexCoords()
        {
            var center = new HexCoord(0, 0);
            var full = HexMath.Spiral(center, 2).ToList();
            var set = full.ToHashSet();

            foreach (var coord in full)
            {
                if (HexMath.Distance(center, coord) < 2)
                {
                    yield return coord;
                    continue;
                }

                int neighbors = 0;
                foreach (var dir in HexCoord.Directions)
                {
                    if (set.Contains(coord + dir))
                        neighbors++;
                }

                if (neighbors > 3)
                    yield return coord;
            }
        }

        public static string GetDisplayName(MapSize size) => size switch
        {
            MapSize.Small => "Small — 7 hex",
            MapSize.Medium => "Medium — 13 hex (kompakt Catan)",
            MapSize.Large => "Large — 19 hex (klassisk Catan)",
            _ => size.ToString()
        };

        public static string GetDescription(MapSize size) => size switch
        {
            MapSize.Small => "Hurtig tutorial — lille blomst.",
            MapSize.Medium => "19-hex form uden de 6 yderhjørner — flade kanter som mini-Catan.",
            MapSize.Large => "Fuld radius-2 hexagon — standard Catan-størrelse.",
            _ => ""
        };

        private static List<(HexCoord coord, ResourceType resource)> BuildFromSpiral(
            int radius, ResourceType[] resources)
        {
            var coords = HexMath.Spiral(new HexCoord(0, 0), radius).ToList();
            return BuildFromCoords(coords, resources);
        }

        private static List<(HexCoord coord, ResourceType resource)> BuildFromCoords(
            IEnumerable<HexCoord> coords, ResourceType[] resources)
        {
            var result = new List<(HexCoord, ResourceType)>();
            int i = 0;
            foreach (var coord in coords)
            {
                if (i >= resources.Length)
                    throw new System.InvalidOperationException(
                        $"Map preset mismatch: {i + 1} coords but only {resources.Length} resources.");
                result.Add((coord, resources[i++]));
            }

            if (i != resources.Length)
                throw new System.InvalidOperationException(
                    $"Map preset mismatch: {resources.Length} resources but only {i} coords.");

            return result;
        }

        /// <summary>
        /// Adds missing hexes from the target preset onto a live board.
        /// Existing buildings, roads, and robber position are preserved.
        /// </summary>
        public static int ExpandBoard(BoardState board, MapSize targetSize, int? seed = null)
        {
            var preset = targetSize switch
            {
                MapSize.Medium => MediumThirteenHex(),
                MapSize.Large => LargeNineteenHex(),
                _ => SmallSevenHex()
            };

            int added = 0;
            foreach (var (coord, resource) in preset)
            {
                if (board.Tiles.ContainsKey(coord))
                    continue;

                board.Tiles[coord] = new HexTileData(coord, resource, isCoastal: false);
                added++;
            }

            if (added == 0)
                return 0;

            var allCoords = new HashSet<HexCoord>(board.Tiles.Keys);
            foreach (var coord in allCoords)
                board.Tiles[coord].IsCoastal = IsCoastalTile(coord, allCoords);

            if (targetSize == MapSize.Large && board.TryGetTile(new HexCoord(0, 0), out var center))
            {
                center.IsDesert = true;
                center.NumberToken = null;
            }

            NumberTokenLibrary.AssignMissingTokens(board, seed);

            return added;
        }

        public static BoardState CreateBoard(MapSize size = MapSize.Small, int? seed = null)
        {
            var preset = size switch
            {
                MapSize.Medium => MediumThirteenHex(),
                MapSize.Large => LargeNineteenHex(),
                _ => SmallSevenHex()
            };

            var board = new BoardState();
            var coords = new HashSet<HexCoord>();

            foreach (var (coord, resource) in preset)
            {
                coords.Add(coord);
                board.Tiles[coord] = new HexTileData(coord, resource, isCoastal: false);
            }

            foreach (var coord in coords)
            {
                board.Tiles[coord].IsCoastal = IsCoastalTile(coord, coords);
            }

            // Small: robber on outer wheat — center wood is too punishing for hybrid production.
            var robberStart = size == MapSize.Small
                ? new HexCoord(1, -1)
                : new HexCoord(0, 0);
            board.PlaceRobber(robberStart);

            if (size == MapSize.Large && board.TryGetTile(new HexCoord(0, 0), out var center))
                center.IsDesert = true;

            NumberTokenLibrary.AssignMissingTokens(board, seed);

            return board;
        }

        /// <summary>Coastal = has at least one missing neighbor (board edge → port vertex).</summary>
        public static bool IsCoastalTile(HexCoord coord, HashSet<HexCoord> allTiles)
        {
            foreach (var dir in HexCoord.Directions)
            {
                if (!allTiles.Contains(coord + dir))
                    return true;
            }
            return false;
        }

        public static int GetHexCount(MapSize size) => (int)size;
    }
}
