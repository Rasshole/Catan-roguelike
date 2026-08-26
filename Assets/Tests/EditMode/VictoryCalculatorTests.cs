using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Victory;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

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
    }
}
