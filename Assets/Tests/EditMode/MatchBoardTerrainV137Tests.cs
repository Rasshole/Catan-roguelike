using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Match;
using CatanRoguelike.Core.Yield;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    /// <summary>
    /// v137: the 30/37-hex islands used to lay the terrain multiset out in coordinate order (all ore on the left,
    /// forests on the right, brick in one column). Terrain is now shuffled per seed with same-type groups broken up.
    /// </summary>
    public class MatchBoardTerrainV137Tests
    {
        /// <summary>Same-terrain neighbour edges allowed per board edge (a shuffled table rarely has more).</summary>
        private const double MaxSamePairShare = 0.12;

        private const int MaxClusterAllowed = 3;

        private static List<HexTileData> Productive(BoardState board) =>
            board.Tiles.Values.Where(t => !t.IsDesert).ToList();

        private static (int samePairs, int edges, int largest) Clustering(BoardState board)
        {
            var tiles = Productive(board).ToDictionary(t => t.Coord);
            int same = 0, edges = 0, largest = 0;
            foreach (var t in tiles.Values)
                foreach (var d in HexCoord.Directions)
                    if (tiles.TryGetValue(t.Coord + d, out var n) && Order(t.Coord) < Order(n.Coord))
                    {
                        edges++;
                        if (n.Resource == t.Resource)
                            same++;
                    }

            var seen = new HashSet<HexCoord>();
            foreach (var t in tiles.Values)
            {
                if (!seen.Add(t.Coord))
                    continue;
                int size = 0;
                var stack = new Stack<HexTileData>();
                stack.Push(t);
                while (stack.Count > 0)
                {
                    var c = stack.Pop();
                    size++;
                    foreach (var d in HexCoord.Directions)
                        if (tiles.TryGetValue(c.Coord + d, out var n) && n.Resource == t.Resource && seen.Add(n.Coord))
                            stack.Push(n);
                }

                largest = System.Math.Max(largest, size);
            }

            return (same, edges, largest);
        }

        private static long Order(HexCoord c) => c.Q * 1000L + c.R;

        private static string Layout(BoardState board) =>
            string.Join(",", board.Tiles.Values.OrderBy(t => Order(t.Coord)).Select(t => t.IsDesert ? "D" : t.Resource.ToString()));

        private static void AssertSpread(BoardState board, string what)
        {
            var (same, edges, largest) = Clustering(board);
            TestContext.WriteLine($"{what}: {same}/{edges} same-terrain edges, largest group {largest}");
            Assert.LessOrEqual(same, (int)(edges * MaxSamePairShare), what + ": same-terrain edges");
            Assert.LessOrEqual(largest, MaxClusterAllowed, what + ": largest same-terrain group");
        }

        private static void AssertNoAdjacentRed(BoardState board, string what)
        {
            foreach (var t in board.Tiles.Values.Where(t => t.NumberToken is 6 or 8))
                foreach (var d in HexCoord.Directions)
                    if (board.TryGetTile(t.Coord + d, out var n))
                        Assert.IsFalse(n.NumberToken is 6 or 8, $"{what}: adjacent red tokens at {t.Coord}/{n.Coord}");
        }

        [Test]
        public void FivePlayerMatchBoards_AreShuffledPerSeed_AndNotClustered()
        {
            var layouts = new HashSet<string>();
            for (int seed = 1; seed <= 40; seed++)
            {
                var board = new MatchController(MatchConfig.Create(5, 2, seed, partyRules: true)).State.Board;
                Assert.AreEqual(30, board.Tiles.Count);
                Assert.IsTrue(board.Tiles[new HexCoord(0, 0)].IsDesert, "desert stays in the centre");
                AssertSpread(board, "5p seed " + seed);
                AssertNoAdjacentRed(board, "5p seed " + seed);
                layouts.Add(Layout(board));
            }

            Assert.GreaterOrEqual(layouts.Count, 38, "terrain differs per seed");
        }

        [Test]
        public void FourPlayerMatchBoards_AreShuffledPerSeed_AndKeepTheBaseMix()
        {
            var designed = MapPresets.CreateBoard(MapSize.Large, 7);
            var designedCounts = Productive(designed).GroupBy(t => t.Resource).ToDictionary(g => g.Key, g => g.Count());
            var layouts = new HashSet<string>();
            for (int seed = 1; seed <= 40; seed++)
            {
                var board = new MatchController(MatchConfig.Create(4, 1, seed)).State.Board;
                var counts = Productive(board).GroupBy(t => t.Resource).ToDictionary(g => g.Key, g => g.Count());
                CollectionAssert.AreEquivalent(designedCounts, counts, "4 wood/wheat/sheep, 3 brick/ore");
                AssertSpread(board, "4p seed " + seed);
                AssertNoAdjacentRed(board, "4p seed " + seed);
                layouts.Add(Layout(board));
            }

            Assert.GreaterOrEqual(layouts.Count, 38);
        }

        [Test]
        public void SameSeed_SameIsland()
        {
            var a = new MatchController(MatchConfig.Create(5, 1, 99)).State.Board;
            var b = new MatchController(MatchConfig.Create(5, 1, 99)).State.Board;
            Assert.AreEqual(Layout(a), Layout(b));
            Assert.AreEqual(string.Join(",", a.Tiles.Values.OrderBy(t => Order(t.Coord)).Select(t => t.NumberToken)),
                string.Join(",", b.Tiles.Values.OrderBy(t => Order(t.Coord)).Select(t => t.NumberToken)));
        }

        [Test]
        public void SinglePlayerMaps_AreNotClustered()
        {
            // The designed single-player maps keep their layout; the big islands (map pick or act expansion) are
            // shuffled per run seed now, so none of them groups terrain.
            foreach (var size in new[] { MapSize.Large, MapSize.Extended, MapSize.Huge })
                for (int seed = 1; seed <= 20; seed++)
                {
                    var board = MapPresets.CreateBoard(size, seed);
                    AssertSpread(board, $"{size} seed {seed}");
                    AssertNoAdjacentRed(board, $"{size} seed {seed}");
                }

            Assert.AreEqual(Layout(MapPresets.CreateBoard(MapSize.Large, 1)), Layout(MapPresets.CreateBoard(MapSize.Large, 2)),
                "the single-player base map keeps its designed layout");

            // Act expansion (19 → 30) adds spread-out terrain too.
            var grown = MapPresets.CreateBoard(MapSize.Large, 5);
            MapPresets.ExpandBoard(grown, MapSize.Extended, 5);
            var (_, _, largest) = Clustering(grown);
            Assert.LessOrEqual(largest, MaxClusterAllowed + 1, "expanded ring is not one band");
        }

        [Test]
        public void ThePreV137Layout_WouldFailTheThreshold()
        {
            // Guard the guard: the old coordinate-order fill (sorted multiset) is flagged by the same metric.
            var board = MapPresets.CreateBoard(MapSize.Extended, 1);
            var sorted = Productive(board).Select(t => t.Resource).OrderBy(r => r).ToList();
            int i = 0;
            foreach (var t in Productive(board).OrderBy(t => t.Coord.R).ThenBy(t => t.Coord.Q))
                t.Resource = sorted[i++];
            var (same, edges, largest) = Clustering(board);
            Assert.Greater(same, (int)(edges * MaxSamePairShare));
            Assert.Greater(largest, MaxClusterAllowed);
        }
    }
}
