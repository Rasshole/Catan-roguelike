using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;

namespace CatanRoguelike.Core.Victory
{
    public static class VictoryCalculator
    {
        public static VictoryBreakdown GetBreakdown(GameState state, PlayerId player)
        {
            bool longRoadPerk = player == PlayerId.Human && state.HasPerk(LevelUpPerkId.LongRoadBonus);
            return GetBreakdown(
                state.Board,
                player,
                state.GetBonusVictoryPoints(player),
                longRoadPerk);
        }

        public static VictoryBreakdown GetBreakdown(
            BoardState board,
            PlayerId player,
            int bonusVp,
            bool longRoadBonusPerk = false)
        {
            int settlements = board.CountBuildings(player, BuildingType.Settlement);
            int cities = board.CountBuildings(player, BuildingType.City) * 2;
            int longest = 0;
            int longRoadBonus = 0;

            var longestOwner = RouteCalculator.GetLongestRoadOwner(board);
            if (longestOwner == player)
            {
                longest = BalanceConfig.LongestRouteVictoryPoints;
                if (longRoadBonusPerk)
                    longRoadBonus = 1;
            }

            return new VictoryBreakdown(settlements, cities, longest, longRoadBonus, bonusVp);
        }

        public static int CalculateVictoryPoints(BoardState board, PlayerId player, bool longRoadBonusPerk = false)
        {
            return GetBreakdown(board, player, 0, longRoadBonusPerk).Total;
        }

        public static void RefreshVictoryPoints(GameState state)
        {
            state.PlayerVictoryPoints = GetBreakdown(state, PlayerId.Human).Total;
            state.AiVictoryPoints = GetBreakdown(state, PlayerId.Ai).Total;
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
