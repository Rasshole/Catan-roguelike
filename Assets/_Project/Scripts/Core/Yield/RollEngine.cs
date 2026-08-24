using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;

namespace CatanRoguelike.Core.Yield
{
    public sealed class RollEngine
    {
        private readonly Random _random;

        public RollEngine(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public Dictionary<ResourceType, int> RollNightly(int maxRoll = 2)
        {
            var rolls = new Dictionary<ResourceType, int>();
            foreach (ResourceType resource in Enum.GetValues(typeof(ResourceType)))
            {
                rolls[resource] = RollSingle(maxRoll);
            }

            ApplyCap(rolls, 0, maxRoll);
            ApplyCap(rolls, maxRoll, maxRoll);

            return rolls;
        }

        public int RollResource(ResourceType resource, int maxRoll = 2)
        {
            return RollSingle(maxRoll);
        }

        private int RollSingle(int maxRoll)
        {
            if (maxRoll <= 2)
            {
                int total = BalanceConfig.RollZeroWeight + BalanceConfig.RollOneWeight + BalanceConfig.RollTwoWeight;
                int roll = _random.Next(total);
                if (roll < BalanceConfig.RollZeroWeight) return 0;
                if (roll < BalanceConfig.RollZeroWeight + BalanceConfig.RollOneWeight) return 1;
                return 2;
            }

            // Late game 0-3 distribution
            int[] weights = { 15, 45, 25, 15 };
            int sum = weights.Sum();
            int r = _random.Next(sum);
            int acc = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                acc += weights[i];
                if (r < acc) return i;
            }
            return 1;
        }

        /// <summary>50/50 elimination until exactly one resource keeps targetValue.</summary>
        private void ApplyCap(Dictionary<ResourceType, int> rolls, int targetValue, int maxRoll)
        {
            if (maxRoll < targetValue) return;

            var contenders = rolls.Where(kv => kv.Value == targetValue).Select(kv => kv.Key).ToList();
            while (contenders.Count > 1)
            {
                int i = _random.Next(contenders.Count);
                int j = _random.Next(contenders.Count);
                while (j == i)
                    j = _random.Next(contenders.Count);

                var winner = _random.Next(2) == 0 ? contenders[i] : contenders[j];
                var loser = winner.Equals(contenders[i]) ? contenders[j] : contenders[i];

                rolls[loser] = 1;
                contenders.Remove(loser);
            }
        }
    }
}
