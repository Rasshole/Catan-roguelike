using System.Collections.Generic;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Victory;

namespace CatanRoguelike.Core
{
    /// <summary>
    /// Pure helpers for IMGUI run-summary lines after a win (VP breakdown, seed, run metadata).
    /// </summary>
    public static class RunSummaryDisplay
    {
        /// <summary>
        /// Builds summary lines when <see cref="GameState.Winner"/> is set; otherwise returns false.
        /// </summary>
        public static bool TryGetSummaryLines(GameState state, int runSeed, out IReadOnlyList<string> lines)
        {
            if (!state.Winner.HasValue)
            {
                lines = null;
                return false;
            }

            var humanVp = VictoryCalculator.GetBreakdown(state, PlayerId.Human);
            var aiVp = VictoryCalculator.GetBreakdown(state, PlayerId.Ai);
            var leaderName = LeaderLibrary.Get(state.Leader).Name;
            var mapName = MapPresets.GetDisplayName(state.MapSize);
            string winnerLine = state.Winner == PlayerId.Human ? "You Win!" : "AI Wins!";

            lines = new[]
            {
                winnerLine,
                $"Day {state.Board.DayNumber}",
                mapName,
                $"Leader: {leaderName}",
                $"Seed: {runSeed}",
                $"You: {state.PlayerVictoryPoints} VP ({humanVp.FormatLine()})",
                $"AI: {state.AiVictoryPoints} VP ({aiVp.FormatLine()})",
            };
            return true;
        }
    }
}
