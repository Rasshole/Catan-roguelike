using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Victory;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.Tests
{
    public class VictoryCalculatorTests
    {
        [Test]
        public void HarborCharter_BonusVp_SurvivesRefreshVictoryPoints()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var state = new GameState(board);
            PlaceBuilding(board, BuildingType.Settlement, PlayerId.Human);

            // GameController awards Harbor Charter via AddVictoryPoints, then refreshes.
            state.AddVictoryPoints(PlayerId.Human, 1);

            VictoryCalculator.RefreshVictoryPoints(state);
            Assert.AreEqual(1, state.PlayerBonusVictoryPoints);
            Assert.AreEqual(2, state.PlayerVictoryPoints, "1 settlement + Harbor Charter +1 VP");

            VictoryCalculator.RefreshVictoryPoints(state);
            Assert.AreEqual(2, state.PlayerVictoryPoints, "second refresh must not drop Harbor Charter");
            Assert.AreEqual(0, state.AiVictoryPoints);
        }

        [Test]
        public void FirstCityVp_BonusVp_SurvivesRefreshVictoryPoints()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var state = new GameState(board);
            state.AcquiredPerks.Add(LevelUpPerkId.FirstCityVp);
            PlaceBuilding(board, BuildingType.City, PlayerId.Human);

            // GameController awards FirstCityVp once at first city, then refreshes.
            state.FirstCityBuiltThisRun = true;
            state.AddVictoryPoints(PlayerId.Human, 1);

            VictoryCalculator.RefreshVictoryPoints(state);
            Assert.AreEqual(1, state.PlayerBonusVictoryPoints);
            Assert.AreEqual(3, state.PlayerVictoryPoints, "city 2 VP + FirstCityVp +1");

            VictoryCalculator.RefreshVictoryPoints(state);
            Assert.AreEqual(3, state.PlayerVictoryPoints, "second refresh must not drop FirstCityVp");
            Assert.AreEqual(0, state.AiVictoryPoints);
        }

        [Test]
        public void RefreshVictoryPoints_WithoutBonus_CountsOnlyBoardVp()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var state = new GameState(board);
            PlaceBuilding(board, BuildingType.Settlement, PlayerId.Human);
            PlaceBuilding(board, BuildingType.City, PlayerId.Ai);

            VictoryCalculator.RefreshVictoryPoints(state);

            Assert.AreEqual(0, state.PlayerBonusVictoryPoints);
            Assert.AreEqual(1, state.PlayerVictoryPoints);
            Assert.AreEqual(2, state.AiVictoryPoints);
        }

        [Test]
        public void LongRoadBonus_HasPerkAndLongest_AddsOneVp()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var state = new GameState(board);
            state.AcquiredPerks.Add(LevelUpPerkId.LongRoadBonus);
            PlaceRoadPath(board, PlayerId.Human, 5, new HexCoord(0, 0), 0);

            VictoryCalculator.RefreshVictoryPoints(state);

            Assert.AreEqual(PlayerId.Human, RouteCalculator.GetLongestRoadOwner(board));
            Assert.AreEqual(0, state.PlayerBonusVictoryPoints, "LongRoadBonus is recomputed, not sticky bonus VP");
            Assert.AreEqual(3, state.PlayerVictoryPoints, "2 VP longest route + 1 perk");
            Assert.AreEqual(0, state.AiVictoryPoints);

            VictoryCalculator.RefreshVictoryPoints(state);
            Assert.AreEqual(3, state.PlayerVictoryPoints, "second refresh must not drop or double LongRoadBonus");
        }

        [Test]
        public void LongRoadBonus_LosesLongest_BonusGone()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var state = new GameState(board);
            state.AcquiredPerks.Add(LevelUpPerkId.LongRoadBonus);
            PlaceRoadPath(board, PlayerId.Human, 5, new HexCoord(0, 0), 0);

            VictoryCalculator.RefreshVictoryPoints(state);
            Assert.AreEqual(3, state.PlayerVictoryPoints);

            PlaceRoadPath(board, PlayerId.Ai, 6, new HexCoord(2, -2), 0);

            VictoryCalculator.RefreshVictoryPoints(state);
            Assert.AreEqual(PlayerId.Ai, RouteCalculator.GetLongestRoadOwner(board));
            Assert.AreEqual(0, state.PlayerVictoryPoints, "perk +1 must vanish with longest route");
            Assert.AreEqual(2, state.AiVictoryPoints, "AI gets regular 2 VP longest, no perk");
            Assert.AreEqual(0, state.PlayerBonusVictoryPoints);
        }

        [Test]
        public void LongRoadBonus_DoesNotDoubleCountRegularLongestRouteBonus()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var state = new GameState(board);
            PlaceRoadPath(board, PlayerId.Human, 5, new HexCoord(0, 0), 0);

            VictoryCalculator.RefreshVictoryPoints(state);
            Assert.AreEqual(2, state.PlayerVictoryPoints, "regular longest route is 2 VP without the perk");

            state.AcquiredPerks.Add(LevelUpPerkId.LongRoadBonus);
            VictoryCalculator.RefreshVictoryPoints(state);
            Assert.AreEqual(3, state.PlayerVictoryPoints, "perk adds +1, not another +2");

            VictoryCalculator.RefreshVictoryPoints(state);
            Assert.AreEqual(3, state.PlayerVictoryPoints);
            Assert.AreEqual(0, state.PlayerBonusVictoryPoints);
        }

        private static void PlaceBuilding(BoardState board, BuildingType type, PlayerId player)
        {
            int skip = board.VertexBuildings.Count;
            foreach (var hex in board.Tiles.Keys)
            {
                for (int c = 0; c < 6; c++)
                {
                    var vertex = VertexGraph.Canonicalize(new Vertex(hex, c));
                    if (board.VertexBuildings.ContainsKey(vertex))
                        continue;
                    if (skip > 0)
                    {
                        skip--;
                        continue;
                    }
                    board.VertexBuildings[vertex] = (type, player);
                    return;
                }
            }
            Assert.Fail("No free vertex");
        }

        private static void PlaceRoadPath(BoardState board, PlayerId player, int roads, HexCoord startHex, int corner)
        {
            var current = VertexGraph.Canonicalize(new Vertex(startHex, corner));
            var used = new HashSet<Vertex> { current };
            for (int i = 0; i < roads; i++)
            {
                bool placed = false;
                foreach (var adj in VertexGraph.GetAdjacentVertices(current))
                {
                    if (!used.Add(adj))
                        continue;
                    var edge = new Edge(current, adj);
                    if (board.Roads.ContainsKey(edge))
                        continue;
                    board.Roads[edge] = player;
                    current = adj;
                    placed = true;
                    break;
                }
                if (!placed)
                    Assert.Fail($"Could not place road {i + 1}/{roads} for {player}");
            }
        }
    }
}
