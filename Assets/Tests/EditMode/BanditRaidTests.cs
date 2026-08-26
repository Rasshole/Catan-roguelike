using System.Collections.Generic;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using NUnit.Framework;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    public class BanditRaidTests
    {
        [Test]
        public void ListOpponentRoads_ReturnsStableSortedOrder()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var hex = new HexCoord(0, 0);
            var edgeA = VertexGraph.GetEdgeBetween(
                VertexGraph.Canonicalize(new Vertex(hex, 0)),
                VertexGraph.Canonicalize(new Vertex(hex, 1)));
            var edgeB = VertexGraph.GetEdgeBetween(
                VertexGraph.Canonicalize(new Vertex(hex, 2)),
                VertexGraph.Canonicalize(new Vertex(hex, 3)));
            board.Roads[edgeB] = PlayerId.Ai;
            board.Roads[edgeA] = PlayerId.Ai;
            board.Roads[VertexGraph.GetEdgeBetween(
                VertexGraph.Canonicalize(new Vertex(hex, 4)),
                VertexGraph.Canonicalize(new Vertex(hex, 5)))] = PlayerId.Human;

            var roads = OpponentRoadSelector.ListOpponentRoads(board, PlayerId.Human);

            Assert.AreEqual(2, roads.Count);
            Assert.That(roads[0].ToString(), Is.LessThan(roads[1].ToString()));
            Assert.AreEqual(roads, OpponentRoadSelector.ListOpponentRoads(board, PlayerId.Human));
        }

        [Test]
        public void SelectRoad_ClampsIndexAndReturnsChosenEdge()
        {
            var roads = new List<Edge>
            {
                new(new Vertex(new HexCoord(0, 0), 0), new Vertex(new HexCoord(0, 0), 1)),
                new(new Vertex(new HexCoord(0, 0), 2), new Vertex(new HexCoord(0, 0), 3))
            };

            Assert.AreEqual(roads[0], OpponentRoadSelector.SelectRoad(roads, -1));
            Assert.AreEqual(roads[1], OpponentRoadSelector.SelectRoad(roads, 99));
            Assert.AreEqual(roads[1], OpponentRoadSelector.SelectRoad(roads, 1));
            Assert.IsNull(OpponentRoadSelector.SelectRoad(new List<Edge>(), 0));
        }

        [Test]
        public void ApplyBanditRaid_DisablesChosenEdge_NotAnotherRoad()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            PlaceAiRoadsAlongHex(board, new HexCoord(0, 0), 0, 2, out var edge0, out var edge1);

            var state = new GameState(board);
            state.PlayerHand.Add(CardId.BanditRaid);
            var engine = new CardEngine(42);

            Assert.IsTrue(engine.PlayCard(state, PlayerId.Human, CardId.BanditRaid, roadTarget: edge1));
            Assert.IsTrue(state.Board.DisabledRoads.Contains(edge1));
            Assert.IsFalse(state.Board.DisabledRoads.Contains(edge0));
            Assert.IsFalse(state.PlayerHand.Contains(CardId.BanditRaid));
        }

        [Test]
        public void ApplyBanditRaid_WithNoOpponentRoads_FailsCleanly()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var state = new GameState(board);
            state.PlayerHand.Add(CardId.BanditRaid);
            var engine = new CardEngine(42);

            Assert.IsFalse(engine.PlayCard(state, PlayerId.Human, CardId.BanditRaid, roadTarget: null));
            Assert.AreEqual(0, state.Board.DisabledRoads.Count);
            Assert.IsTrue(state.PlayerHand.Contains(CardId.BanditRaid));
        }

        private static void PlaceAiRoadsAlongHex(
            BoardState board, HexCoord hex, int startCorner, int roadCount,
            out Edge firstEdge, out Edge secondEdge)
        {
            firstEdge = default;
            secondEdge = default;

            for (int i = 0; i < roadCount; i++)
            {
                var v0 = VertexGraph.Canonicalize(new Vertex(hex, startCorner + i));
                var v1 = VertexGraph.Canonicalize(new Vertex(hex, startCorner + i + 1));
                var edge = VertexGraph.GetEdgeBetween(v0, v1);
                board.Roads[edge] = PlayerId.Ai;

                if (i == 0) firstEdge = edge;
                if (i == 1) secondEdge = edge;
            }
        }
    }
}
