using System.Collections.Generic;
using System.Diagnostics;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Victory;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    public class RouteCalculatorTests
    {
        [Test]
        public void ContinuousRoadOfLengthN_WithoutEnemyBuildings_EqualsN()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            PlaceAlongHexCorners(board, PlayerId.Human, new HexCoord(0, 0), 0, 5);

            Assert.AreEqual(5, RouteCalculator.LongestRoadLength(board, PlayerId.Human));
            Assert.AreEqual(0, RouteCalculator.LongestRoadLength(board, PlayerId.Ai));
        }

        [Test]
        public void EnemySettlementInMiddle_SplitsChain_ReportsLongerPieceNotSum()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            // 5 roads: v0-v1-v2-v3-v4-v5. Enemy at v2 → pieces of 2 and 3.
            var vertices = PlaceAlongHexCorners(board, PlayerId.Human, new HexCoord(0, 0), 0, 5);
            board.VertexBuildings[vertices[2]] = (BuildingType.Settlement, PlayerId.Ai);

            int longest = RouteCalculator.LongestRoadLength(board, PlayerId.Human);
            Assert.AreEqual(3, longest, "longer remaining piece is 3, not the unsplit sum 5");
            Assert.AreNotEqual(5, longest);
            Assert.AreNotEqual(2 + 3, longest);
        }

        [Test]
        public void OwnSettlement_DoesNotSplitOwnRoads()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var vertices = PlaceAlongHexCorners(board, PlayerId.Human, new HexCoord(0, 0), 0, 5);
            board.VertexBuildings[vertices[2]] = (BuildingType.Settlement, PlayerId.Human);
            board.VertexBuildings[vertices[4]] = (BuildingType.City, PlayerId.Human);

            Assert.AreEqual(5, RouteCalculator.LongestRoadLength(board, PlayerId.Human));
        }

        [Test]
        public void NoRoads_ReturnsZero()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            Assert.AreEqual(0, RouteCalculator.LongestRoadLength(board, PlayerId.Human));
            Assert.AreEqual(0, RouteCalculator.LongestRoadLength(board, PlayerId.Ai));
        }

        [Test]
        public void TwoDisjointChains_ReportsTheLongerOne()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            PlaceAlongHexCorners(board, PlayerId.Human, new HexCoord(1, 0), 0, 3);
            PlaceAlongHexCorners(board, PlayerId.Human, new HexCoord(-1, 0), 0, 5);

            Assert.AreEqual(5, RouteCalculator.LongestRoadLength(board, PlayerId.Human));
        }

        [Test]
        public void TwoDisjointChainsOfEqualLength_ReportsThatLength()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            PlaceAlongHexCorners(board, PlayerId.Human, new HexCoord(1, 0), 0, 3);
            PlaceAlongHexCorners(board, PlayerId.Human, new HexCoord(-1, 0), 0, 3);

            Assert.AreEqual(3, RouteCalculator.LongestRoadLength(board, PlayerId.Human));
        }

        [Timeout(1000)]
        [Test]
        public void VertexDistance_TerminatesWithBuildingsPresentOnRoadChain()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var vertices = PlaceAlongHexCorners(board, PlayerId.Human, new HexCoord(0, 0), 0, 4);
            board.VertexBuildings[vertices[0]] = (BuildingType.Settlement, PlayerId.Human);
            board.VertexBuildings[vertices[2]] = (BuildingType.Settlement, PlayerId.Ai);

            var sw = Stopwatch.StartNew();
            int dist = VertexGraph.VertexDistance(vertices[0], vertices[4]);
            sw.Stop();

            Assert.GreaterOrEqual(dist, 2);
            Assert.Less(dist, int.MaxValue);
            Assert.Less(sw.ElapsedMilliseconds, 1000);
            Assert.AreEqual(2, RouteCalculator.LongestRoadLength(board, PlayerId.Human));
        }

        /// <summary>
        /// Places <paramref name="roadCount"/> consecutive roads along one hex perimeter.
        /// Returns the roadCount+1 canonical vertices in chain order.
        /// </summary>
        private static List<Vertex> PlaceAlongHexCorners(
            BoardState board, PlayerId player, HexCoord hex, int startCorner, int roadCount)
        {
            var vertices = new List<Vertex>(roadCount + 1);
            for (int i = 0; i <= roadCount; i++)
                vertices.Add(VertexGraph.Canonicalize(new Vertex(hex, startCorner + i)));

            for (int i = 0; i < roadCount; i++)
            {
                var edge = VertexGraph.GetEdgeBetween(vertices[i], vertices[i + 1]);
                Assert.IsFalse(board.Roads.ContainsKey(edge), $"edge {i} already occupied");
                board.Roads[edge] = player;
            }

            return vertices;
        }
    }
}
