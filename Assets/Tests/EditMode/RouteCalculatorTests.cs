using System.Collections.Generic;
using System.Diagnostics;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Victory;
using NUnit.Framework;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    public class RouteCalculatorTests
    {
        private static readonly HexCoord HumanRoadHex = new HexCoord(0, 0);
        private static readonly HexCoord AiRoadHex = new HexCoord(2, -2);

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

        [Test]
        public void DisabledRoad_InMiddleOfChain_SplitsAndExcludesFromLength()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var vertices = PlaceAlongHexCorners(board, PlayerId.Human, new HexCoord(0, 0), 0, 5);
            var middleEdge = VertexGraph.GetEdgeBetween(vertices[1], vertices[2]);

            Assert.AreEqual(5, RouteCalculator.LongestRoadLength(board, PlayerId.Human));

            board.DisabledRoads.Add(middleEdge);

            Assert.IsTrue(board.Roads.ContainsKey(middleEdge), "Bandit Raid keeps the road on the board");
            CollectionAssert.Contains(board.DisabledRoads, middleEdge);
            Assert.AreEqual(3, RouteCalculator.LongestRoadLength(board, PlayerId.Human),
                "disabled edge is omitted; longer remaining piece is 3, not the unsplit 5");
        }

        [Test]
        public void DisabledRoad_AtChainEnd_ReducesReportedLength()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var vertices = PlaceAlongHexCorners(board, PlayerId.Human, new HexCoord(0, 0), 0, 5);
            var tailEdge = VertexGraph.GetEdgeBetween(vertices[4], vertices[5]);

            board.DisabledRoads.Add(tailEdge);

            Assert.AreEqual(4, RouteCalculator.LongestRoadLength(board, PlayerId.Human));
        }

        [Test]
        public void HexLoop_SixRoads_CountsAllSixEdges()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var edges = PlaceHexPerimeter(board, PlayerId.Human, new HexCoord(0, 0), 0);

            Assert.AreEqual(6, edges.Count);
            Assert.AreEqual(6, RouteCalculator.LongestRoadLength(board, PlayerId.Human),
                "a closed hex loop counts every road in the cycle");
        }

        [Test]
        public void DisabledRoad_OnClosedLoop_TwoDisabledEdges_SplitsIntoShorterPieces()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var vertices = PlaceAlongHexCorners(board, PlayerId.Human, HumanRoadHex, 0, 6);
            board.DisabledRoads.Add(VertexGraph.GetEdgeBetween(vertices[2], vertices[3]));
            board.DisabledRoads.Add(VertexGraph.GetEdgeBetween(vertices[4], vertices[5]));

            Assert.AreEqual(3, RouteCalculator.LongestRoadLength(board, PlayerId.Human),
                "two disabled edges break the loop; longest remaining simple path is 3 (5-0-1-2)");
        }

        [Test]
        public void BranchingPath_ReportsLongestSimplePathNotSumOfBranches()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var hex = new HexCoord(0, 0);
            var a = VertexGraph.Canonicalize(new Vertex(hex, 0));
            var b = VertexGraph.Canonicalize(new Vertex(hex, 1));
            var c = VertexGraph.Canonicalize(new Vertex(hex, 2));
            var d = VertexGraph.Canonicalize(new Vertex(hex, 3));
            PlaceRoad(board, PlayerId.Human, a, b);
            PlaceRoad(board, PlayerId.Human, b, c);
            PlaceRoad(board, PlayerId.Human, c, d);

            Assert.AreEqual(3, RouteCalculator.LongestRoadLength(board, PlayerId.Human));
            Assert.AreNotEqual(4, RouteCalculator.LongestRoadLength(board, PlayerId.Human));
        }

        [Test]
        public void GetLongestRoadOwner_BothUnderMinimum_ReturnsNull()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            PlaceAlongHexCorners(board, PlayerId.Human, new HexCoord(0, 0), 0, 3);
            PlaceAlongHexCorners(board, PlayerId.Ai, AiRoadHex, 0, 3);

            Assert.IsNull(RouteCalculator.GetLongestRoadOwner(board));
        }

        [Test]
        public void GetLongestRoadOwner_TiedAtMinimumOrMore_ReturnsNull()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            PlaceAlongHexCorners(board, PlayerId.Human, HumanRoadHex, 0, 5);
            PlaceAlongHexCorners(board, PlayerId.Ai, AiRoadHex, 0, 5);

            Assert.AreEqual(5, RouteCalculator.LongestRoadLength(board, PlayerId.Human));
            Assert.AreEqual(5, RouteCalculator.LongestRoadLength(board, PlayerId.Ai));
            Assert.IsNull(RouteCalculator.GetLongestRoadOwner(board));
        }

        [Test]
        public void GetLongestRoadOwner_ClearWinner_ReturnsThatPlayer()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            PlaceAlongHexCorners(board, PlayerId.Human, HumanRoadHex, 0, 5);
            PlaceAlongHexCorners(board, PlayerId.Ai, AiRoadHex, 0, 4);

            Assert.AreEqual(PlayerId.Human, RouteCalculator.GetLongestRoadOwner(board));

            board = MapPresets.CreateBoard(MapSize.Small);
            PlaceAlongHexCorners(board, PlayerId.Human, HumanRoadHex, 0, 4);
            PlaceAlongHexCorners(board, PlayerId.Ai, AiRoadHex, 0, 5);

            Assert.AreEqual(PlayerId.Ai, RouteCalculator.GetLongestRoadOwner(board));
        }

        [Test]
        public void GetLongestRoadOwner_DisabledRoadDropsLeaderBelowThreshold_ReturnsNull()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var vertices = PlaceAlongHexCorners(board, PlayerId.Human, new HexCoord(0, 0), 0, 5);
            PlaceAlongHexCorners(board, PlayerId.Ai, AiRoadHex, 0, 4);

            Assert.AreEqual(PlayerId.Human, RouteCalculator.GetLongestRoadOwner(board));

            board.DisabledRoads.Add(VertexGraph.GetEdgeBetween(vertices[4], vertices[5]));

            Assert.AreEqual(4, RouteCalculator.LongestRoadLength(board, PlayerId.Human));
            Assert.IsNull(RouteCalculator.GetLongestRoadOwner(board));
        }

        [Test]
        public void GetLongestRoadOwner_DisabledRoadFlipsLeadership()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var humanVertices = PlaceAlongHexCorners(board, PlayerId.Human, HumanRoadHex, 0, 5);
            PlaceAlongHexCorners(board, PlayerId.Ai, AiRoadHex, 0, 5);

            Assert.IsNull(RouteCalculator.GetLongestRoadOwner(board));

            board.DisabledRoads.Add(VertexGraph.GetEdgeBetween(humanVertices[4], humanVertices[5]));

            Assert.AreEqual(PlayerId.Ai, RouteCalculator.GetLongestRoadOwner(board));
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

        private static List<Edge> PlaceHexPerimeter(
            BoardState board, PlayerId player, HexCoord hex, int startCorner)
        {
            var edges = new List<Edge>(6);
            for (int i = 0; i < 6; i++)
            {
                var v0 = VertexGraph.Canonicalize(new Vertex(hex, startCorner + i));
                var v1 = VertexGraph.Canonicalize(new Vertex(hex, startCorner + i + 1));
                var edge = VertexGraph.GetEdgeBetween(v0, v1);
                Assert.IsFalse(board.Roads.ContainsKey(edge), $"loop edge {i} already occupied");
                board.Roads[edge] = player;
                edges.Add(edge);
            }

            return edges;
        }

        private static void PlaceRoad(BoardState board, PlayerId player, Vertex a, Vertex b)
        {
            var edge = VertexGraph.GetEdgeBetween(a, b);
            Assert.IsFalse(board.Roads.ContainsKey(edge), "edge already occupied");
            board.Roads[edge] = player;
        }
    }
}
