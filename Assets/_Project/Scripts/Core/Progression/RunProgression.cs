using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Leaders;

namespace CatanRoguelike.Core.Progression
{
    public static class RunProgression
    {
        public const int LevelUpIntervalDays = 5;
        public const int MaxLevelUpsPerRun = 3;
        public const int DraftPickCount = 2;

        public static bool ShouldOfferLevelUp(GameState state) =>
            state.LevelUpsTaken < MaxLevelUpsPerRun
            && state.Board.DayNumber > 0
            && state.Board.DayNumber % LevelUpIntervalDays == 0
            && state.LastLevelUpDay != state.Board.DayNumber;

        public static List<LevelUpPerkId> GenerateLevelUpChoices(GameState state, Random random)
        {
            var pool = new List<LevelUpPerkId>(LeaderLibrary.Get(state.Leader).PerkPool);
            pool.Add(LevelUpPerkId.ExtraCardDraw);
            pool.Add(LevelUpPerkId.RollInsurance);
            pool.Add(LevelUpPerkId.ThresholdDelay);

            pool = pool.Where(p => !state.AcquiredPerks.Contains(p)).ToList();
            if (pool.Count == 0) return new List<LevelUpPerkId>();

            var choices = new List<LevelUpPerkId>();
            while (choices.Count < 3 && pool.Count > 0)
            {
                int idx = random.Next(pool.Count);
                choices.Add(pool[idx]);
                pool.RemoveAt(idx);
            }
            return choices;
        }
    }
}
