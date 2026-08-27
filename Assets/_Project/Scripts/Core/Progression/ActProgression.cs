using CatanRoguelike.Core.Data;

namespace CatanRoguelike.Core.Progression
{
    /// <summary>
    /// Day→act mapping and scaling knobs for escalating runs (Fase 2.4).
    /// Act 1: days 1–5, Act 2: days 6–10, Act 3: day 11+.
    /// </summary>
    public static class ActProgression
    {
        public const int Act2StartDay = BalanceConfig.Act2StartDay;
        public const int Act3StartDay = BalanceConfig.Act3StartDay;

        public static int GetAct(int dayNumber) =>
            dayNumber >= Act3StartDay ? 3
            : dayNumber >= Act2StartDay ? 2
            : 1;

        public static string GetActLabel(int act) => $"Act {act}";

        public static string GetActUnlockSummary(int act) => act switch
        {
            2 => "Act 2: double yield rolls, harder events, stronger AI, map grows",
            3 => "Act 3: max roll 3, fiercer events, AI draws extra card",
            _ => ""
        };

        public static (int rollPasses, int maxRoll) GetYieldConfig(int dayNumber)
        {
            int act = GetAct(dayNumber);
            return (GetNightlyRollPasses(act), GetMaxRoll(act));
        }

        public static int GetNightlyRollPasses(int act) => act switch
        {
            1 => BalanceConfig.Act1NightlyRollPasses,
            2 => BalanceConfig.Act2NightlyRollPasses,
            _ => BalanceConfig.Act3NightlyRollPasses
        };

        public static int GetMaxRoll(int act) => act switch
        {
            1 => BalanceConfig.Act1MaxRoll,
            2 => BalanceConfig.Act2MaxRoll,
            _ => BalanceConfig.Act3MaxRoll
        };

        public static int GetMaxRollForDay(int dayNumber) => GetMaxRoll(GetAct(dayNumber));

        public static float GetEventChance(int act) => act switch
        {
            1 => BalanceConfig.Act1EventChance,
            2 => BalanceConfig.Act2EventChance,
            _ => BalanceConfig.Act3EventChance
        };

        public static int GetAiNightCardPlays(int act) => act >= 2 ? 2 : 1;

        public static int GetAiNightDraws(int act) => act >= 3 ? 2 : 1;

        /// <summary>
        /// Map growth: Small→Medium at Act 2, any non-Large→Large at Act 3.
        /// Returns null when no expansion applies.
        /// </summary>
        public static MapSize? GetMapExpansionTarget(MapSize currentSize, int act)
        {
            if (act >= 3 && currentSize != MapSize.Large)
                return MapSize.Large;
            if (act >= 2 && currentSize == MapSize.Small)
                return MapSize.Medium;
            return null;
        }

        public static MapSize? GetMapExpansionTargetForDay(MapSize currentSize, int dayNumber) =>
            GetMapExpansionTarget(currentSize, GetAct(dayNumber));
    }
}
