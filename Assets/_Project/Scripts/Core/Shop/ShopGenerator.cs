using System;
using System.Collections.Generic;
using System.Linq;
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

        public string Format(int effectiveGive = -1)
        {
            int give = effectiveGive >= 0 ? effectiveGive : GiveAmount;
            string portNote = effectiveGive >= 0 && effectiveGive < GiveAmount ? " (port)" : "";
            return $"Give {give} {Give} → Get {ReceiveAmount} {Receive}{portNote}";
        }

        public override string ToString() => Format();
    }

    public sealed class ShopGenerator
    {
        private readonly Random _random;

        private static readonly (ResourceType give, ResourceType receive)[] DealTemplates =
        {
            (ResourceType.Wood, ResourceType.Brick),
            (ResourceType.Wheat, ResourceType.Sheep),
            (ResourceType.Stone, ResourceType.Wheat),
            (ResourceType.Wood, ResourceType.Wheat),
            (ResourceType.Brick, ResourceType.Stone),
            (ResourceType.Sheep, ResourceType.Wood),
            (ResourceType.Brick, ResourceType.Wheat),
            (ResourceType.Stone, ResourceType.Sheep)
        };

        public const int DailyDealCount = 3;
        public const int BaseTradeRate = 4;

        public ShopGenerator(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>Exactly 3 distinct trades per day (default 4:1 bank rate).</summary>
        public List<ShopDeal> GenerateDailyDeals()
        {
            var shuffled = DealTemplates.OrderBy(_ => _random.Next()).ToList();
            var deals = new List<ShopDeal>(DailyDealCount);

            for (int i = 0; i < DailyDealCount; i++)
            {
                var (give, receive) = shuffled[i];
                deals.Add(new ShopDeal(give, BaseTradeRate, receive, 1, risky: _random.Next(6) == 0));
            }

            return deals;
        }

        public int GetEffectiveGiveAmount(GameState state, PlayerId player, ShopDeal deal)
        {
            return PortAccess.GetEffectiveGiveAmount(state.Board, player, deal, state.Ports);
        }

        public bool TryPurchase(GameState state, PlayerId player, ShopDeal deal)
        {
            if (player == PlayerId.Ai
                && state.AiShopEmbargo.HasValue
                && state.AiShopEmbargo.Value == deal.Give)
                return false;

            int giveAmount = GetEffectiveGiveAmount(state, player, deal);
            var inv = state.GetInventory(player);
            var cost = new ResourceBundle();
            cost.Set(deal.Give, giveAmount);

            if (!inv.CanAfford(cost)) return false;

            inv.Pay(cost);
            inv.Add(deal.Receive, deal.ReceiveAmount);
            state.SetInventory(player, inv);
            return true;
        }
    }
}
