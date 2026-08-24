using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Map;

namespace CatanRoguelike.Core.Victory
{
    public static class VictoryCalculator
    {
        public static int CalculateVictoryPoints(BoardState board, PlayerId player)
        {
            int vp = 0;
            vp += board.CountBuildings(player, BuildingType.Settlement);
            vp += board.CountBuildings(player, BuildingType.City) * 2;

            var longestOwner = RouteCalculator.GetLongestRoadOwner(board);
            if (longestOwner == player)
                vp += BalanceConfig.LongestRouteVictoryPoints;

            return vp;
        }

        public static void RefreshVictoryPoints(GameState state)
        {
            state.PlayerVictoryPoints = CalculateVictoryPoints(state.Board, PlayerId.Human);
            state.AiVictoryPoints = CalculateVictoryPoints(state.Board, PlayerId.Ai);
        }

        public static PlayerId? CheckWinner(GameState state)
        {
            RefreshVictoryPoints(state);

            if (state.PlayerVictoryPoints >= BalanceConfig.VictoryPointGoal)
                return PlayerId.Human;
            if (state.AiVictoryPoints >= BalanceConfig.VictoryPointGoal)
                return PlayerId.Ai;
            return null;
        }
    }
}
