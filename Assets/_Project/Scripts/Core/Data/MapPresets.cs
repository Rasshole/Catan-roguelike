using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
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
        /// Resource counts: 4 wood, 4 wheat, 4 sheep, 3 brick, 3 ore, 1 desert (center).
        /// </summary>
        public static IReadOnlyList<(HexCoord coord, ResourceType resource)> LargeNineteenHex()
        {
            var resources = new[]
            {
                ResourceType.Stone,  // center — desert (no production); robber starts here
                ResourceType.Wood, ResourceType.Wheat, ResourceType.Sheep,
                ResourceType.Brick, ResourceType.Wood, ResourceType.Wheat,
                ResourceType.Sheep, ResourceType.Brick, ResourceType.Wood,
                ResourceType.Wheat, ResourceType.Stone, ResourceType.Sheep,
                ResourceType.Brick, ResourceType.Wood, ResourceType.Wheat,
                ResourceType.Sheep, ResourceType.Stone, ResourceType.Brick
            };

            return BuildFromSpiral(2, resources);
        }

        /// <summary>30-hex extended board (radius 3 minus 7 outer tips) — 5–6 player shape.</summary>
        public static IEnumerable<HexCoord> ExtendedThirtyHexCoords()
        {
            var center = new HexCoord(0, 0);
            var full = HexMath.Spiral(center, 3).ToList();
            var set = full.ToHashSet();
            var drop = new HashSet<HexCoord>();

            foreach (var coord in full)
            {
                if (HexMath.Distance(center, coord) != 3)
                    continue;
                if (CountBoardNeighbors(set, coord) <= 2)
                    drop.Add(coord);
            }

            if (full.Count - drop.Count > 30)
            {
                var extra = full
                    .Where(c => HexMath.Distance(center, c) == 3 && !drop.Contains(c))
                    .Where(c => CountBoardNeighbors(set, c) == 3)
                    .OrderByDescending(c => c.Q + c.R)
                    .ThenByDescending(c => c.Q)
                    .First();
                drop.Add(extra);
            }

            foreach (var coord in full)
            {
                if (!drop.Contains(coord))
                    yield return coord;
            }
        }

        /// <summary>37-hex full radius-3 hexagon.</summary>
        public static IEnumerable<HexCoord> HugeThirtySevenHexCoords() =>
            HexMath.Spiral(new HexCoord(0, 0), 3);

        public static IReadOnlyList<(HexCoord coord, ResourceType resource)> ExtendedThirtyHex()
        {
            var coords = ExtendedThirtyHexCoords().ToList();
            return BuildFromCoords(coords, BuildResourcesForLandCoords(coords, new HexCoord(0, 0)));
        }

        public static IReadOnlyList<(HexCoord coord, ResourceType resource)> HugeThirtySevenHex()
        {
            var coords = HugeThirtySevenHexCoords().ToList();
            return BuildFromCoords(coords, BuildResourcesForLandCoords(coords, new HexCoord(0, 0)));
        }

        private static int CountBoardNeighbors(HashSet<HexCoord> set, HexCoord coord)
        {
            int neighbors = 0;
            foreach (var dir in HexCoord.Directions)
            {
                if (set.Contains(coord + dir))
                    neighbors++;
            }

            return neighbors;
        }

        private static ResourceType[] BuildResourcesForLandCoords(
            IReadOnlyList<HexCoord> coords, HexCoord desertCoord)
        {
            int productive = coords.Count - 1;
            var multiset = BuildProductiveResourceMultiset(productive);
            var resources = new ResourceType[coords.Count];
            int pi = 0;
            for (int i = 0; i < coords.Count; i++)
            {
                if (coords[i].Equals(desertCoord))
                    resources[i] = ResourceType.Stone;
                else
                    resources[i] = multiset[pi++];
            }

            return resources;
        }

        /// <summary>Classic ratios for productive land hexes (excludes desert).</summary>
        private static ResourceType[] BuildProductiveResourceMultiset(int productiveCount)
        {
            int wood = RoundRatio(productiveCount, 4, 18);
            int sheep = RoundRatio(productiveCount, 4, 18);
            int wheat = RoundRatio(productiveCount, 4, 18);
            int brick = RoundRatio(productiveCount, 3, 18);
            int stone = productiveCount - wood - sheep - wheat - brick;
            if (stone < 0)
                stone = 0;

            var list = new List<ResourceType>(productiveCount);
            AddMany(list, ResourceType.Wood, wood);
            AddMany(list, ResourceType.Sheep, sheep);
            AddMany(list, ResourceType.Wheat, wheat);
            AddMany(list, ResourceType.Brick, brick);
            AddMany(list, ResourceType.Stone, stone);

            while (list.Count < productiveCount)
                list.Add(ResourceType.Wheat);
            while (list.Count > productiveCount)
                list.RemoveAt(list.Count - 1);

            return list.ToArray();
        }

        private static int RoundRatio(int total, int part, int whole) =>
            (int)System.Math.Round(total * (double)part / whole);

        private static void AddMany(List<ResourceType> list, ResourceType type, int count)
        {
            for (int i = 0; i < count; i++)
                list.Add(type);
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
            MapSize.Small => "Tutorial — 7 hex",
            MapSize.Medium => "Compact — 13 hex",
            MapSize.Large => "Base — 19 hex (3–4 players)",
            MapSize.Extended => "Extended — 30 hex (5–6 players)",
            MapSize.Huge => "Huge — 37 hex (7–8+ players)",
            _ => size.ToString()
        };

        public static string GetDescription(MapSize size) => size switch
        {
            MapSize.Small => "Hurtig tutorial — lille blomst.",
            MapSize.Medium => "19-hex form uden de 6 yderhjørner — flade kanter som mini-Catan.",
            MapSize.Large => "Standard Catan: 18 tokens, 9 ports, desert center.",
            MapSize.Extended => "Colonist extended table — 30 land hexes.",
            MapSize.Huge => "Full radius-3 island — largest standard hexagon.",
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
            var preset = GetLandPreset(targetSize);

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

            ApplyDesertCenter(board, targetSize);

            NumberTokenLibrary.AssignMissingTokens(board, seed);

            return added;
        }

        public static BoardState CreateBoard(
            MapSize size = MapSize.Small,
            int? seed = null,
            MapGenerationOptions options = default)
        {
            var preset = GetLandPreset(size);

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

            ApplyDesertCenter(board, size);

            NumberTokenLibrary.AssignMissingTokens(board, seed);

            if (options.FishermenOfCatan)
                AppendFishermenFishTiles(board, seed);

            return board;
        }

        private static IReadOnlyList<(HexCoord coord, ResourceType resource)> GetLandPreset(MapSize size) =>
            size switch
            {
                MapSize.Medium => MediumThirteenHex(),
                MapSize.Large => LargeNineteenHex(),
                MapSize.Extended => ExtendedThirtyHex(),
                MapSize.Huge => HugeThirtySevenHex(),
                _ => SmallSevenHex()
            };

        private static void ApplyDesertCenter(BoardState board, MapSize size)
        {
            if (size != MapSize.Large && size != MapSize.Extended && size != MapSize.Huge)
                return;

            if (!board.TryGetTile(new HexCoord(0, 0), out var center))
                return;

            center.IsDesert = true;
            center.NumberToken = null;
        }

        /// <summary>Sea/lake fish hexes just outside the coast (not counted in map size).</summary>
        public static void AppendFishermenFishTiles(BoardState board, int? seed = null)
        {
            var land = new HashSet<HexCoord>(board.Tiles.Keys);
            var candidates = new List<HexCoord>();
            foreach (var coord in land)
            {
                for (int d = 0; d < 6; d++)
                {
                    var sea = HexMath.GetNeighbor(coord, d);
                    if (!land.Contains(sea) && !candidates.Contains(sea))
                        candidates.Add(sea);
                }
            }

            candidates.Sort((a, b) =>
            {
                int cmp = a.Q.CompareTo(b.Q);
                return cmp != 0 ? cmp : a.R.CompareTo(b.R);
            });

            var rng = seed.HasValue ? new System.Random(seed.Value ^ 0xF15E) : new System.Random();
            int fishCount = System.Math.Min(6, candidates.Count);
            var values = new[] { 1, 1, 2, 2, 3, 0 };

            for (int i = 0; i < fishCount; i++)
            {
                int pick = rng.Next(candidates.Count);
                var coord = candidates[pick];
                candidates.RemoveAt(pick);

                var tile = new HexTileData(coord, ResourceType.Wheat, isCoastal: true)
                {
                    IsFishTile = true,
                    FishValue = values[i % values.Length]
                };
                board.Tiles[coord] = tile;
            }

            NumberTokenLibrary.AssignMissingTokens(board, seed);
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
