using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;

namespace CatanRoguelike.Core.Victory
{
    public static class VictoryCalculator
    {
        public static int CalculateVictoryPoints(BoardState board, PlayerId player, bool longRoadBonusPerk = false)
        {
            int vp = 0;
            vp += board.CountBuildings(player, BuildingType.Settlement);
            vp += board.CountBuildings(player, BuildingType.City) * 2;

            var longestOwner = RouteCalculator.GetLongestRoadOwner(board);
            if (longestOwner == player)
            {
                vp += BalanceConfig.LongestRouteVictoryPoints;
                if (longRoadBonusPerk)
                    vp += 1;
            }

            return vp;
        }

        public static void RefreshVictoryPoints(GameState state)
        {
            bool humanLongRoadPerk = state.HasPerk(LevelUpPerkId.LongRoadBonus);
            state.PlayerVictoryPoints = CalculateVictoryPoints(state.Board, PlayerId.Human, humanLongRoadPerk)
                + state.GetBonusVictoryPoints(PlayerId.Human);
            state.AiVictoryPoints = CalculateVictoryPoints(state.Board, PlayerId.Ai)
                + state.GetBonusVictoryPoints(PlayerId.Ai);
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
