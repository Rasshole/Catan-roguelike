using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core;

namespace CatanRoguelike.Core.Ai
{
    public enum AiDayActionKind
    {
        Shop,
        UpgradeCity,
        BuildSettlement,
        BuildRoad,
        MoveRobber
    }

    /// <summary>Non-timing plan of AI day actions for logging and paced playback.</summary>
    public static class AiPacingPlanner
    {
        public static List<AiDayActionKind> PlanDayActions(GameState state)
        {
            var steps = new List<AiDayActionKind>();
            if (state.ShopDeals != null && state.ShopDeals.Count > 0)
                steps.Add(AiDayActionKind.Shop);

            int act = ActProgression.GetAct(state.Board.DayNumber);
            bool hasCityUpgrade = state.Board.VertexBuildings.Values.Any(v =>
                v.owner == PlayerId.Ai
                && v.type == BuildingType.Settlement
                && state.AiInventory.CanAfford(BalanceConfig.GetCityCost(state.Board, PlayerId.Ai)));
            if (hasCityUpgrade)
                steps.Add(AiDayActionKind.UpgradeCity);

            var placement = new PlacementValidator();
            bool canSettlement = placement.GetValidSettlementSpots(state.Board, PlayerId.Ai, false).Any()
                && state.AiInventory.CanAfford(BalanceConfig.GetSettlementCost(state.Board, PlayerId.Ai));
            if (canSettlement)
                steps.Add(AiDayActionKind.BuildSettlement);

            bool canRoad = placement.GetValidRoadSpots(state.Board, PlayerId.Ai, false).Any()
                && state.AiInventory.CanAfford(BalanceConfig.GetRoadCost(state.Board, PlayerId.Ai));
            if (canRoad)
                steps.Add(AiDayActionKind.BuildRoad);

            steps.Add(AiDayActionKind.MoveRobber);
            return steps;
        }
    }
}
