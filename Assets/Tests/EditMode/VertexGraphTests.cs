using System.Collections.Generic;
using System.Diagnostics;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.Tests
{
    public class VertexGraphTests
    {
        [Test]
        public void Canonicalize_IsIdempotent_ForEveryBoardCorner()
        {
            var board = MapPresets.CreateBoard(MapSize.Large);
            foreach (var hex in board.Tiles.Keys)
            {
                for (int c = 0; c < 6; c++)
                {
                    var vertex = new Vertex(hex, c);
                    var once = VertexGraph.Canonicalize(vertex);
                    var twice = VertexGraph.Canonicalize(once);
                    Assert.AreEqual(once, twice, $"Canonicalize not idempotent for {vertex} -> {once} -> {twice}");
                }
            }
        }

        [Test]
        public void Canonicalize_EquivalentRepresentations_ShareTheSameCanonicalVertex()
        {
            var origin = new Vertex(new HexCoord(0, 0), 0);
            var once = VertexGraph.Canonicalize(origin);
            foreach (var hex in VertexGraph.GetHexesForVertex(origin))
            {
                for (int c = 0; c < 6; c++)
                {
                    var candidate = new Vertex(hex, c);
                    if (!SameHexSet(origin, candidate))
                        continue;
                    Assert.AreEqual(once, VertexGraph.Canonicalize(candidate));
                }
            }
        }

        [Timeout(1000)]
        [Test]
        public void VertexDistance_TerminatesOnBoardWithTwoSettlementsAndRoads()
        {
            var board = BoardWithTwoSettlementsAndRoads();
            var vertices = new List<Vertex>(board.VertexBuildings.Keys);
            Assert.AreEqual(2, vertices.Count);

            var sw = Stopwatch.StartNew();
            int dist = VertexGraph.VertexDistance(vertices[0], vertices[1]);
            sw.Stop();

            Assert.GreaterOrEqual(dist, 2);
            Assert.Less(dist, int.MaxValue);
            Assert.Less(sw.ElapsedMilliseconds, 1000);
        }

        [Timeout(1000)]
        [Test]
        public void VertexDistance_SameVertexDifferentRepresentations_IsZero()
        {
            var vertex = new Vertex(new HexCoord(0, 0), 1);
            var canon = VertexGraph.Canonicalize(vertex);
            Assert.AreEqual(0, VertexGraph.VertexDistance(vertex, canon));
        }

        [Timeout(1000)]
        [Test]
        public void GetValidSettlementSpots_ReturnsInBoundedTime_WithBuildingsPresent()
        {
            var board = BoardWithTwoSettlementsAndRoads();
            var validator = new PlacementValidator();

            var sw = Stopwatch.StartNew();
            int count = 0;
            foreach (var spot in validator.GetValidSettlementSpots(board, PlayerId.Human, setupPhase: true))
            {
                count++;
                Assert.IsTrue(validator.CanPlaceSettlement(board, spot, PlayerId.Human, setupPhase: true));
            }
            sw.Stop();

            Assert.Greater(count, 0);
            Assert.Less(sw.ElapsedMilliseconds, 1000);
        }

        [Timeout(1000)]
        [Test]
        public void CanPlaceSettlement_ReturnsInBoundedTime_WithBuildingsPresent()
        {
            var board = BoardWithTwoSettlementsAndRoads();
            var validator = new PlacementValidator();
            var occupied = default(Vertex);
            foreach (var key in board.VertexBuildings.Keys)
            {
                occupied = key;
                break;
            }

            var sw = Stopwatch.StartNew();
            bool onOccupied = validator.CanPlaceSettlement(board, occupied, PlayerId.Ai, setupPhase: true);
            bool onFarHex = validator.CanPlaceSettlement(
                board,
                VertexGraph.Canonicalize(new Vertex(new HexCoord(1, 0), 1)),
                PlayerId.Ai,
                setupPhase: true);
            sw.Stop();

            Assert.IsFalse(onOccupied);
            Assert.Less(sw.ElapsedMilliseconds, 1000);
            _ = onFarHex;
        }

        private static BoardState BoardWithTwoSettlementsAndRoads()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var v0 = VertexGraph.Canonicalize(new Vertex(new HexCoord(0, 0), 0));
            var v1 = VertexGraph.Canonicalize(new Vertex(new HexCoord(0, 0), 3));
            board.VertexBuildings[v0] = (BuildingType.Settlement, PlayerId.Human);
            board.VertexBuildings[v1] = (BuildingType.Settlement, PlayerId.Ai);

            Vertex? adj0 = null;
            foreach (var adj in VertexGraph.GetAdjacentVertices(v0))
            {
                adj0 = adj;
                break;
            }
            Vertex? adj1 = null;
            foreach (var adj in VertexGraph.GetAdjacentVertices(v1))
            {
                adj1 = adj;
                break;
            }

            Assert.IsTrue(adj0.HasValue && adj1.HasValue);
            board.Roads[new Edge(v0, adj0.Value)] = PlayerId.Human;
            board.Roads[new Edge(v1, adj1.Value)] = PlayerId.Ai;
            return board;
        }

        private static bool SameHexSet(Vertex a, Vertex b)
        {
            var ha = new HashSet<HexCoord>(VertexGraph.GetHexesForVertex(a));
            var hb = new HashSet<HexCoord>(VertexGraph.GetHexesForVertex(b));
            return ha.SetEquals(hb);
        }
    }
}
