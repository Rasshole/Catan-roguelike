using System;
using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Map;

namespace CatanRoguelike.Core.Shop
{
    public sealed class ShopDeal
    {
        public ResourceType Give { get; }
        public int GiveAmount { get; }
        public ResourceType Receive { get; }
        public int ReceiveAmount { get; }
        public bool IsRisky { get; }

        public ShopDeal(ResourceType give, int giveAmount, ResourceType receive, int receiveAmount, bool risky = false)
        {
            Give = give;
            GiveAmount = giveAmount;
            Receive = receive;
            ReceiveAmount = receiveAmount;
            IsRisky = risky;
        }

        public override string ToString() =>
            $"Give {GiveAmount} {Give} → Get {ReceiveAmount} {Receive}";
    }

    public sealed class ShopGenerator
    {
        private readonly Random _random;

        private static readonly (ResourceType a, ResourceType b)[] Pairs =
        {
            (ResourceType.Wood, ResourceType.Brick),
            (ResourceType.Wheat, ResourceType.Sheep),
            (ResourceType.Stone, ResourceType.Wheat),
            (ResourceType.Wood, ResourceType.Wheat),
            (ResourceType.Brick, ResourceType.Stone),
            (ResourceType.Sheep, ResourceType.Wood)
        };

        public ShopGenerator(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public List<ShopDeal> GenerateDailyDeals(int count = 3)
        {
            var deals = new List<ShopDeal>();
            var used = new HashSet<int>();

            while (deals.Count < count && used.Count < Pairs.Length)
            {
                int idx = _random.Next(Pairs.Length);
                if (!used.Add(idx)) continue;

                var (a, b) = Pairs[idx];
                int rate = _random.Next(2) == 0 ? 2 : 3;
                deals.Add(new ShopDeal(a, rate, b, 1, risky: _random.Next(5) == 0));
            }

            return deals;
        }

        public bool TryPurchase(GameState state, PlayerId player, ShopDeal deal)
        {
            if (player == PlayerId.Ai
                && state.AiShopEmbargo.HasValue
                && state.AiShopEmbargo.Value == deal.Give)
                return false;

            var inv = state.GetInventory(player);
            var cost = new ResourceBundle();
            cost.Set(deal.Give, deal.GiveAmount);

            if (!inv.CanAfford(cost)) return false;

            inv.Pay(cost);
            inv.Add(deal.Receive, deal.ReceiveAmount);
            state.SetInventory(player, inv);
            return true;
        }
    }
}
