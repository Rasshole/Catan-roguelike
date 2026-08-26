using System.Collections.Generic;
using System.Linq;

namespace CatanRoguelike.Core.Cards
{
    /// <summary>
    /// Picks which resource to embargo against the human (inventory + shop Give usage).
    /// </summary>
    public static class EmbargoTargetSelector
    {
        public const int ShopDealWeight = 4;

        public static ResourceType PickTarget(GameState state)
        {
            var scores = new Dictionary<ResourceType, int>();

            foreach (var (type, amount) in state.PlayerInventory.EnumerateNonZero())
            {
                scores.TryGetValue(type, out int current);
                scores[type] = current + amount;
            }

            foreach (var deal in state.ShopDeals)
            {
                scores.TryGetValue(deal.Give, out int current);
                scores[deal.Give] = current + ShopDealWeight;
            }

            if (scores.Count == 0)
                return ResourceType.Wheat;

            return scores.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;
        }
    }

}
