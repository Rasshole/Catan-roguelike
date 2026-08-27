using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Core.Victory;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    public class RunSummaryDisplayTests
    {
        private static GameState CreateState(MapSize mapSize = MapSize.Medium)
        {
            var board = MapPresets.CreateBoard(mapSize);
            var state = new GameState(board) { MapSize = mapSize };
            state.Leader = LeaderId.Pioneer;
            return state;
        }

        [Test]
        public void TryGetSummaryLines_ReturnsFalse_WhenWinnerIsNull()
        {
            var state = CreateState();
            Assert.IsFalse(RunSummaryDisplay.TryGetSummaryLines(state, 12345, out var lines));
            Assert.IsNull(lines);
        }

        [Test]
        public void TryGetSummaryLines_HumanWin_IncludesYouWinWording()
        {
            var state = CreateWinState(PlayerId.Human, dayNumber: 8);

            Assert.IsTrue(RunSummaryDisplay.TryGetSummaryLines(state, 4242, out var lines));
            CollectionAssert.Contains(lines, "You Win!");
            Assert.That(string.Join("\n", lines), Does.Not.Contain("AI Wins!"));
        }

        [Test]
        public void TryGetSummaryLines_AiWin_IncludesAiWinsWording()
        {
            var state = CreateWinState(PlayerId.Ai, dayNumber: 6);

            Assert.IsTrue(RunSummaryDisplay.TryGetSummaryLines(state, 999, out var lines));
            CollectionAssert.Contains(lines, "AI Wins!");
            Assert.That(string.Join("\n", lines), Does.Not.Contain("You Win!"));
        }

        [Test]
        public void TryGetSummaryLines_IncludesSeedDayMapAndLeader()
        {
            var state = CreateWinState(PlayerId.Human, dayNumber: 12);
            state.MapSize = MapSize.Large;
            state.Leader = LeaderId.Warlord;

            Assert.IsTrue(RunSummaryDisplay.TryGetSummaryLines(state, 777, out var lines));
            var text = string.Join("\n", lines);

            Assert.That(text, Does.Contain("Seed: 777"));
            Assert.That(text, Does.Contain("Day 12"));
            Assert.That(text, Does.Contain(MapPresets.GetDisplayName(MapSize.Large)));
            Assert.That(text, Does.Contain("Leader: The Warlord"));
        }

        [Test]
        public void TryGetSummaryLines_IncludesBothVpBreakdownLines()
        {
            var state = CreateWinState(PlayerId.Human, dayNumber: 5);
            PlaceBuilding(state.Board, BuildingType.Settlement, PlayerId.Human);
            PlaceBuilding(state.Board, BuildingType.City, PlayerId.Ai);
            VictoryCalculator.RefreshVictoryPoints(state);

            Assert.IsTrue(RunSummaryDisplay.TryGetSummaryLines(state, 1, out var lines));
            var text = string.Join("\n", lines);

            var humanBreakdown = VictoryCalculator.GetBreakdown(state, PlayerId.Human);
            var aiBreakdown = VictoryCalculator.GetBreakdown(state, PlayerId.Ai);

            Assert.That(text, Does.Contain($"You: {state.PlayerVictoryPoints} VP ({humanBreakdown.FormatLine()})"));
            Assert.That(text, Does.Contain($"AI: {state.AiVictoryPoints} VP ({aiBreakdown.FormatLine()})"));
        }

        private static GameState CreateWinState(PlayerId winner, int dayNumber)
        {
            var state = CreateState();
            state.Board.DayNumber = dayNumber;
            state.Winner = winner;
            state.Phase = GamePhase.GameOver;
            return state;
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
