using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    public class ShopDealPricingTests
    {
        private static GameState CreateState(MapSize size = MapSize.Large)
        {
            var board = MapPresets.CreateBoard(size);
            var state = new GameState(board)
            {
                MapSize = size,
                Leader = LeaderId.Pioneer,
            };
            state.Ports = PortAccess.DiscoverPorts(board);
            return state;
        }

        [Test]
        public void Analyze_MatchesShopGeneratorEffectiveCost()
        {
            var game = new GameController(42, MapSize.Large);
            var deal = new ShopDeal(ResourceType.Wood, ShopGenerator.BaseTradeRate, ResourceType.Brick, 1);

            int fromGenerator = game.Shop.GetEffectiveGiveAmount(game.State, PlayerId.Human, deal);
            var breakdown = ShopDealPricing.Analyze(game.State, PlayerId.Human, deal);

            Assert.AreEqual(fromGenerator, breakdown.EffectiveGive);
        }

        [Test]
        public void Analyze_BaseRate_WhenNoDiscounts()
        {
            var state = CreateState();
            var deal = new ShopDeal(ResourceType.Wood, ShopGenerator.BaseTradeRate, ResourceType.Brick, 1);

            var breakdown = ShopDealPricing.Analyze(state, PlayerId.Human, deal);

            Assert.AreEqual(ShopGenerator.BaseTradeRate, breakdown.EffectiveGive);
            Assert.AreEqual(ShopPriceReasonKind.Base, breakdown.Reason);
            Assert.AreEqual("base", ShopDealPricing.FormatShortReason(breakdown.Reason));
        }

        [Test]
        public void Analyze_Port2To1_WhenSpecificPortControlled()
        {
            var state = CreateState();
            var port = state.Ports.First(p => !p.IsGeneric);
            state.Board.VertexBuildings[port.Vertex] = (BuildingType.Settlement, PlayerId.Human);

            var deal = new ShopDeal(port.SpecificResource!.Value, ShopGenerator.BaseTradeRate, ResourceType.Wheat, 1);
            var breakdown = ShopDealPricing.Analyze(state, PlayerId.Human, deal);

            Assert.AreEqual(2, breakdown.EffectiveGive);
            Assert.AreEqual(ShopPriceReasonKind.Port2To1, breakdown.Reason);
        }

        [Test]
        public void Analyze_Port3To1_WhenGenericPortControlled()
        {
            var state = CreateState();
            var generic = state.Ports.First(p => p.IsGeneric);
            state.Board.VertexBuildings[generic.Vertex] = (BuildingType.Settlement, PlayerId.Human);

            var deal = new ShopDeal(ResourceType.Wood, ShopGenerator.BaseTradeRate, ResourceType.Brick, 1);
            var breakdown = ShopDealPricing.Analyze(state, PlayerId.Human, deal);

            Assert.AreEqual(3, breakdown.EffectiveGive);
            Assert.AreEqual(ShopPriceReasonKind.Port3To1, breakdown.Reason);
        }

        [Test]
        public void Analyze_Leader_WhenMerchantDiscountApplies()
        {
            var state = CreateState();
            state.Leader = LeaderId.Merchant;

            var deal = new ShopDeal(ResourceType.Wood, ShopGenerator.BaseTradeRate, ResourceType.Brick, 1);
            var breakdown = ShopDealPricing.Analyze(state, PlayerId.Human, deal);

            Assert.AreEqual(3, breakdown.EffectiveGive);
            Assert.AreEqual(ShopPriceReasonKind.Leader, breakdown.Reason);
        }

        [Test]
        public void Analyze_Event_WhenMarketDayBonusActive()
        {
            var state = CreateState();
            state.EventShopBonus = 1;

            var deal = new ShopDeal(ResourceType.Wood, ShopGenerator.BaseTradeRate, ResourceType.Brick, 1);
            var breakdown = ShopDealPricing.Analyze(state, PlayerId.Human, deal);

            Assert.AreEqual(3, breakdown.EffectiveGive);
            Assert.AreEqual(ShopPriceReasonKind.Event, breakdown.Reason);
        }

        [Test]
        public void Analyze_Perk_WhenPortDiscountReducesFurther()
        {
            var state = CreateState();
            var port = state.Ports.First(p => !p.IsGeneric);
            state.Board.VertexBuildings[port.Vertex] = (BuildingType.Settlement, PlayerId.Human);
            state.AcquiredPerks.Add(LevelUpPerkId.PortDiscount);

            var unrelatedGive = System.Enum.GetValues(typeof(ResourceType))
                .Cast<ResourceType>()
                .First(r => r != port.SpecificResource);
            var deal = new ShopDeal(unrelatedGive, ShopGenerator.BaseTradeRate, ResourceType.Brick, 1);
            var breakdown = ShopDealPricing.Analyze(state, PlayerId.Human, deal);

            Assert.AreEqual(3, breakdown.EffectiveGive);
            Assert.AreEqual(ShopPriceReasonKind.PerkPortDiscount, breakdown.Reason);
        }

        [Test]
        public void Analyze_Port2To1_BeatsLeaderDiscount()
        {
            var state = CreateState();
            state.Leader = LeaderId.Merchant;
            var port = state.Ports.First(p => !p.IsGeneric);
            state.Board.VertexBuildings[port.Vertex] = (BuildingType.Settlement, PlayerId.Human);

            var deal = new ShopDeal(port.SpecificResource!.Value, ShopGenerator.BaseTradeRate, ResourceType.Wheat, 1);
            var breakdown = ShopDealPricing.Analyze(state, PlayerId.Human, deal);

            Assert.AreEqual(2, breakdown.EffectiveGive);
            Assert.AreEqual(ShopPriceReasonKind.Port2To1, breakdown.Reason);
        }
    }
}
