using CatanRoguelike.Core.Map;

namespace CatanRoguelike.Core.Data
{
    public static class BalanceConfig
    {
        public const int VictoryPointGoal = 10;
        public const int LongestRouteVictoryPoints = 3;
        public const int LongestRouteMinimum = 3;
        public const int LargestArmyThreshold = 2;
        public const int LargestArmyVictoryPoints = 3;
        public const int MaxHandSize = 5;
        public const int CardsDrawnPerNight = 1;
        public const int MaxCardsPlayedPerNight = 1;

        public const int SettlementThresholdCount = 7;
        public const float SettlementThresholdMultiplier = 1.5f;

        // Balance pass: fewer zero-weather nights, more 2× resource rolls
        public const int RollZeroWeight = 5;
        public const int RollOneWeight = 45;
        public const int RollTwoWeight = 50;

        // Act progression (Fase 2.4) — day thresholds and scaling knobs
        public const int Act2StartDay = 5;
        public const int Act3StartDay = 9;

        public const float Act1EventChance = 0.14f;
        public const float Act2EventChance = 0.22f;
        public const float Act3EventChance = 0.30f;

        public const int Act1NightlyRollPasses = 2;
        public const int Act2NightlyRollPasses = 3;
        public const int Act3NightlyRollPasses = 3;

        public const int Act1MaxRoll = 2;
        public const int Act2MaxRoll = 3;
        public const int Act3MaxRoll = 3;

        public static ResourceBundle RoadCost => new() { Wood = 1, Brick = 1 };

        public static ResourceBundle SettlementCost => new()
        {
            Wood = 1, Brick = 1, Wheat = 1, Sheep = 1
        };

        public static ResourceBundle CityCost => new() { Wheat = 1, Stone = 2 };

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
