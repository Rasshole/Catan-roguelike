using System;
using System.Collections.Generic;
using CatanRoguelike.Core;

namespace CatanRoguelike.Core.Yield
{
    /// <summary>Fishermen of Catan fish tokens (1–3) and old boot (0) drawn on fish production.</summary>
    public static class FishTokenBag
    {
        public static readonly int[] DefaultTokenValues = { 1, 1, 2, 2, 3, 0 };

        public static void EnsureInitialized(GameState state, int runSeed)
        {
            if (state == null || !FishermenRules.IsEnabled(state))
                return;

            if (state.FishBagRemaining != null && state.FishBagRemaining.Count > 0)
                return;

            state.FishBagRemaining = new List<int>(DefaultTokenValues);
            Shuffle(state.FishBagRemaining, runSeed ^ 0xF15E);
        }

        public static int Draw(GameState state, int runSeed)
        {
            if (state.FishBagRemaining == null || state.FishBagRemaining.Count == 0)
            {
                state.FishBagRemaining = new List<int>(DefaultTokenValues);
                Shuffle(state.FishBagRemaining, runSeed ^ 0xF15E ^ state.Board.DayNumber);
            }

            int value = state.FishBagRemaining[0];
            state.FishBagRemaining.RemoveAt(0);
            return value;
        }

        private static void Shuffle(List<int> list, int seed)
        {
            var rng = new Random(seed);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
