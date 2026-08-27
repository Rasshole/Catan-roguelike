using CatanRoguelike.Core.Data;

namespace CatanRoguelike.Core.Victory
{
    /// <summary>
    /// Largest Army: count played Knight cards per player; 3+ and strictly most = holder.
    /// Classic Catan ties: incumbent keeps until strictly surpassed.
    /// </summary>
    public static class ArmyCalculator
    {
        public static int GetKnightsPlayed(GameState state, PlayerId player) =>
            player == PlayerId.Human ? state.PlayerKnightsPlayed : state.AiKnightsPlayed;

        /// <summary>Records a successful Knight card play and updates <see cref="GameState.LargestArmyOwner"/>.</summary>
        public static void RecordKnightPlayed(GameState state, PlayerId player)
        {
            if (player == PlayerId.Human)
                state.PlayerKnightsPlayed++;
            else
                state.AiKnightsPlayed++;

            UpdateLargestArmyOwner(state);
        }

        public static void UpdateLargestArmyOwner(GameState state)
        {
            int human = state.PlayerKnightsPlayed;
            int ai = state.AiKnightsPlayed;
            var holder = state.LargestArmyOwner;

            if (holder == null)
            {
                if (human >= BalanceConfig.LargestArmyThreshold && human > ai)
                    state.LargestArmyOwner = PlayerId.Human;
                else if (ai >= BalanceConfig.LargestArmyThreshold && ai > human)
                    state.LargestArmyOwner = PlayerId.Ai;
                return;
            }

            var challenger = holder == PlayerId.Human ? PlayerId.Ai : PlayerId.Human;
            int holderCount = holder == PlayerId.Human ? human : ai;
            int challengerCount = challenger == PlayerId.Human ? human : ai;

            if (challengerCount >= BalanceConfig.LargestArmyThreshold && challengerCount > holderCount)
                state.LargestArmyOwner = challenger;
        }
    }
}
