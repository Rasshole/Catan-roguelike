using System;
using System.Collections.Generic;

namespace CatanRoguelike.Core.Yield
{
    /// <summary>2d6 sums (2–12) — classic Catan dice; one roll per yield pass.</summary>
    public sealed class DiceRollEngine
    {
        private readonly Random _random;

        public DiceRollEngine(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public int Roll2d6() => RollD6() + RollD6();

        public List<int> RollNightly(int passes)
        {
            var rolls = new List<int>(passes);
            for (int i = 0; i < passes; i++)
                rolls.Add(Roll2d6());
            return rolls;
        }

        private int RollD6() => _random.Next(1, 7);
    }
}
