using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class ShopDealDisplayTests
    {
        private static GameState CreateState()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            return new GameState(board) { MapSize = MapSize.Small };
        }

        private static ShopDeal CreateRiskyDeal() =>
            new ShopDeal(
                ResourceType.Wood,
                ShopGenerator.RiskyTradeRate,
                ResourceType.Brick,
                1,
                risky: true,
                riskDescription: ShopDealDisplay.RiskyRobberConsequence);

        [Test]
        public void FormatRiskConsequence_ReturnsEmpty_ForSafeDeal()
        {
            var deal = new ShopDeal(
                ResourceType.Wood, ShopGenerator.BaseTradeRate, ResourceType.Brick, 1);

            Assert.AreEqual("", ShopDealDisplay.FormatRiskConsequence(CreateState(), PlayerId.Human, deal));
        }

        [Test]
        public void FormatRiskConsequence_StatesRobberOnBestTile_ForRiskyDeal()
        {
            var line = ShopDealDisplay.FormatRiskConsequence(CreateState(), PlayerId.Human, CreateRiskyDeal());

            Assert.That(line, Does.Contain("⚠"));
            Assert.That(line, Does.Contain("best tile"));
            Assert.That(line, Does.Contain("Robber"));
            Assert.That(line, Does.Not.Contain("RISKY"));
        }

        [Test]
        public void FormatRiskConsequence_ShowsWaived_WhenHumanHasRiskyDealsSafe()
        {
            var state = CreateState();
            state.AcquiredPerks.Add(LevelUpPerkId.RiskyDealsSafe);

            var line = ShopDealDisplay.FormatRiskConsequence(state, PlayerId.Human, CreateRiskyDeal());

            Assert.That(line, Does.Contain("robber penalty waived"));
            Assert.That(line, Does.Not.Contain("best tile"));
        }

        [Test]
        public void FormatShopButtonLabel_IncludesConsequenceLine_ForRiskyDeal()
        {
            var state = CreateState();
            var deal = CreateRiskyDeal();
            var pricing = ShopDealPricing.Analyze(state, PlayerId.Human, deal);

            var label = ShopDealDisplay.FormatShopButtonLabel(state, PlayerId.Human, deal, pricing);

            Assert.That(label, Does.StartWith("Buy:"));
            Assert.That(label, Does.Contain("Give 2 Wood"));
            Assert.That(label, Does.Contain("(base)"));
            Assert.That(label, Does.Contain(ShopDealDisplay.RiskyRobberConsequence));
        }

        [Test]
        public void FormatShopButtonLabel_OmitsConsequence_ForSafeDeal()
        {
            var state = CreateState();
            var deal = new ShopDeal(
                ResourceType.Wheat, ShopGenerator.BaseTradeRate, ResourceType.Sheep, 1);
            var pricing = ShopDealPricing.Analyze(state, PlayerId.Human, deal);

            var label = ShopDealDisplay.FormatShopButtonLabel(state, PlayerId.Human, deal, pricing);

            Assert.That(label, Does.Not.Contain("Robber"));
            Assert.That(label, Does.Not.Contain("⚠"));
        }
    }
}
