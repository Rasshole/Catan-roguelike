using CatanRoguelike.Core.Map;

namespace CatanRoguelike.Core.Data
{
    public static class BalanceConfig
    {
        public const int VictoryPointGoal = 10;
        public const int LongestRouteVictoryPoints = 2;
        public const int MaxHandSize = 5;
        public const int CardsDrawnPerNight = 1;
        public const int MaxCardsPlayedPerNight = 1;

        public const int SettlementThresholdCount = 5;
        public const float SettlementThresholdMultiplier = 1.5f;

        public const int RollZeroWeight = 15;
        public const int RollOneWeight = 55;
        public const int RollTwoWeight = 25;

        public static ResourceBundle RoadCost => new() { Wood = 1, Brick = 1 };

        public static ResourceBundle SettlementCost => new()
        {
            Wood = 1, Brick = 1, Wheat = 1, Sheep = 1
        };

        public static ResourceBundle CityCost => new() { Wheat = 2, Stone = 3 };

        public static ResourceBundle GetSettlementCost(BoardState board, PlayerId player, int threshold = SettlementThresholdCount)
        {
            int count = board.CountBuildings(player, BuildingType.Settlement)
                      + board.CountBuildings(player, BuildingType.City);
            return ApplyThreshold(SettlementCost, count, threshold, SettlementThresholdMultiplier);
        }

        public static ResourceBundle GetRoadCost(BoardState board, PlayerId player)
        {
            int count = board.CountRoads(player);
            return ApplyThreshold(RoadCost, count, 8, 1.5f);
        }

        public static ResourceBundle GetCityCost(BoardState board, PlayerId player)
        {
            int count = board.CountBuildings(player, BuildingType.City);
            return ApplyThreshold(CityCost, count, 3, 1.5f);
        }

        private static ResourceBundle ApplyThreshold(ResourceBundle baseCost, int count, int threshold, float multiplier)
        {
            if (count < threshold) return baseCost;

            int tiers = count / threshold;
            float factor = (float)System.Math.Pow(multiplier, tiers);
            return new ResourceBundle
            {
                Wood = CeilCost(baseCost.Wood, factor),
                Brick = CeilCost(baseCost.Brick, factor),
                Wheat = CeilCost(baseCost.Wheat, factor),
                Sheep = CeilCost(baseCost.Sheep, factor),
                Stone = CeilCost(baseCost.Stone, factor)
            };
        }

        private static int CeilCost(int value, float factor) =>
            value == 0 ? 0 : (int)System.Math.Ceiling(value * factor);
    }
}
