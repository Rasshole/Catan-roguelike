using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Leaders;
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
        public string RiskDescription { get; }

        public ShopDeal(ResourceType give, int giveAmount, ResourceType receive, int receiveAmount,
            bool risky = false, string riskDescription = "")
        {
            Give = give;
            GiveAmount = giveAmount;
            Receive = receive;
            ReceiveAmount = receiveAmount;
            IsRisky = risky;
            RiskDescription = riskDescription;
        }

        public string Format(int effectiveGive = -1) =>
            ShopDealDisplay.FormatTradeSummary(this, effectiveGive);

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
        public const int RiskyTradeRate = 2;

        public ShopGenerator(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>
        /// 3 trades per day; the 3rd is often a risky 2:1 deal.
        /// Risky = better rate, but robber jumps to your best tile when purchased.
        /// </summary>
        public List<ShopDeal> GenerateDailyDeals(GameState state)
        {
            var shuffled = DealTemplates.OrderBy(_ => _random.Next()).ToList();
            var deals = new List<ShopDeal>(DailyDealCount);

            for (int i = 0; i < DailyDealCount; i++)
            {
                var (give, receive) = shuffled[i];
                bool risky = i == DailyDealCount - 1;
                int rate = risky ? RiskyTradeRate : BaseTradeRate;
                deals.Add(new ShopDeal(
                    give, rate, receive, 1,
                    risky: risky,
                    riskDescription: risky
                        ? ShopDealDisplay.RiskyRobberConsequence
                        : ""));
            }

            if (state.HasPerk(LevelUpPerkId.ExtraShopDeal) && shuffled.Count > DailyDealCount)
            {
                var extra = shuffled[DailyDealCount];
                deals.Add(new ShopDeal(extra.give, BaseTradeRate, extra.receive, 1));
            }

            return deals;
        }

        public int GetEffectiveGiveAmount(GameState state, PlayerId player, ShopDeal deal)
        {
            if (player == PlayerId.Ai
                && state.AiShopEmbargo.HasValue
                && state.AiShopEmbargo.Value == deal.Give)
                return int.MaxValue;

            if (player == PlayerId.Human
                && state.PlayerShopEmbargo.HasValue
                && state.PlayerShopEmbargo.Value == deal.Give)
                return int.MaxValue;

            int give = ModifierService.GetShopGiveAmount(state, player, deal.GiveAmount, deal.Give);
            int portGive = PortAccess.GetEffectiveGiveAmount(state.Board, player, deal, state.Ports);
            return Math.Min(give, portGive);
        }

        public bool TryPurchase(GameState state, PlayerId player, ShopDeal deal)
        {
            if (player == PlayerId.Ai
                && state.AiShopEmbargo.HasValue
                && state.AiShopEmbargo.Value == deal.Give)
                return false;

            if (player == PlayerId.Human
                && state.PlayerShopEmbargo.HasValue
                && state.PlayerShopEmbargo.Value == deal.Give)
                return false;

            int giveAmount = GetEffectiveGiveAmount(state, player, deal);
            var inv = state.GetInventory(player);
            var cost = new ResourceBundle();
            cost.Set(deal.Give, giveAmount);

            if (!inv.CanAfford(cost)) return false;

            inv.Pay(cost);
            inv.Add(deal.Receive, deal.ReceiveAmount);
            state.SetInventory(player, inv);

            if (deal.IsRisky && !(player == PlayerId.Human && state.HasPerk(LevelUpPerkId.RiskyDealsSafe)))
                ApplyRiskyDealPenalty(state, player);

            return true;
        }

        private void ApplyRiskyDealPenalty(GameState state, PlayerId player)
        {
            var best = PickPlayerBestProductionTile(state, player);
            if (best.HasValue)
            {
                state.Board.PlaceRobber(best.Value);
                state.StatusMessage += " Risky deal: robber moved!";
            }
        }

        private static HexCoord? PickPlayerBestProductionTile(GameState state, PlayerId player)
        {
            var counts = new Dictionary<HexCoord, int>();
            foreach (var kvp in state.Board.VertexBuildings)
            {
                if (kvp.Value.owner != player) continue;
                foreach (var hex in VertexGraph.GetHexesForVertex(kvp.Key))
                {
                    if (!state.Board.Tiles.ContainsKey(hex)) continue;
                    counts.TryGetValue(hex, out int c);
                    counts[hex] = c + 1;
                }
            }
            if (counts.Count == 0) return null;
            return counts.OrderByDescending(kv => kv.Value).First().Key;
        }
    }
}
