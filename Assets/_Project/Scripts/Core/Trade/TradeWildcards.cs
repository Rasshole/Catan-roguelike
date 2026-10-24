using CatanRoguelike.Core;

namespace CatanRoguelike.Core.Trade
{
    /// <summary>
    /// v139 wildcard ("any resource") trade cards. A wildcard on the give side means "I'll give N of any resource
    /// you choose"; the taker picks from the giver's hand, never from resources the giver is asking for in the same
    /// trade and never from cards already promised. Pure and deterministic (shared by single-player and the match
    /// engine, and by the UI pickers).
    /// </summary>
    public static class TradeWildcards
    {
        public const int MaxWildcards = 9;

        public static readonly ResourceType[] Resources =
        {
            ResourceType.Wood, ResourceType.Brick, ResourceType.Wheat, ResourceType.Sheep, ResourceType.Stone
        };

        /// <summary>
        /// What a wildcard giver can hand over: <paramref name="hand"/> minus the cards already
        /// <paramref name="committed"/>, with every resource in <paramref name="excludedA"/> /
        /// <paramref name="excludedB"/> left out (the giver asks for those, or they are already picked).
        /// </summary>
        public static ResourceBundle Pool(ResourceBundle hand, ResourceBundle committed, ResourceBundle excludedA,
            ResourceBundle excludedB = default)
        {
            var pool = ResourceBundle.Zero;
            foreach (var r in Resources)
            {
                if (excludedA[r] > 0 || excludedB[r] > 0)
                    continue;
                pool.Set(r, System.Math.Max(0, hand[r] - committed[r]));
            }

            return pool;
        }

        public static bool IsValid(ResourceBundle b)
        {
            foreach (var r in Resources)
                if (b[r] < 0)
                    return false;
            return true;
        }

        /// <summary>A pick of exactly <paramref name="n"/> cards, all inside <paramref name="pool"/>.</summary>
        public static bool Fits(ResourceBundle choice, ResourceBundle pool, int n) =>
            IsValid(choice) && choice.Total == n && pool.CanAfford(choice);

        /// <summary>
        /// Deterministic pick for an AI taker: one card at a time, the resource it holds fewest of
        /// (counting what it already picked), ties in board order. Returns Zero when the pool is too small.
        /// </summary>
        public static ResourceBundle PickForNeed(ResourceBundle pool, int n, ResourceBundle takerHand)
        {
            var pick = ResourceBundle.Zero;
            if (n <= 0 || pool.Total < n)
                return pick;
            for (int k = 0; k < n; k++)
            {
                ResourceType? best = null;
                int bestHeld = int.MaxValue;
                foreach (var r in Resources)
                {
                    if (pool[r] - pick[r] <= 0)
                        continue;
                    int held = takerHand[r] + pick[r];
                    if (held < bestHeld)
                    {
                        bestHeld = held;
                        best = r;
                    }
                }

                pick.Add(best.Value, 1);
            }

            return pick;
        }

        /// <summary>Deterministic pick for a giver-side default: the resource the giver holds most of first.</summary>
        public static ResourceBundle PickMostHeld(ResourceBundle pool, int n)
        {
            var pick = ResourceBundle.Zero;
            if (n <= 0 || pool.Total < n)
                return pick;
            for (int k = 0; k < n; k++)
            {
                ResourceType? best = null;
                int most = 0;
                foreach (var r in Resources)
                {
                    int left = pool[r] - pick[r];
                    if (left > most)
                    {
                        most = left;
                        best = r;
                    }
                }

                pick.Add(best.Value, 1);
            }

            return pick;
        }
    }
}
