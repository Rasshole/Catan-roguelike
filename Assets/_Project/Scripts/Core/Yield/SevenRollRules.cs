using System.Collections.Generic;

namespace CatanRoguelike.Core.Yield
{
    /// <summary>Classic Catan seven: no production, discard half if over 7 resource cards, then robber.</summary>
    public static class SevenRollRules
    {
        public const int DiscardThreshold = 7;

        public static bool RolledSeven(IReadOnlyList<int> diceRolls)
        {
            if (diceRolls == null)
                return false;
            foreach (var roll in diceRolls)
            {
                if (roll == 7)
                    return true;
            }
            return false;
        }

        /// <summary>Resources in hand above 7 → discard half (rounded down).</summary>
        public static int GetDiscardCount(int resourceCardTotal)
        {
            if (resourceCardTotal <= DiscardThreshold)
                return 0;
            return resourceCardTotal / 2;
        }

        public static bool ValidateDiscardTotal(int required, ResourceBundle discard)
        {
            if (required <= 0)
                return discard.Total == 0;
            return discard.Total == required;
        }
    }
}
