using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class ShopGeneratorTests
    {
        private static GameState CreateState()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            return new GameState(board) { MapSize = MapSize.Small };
        }

        private static GameController CreateGame()
        {
            var game = new GameController(42, MapSize.Small);
            game.State.Leader = LeaderId.Pioneer;
            return game;
        }

        [Test]
        public void GenerateDailyDeals_SeededProducesDeterministicThreeDeals()
        {
            var state = CreateState();
            var shopA = new ShopGenerator(42);
            var shopB = new ShopGenerator(42);

            var dealsA = shopA.GenerateDailyDeals(state);
            var dealsB = shopB.GenerateDailyDeals(state);

            Assert.AreEqual(ShopGenerator.DailyDealCount, dealsA.Count);
            CollectionAssert.AreEqual(
                dealsA.Select(d => (d.Give, d.Receive, d.GiveAmount, d.IsRisky)).ToList(),
                dealsB.Select(d => (d.Give, d.Receive, d.GiveAmount, d.IsRisky)).ToList());
        }

        [Test]
        public void GenerateDailyDeals_FirstTwoSafeAtBaseRate_ThirdRiskyAtTwoToOne()
        {
            var state = CreateState();
            var deals = new ShopGenerator(42).GenerateDailyDeals(state);

            Assert.AreEqual(ShopGenerator.DailyDealCount, deals.Count);
            Assert.IsFalse(deals[0].IsRisky);
            Assert.IsFalse(deals[1].IsRisky);
            Assert.IsTrue(deals[2].IsRisky);

            Assert.AreEqual(ShopGenerator.BaseTradeRate, deals[0].GiveAmount);
            Assert.AreEqual(ShopGenerator.BaseTradeRate, deals[1].GiveAmount);
            Assert.AreEqual(ShopGenerator.RiskyTradeRate, deals[2].GiveAmount);
            Assert.AreEqual("Robber moves to your best tile when you buy.", deals[2].RiskDescription);
        }

        [Test]
        public void GenerateDailyDeals_WithExtraShopDealPerk_AddsFourthSafeDeal()
        {
            var state = CreateState();
            state.AcquiredPerks.Add(LevelUpPerkId.ExtraShopDeal);

            var deals = new ShopGenerator(42).GenerateDailyDeals(state);

            Assert.AreEqual(4, deals.Count);
            Assert.IsFalse(deals[3].IsRisky);
            Assert.AreEqual(ShopGenerator.BaseTradeRate, deals[3].GiveAmount);
            Assert.AreEqual(1, deals[3].ReceiveAmount);
        }

        [Test]
        public void TryPurchase_SafeDeal_ExchangesResourcesWithoutRobberMove()
        {
            var game = CreateGame();
            var deal = new ShopDeal(
                ResourceType.Wood, ShopGenerator.BaseTradeRate, ResourceType.Brick, 1);
            game.State.PlayerInventory = new ResourceBundle { Wood = ShopGenerator.BaseTradeRate };
            var robberBefore = game.State.Board.RobberTile;

            Assert.IsTrue(game.Shop.TryPurchase(game.State, PlayerId.Human, deal));
            Assert.AreEqual(0, game.State.PlayerInventory.Wood);
            Assert.AreEqual(1, game.State.PlayerInventory.Brick);
            Assert.AreEqual(robberBefore, game.State.Board.RobberTile);
        }

        [Test]
        public void GetEffectiveGiveAmount_Human_ReturnsMaxValue_WhenPlayerShopEmbargoMatchesGive()
        {
            var game = CreateGame();
            game.State.PlayerShopEmbargo = ResourceType.Brick;
            game.State.PlayerEmbargoDaysLeft = 1;

            var embargoed = new ShopDeal(
                ResourceType.Brick, ShopGenerator.BaseTradeRate, ResourceType.Wood, 1);
            var open = new ShopDeal(
                ResourceType.Wheat, ShopGenerator.BaseTradeRate, ResourceType.Sheep, 1);

            Assert.AreEqual(int.MaxValue,
                game.Shop.GetEffectiveGiveAmount(game.State, PlayerId.Human, embargoed));
            Assert.AreEqual(ShopGenerator.BaseTradeRate,
                game.Shop.GetEffectiveGiveAmount(game.State, PlayerId.Human, open));
        }

        [Test]
        public void TryPurchase_Human_BlockedByPlayerShopEmbargo_BuysOtherDeal()
        {
            var game = CreateGame();
            game.State.PlayerShopEmbargo = ResourceType.Wood;
            game.State.PlayerEmbargoDaysLeft = 1;
            game.State.PlayerInventory = new ResourceBundle
            {
                Wood = ShopGenerator.BaseTradeRate,
                Wheat = ShopGenerator.BaseTradeRate
            };

            var embargoed = new ShopDeal(
                ResourceType.Wood, ShopGenerator.BaseTradeRate, ResourceType.Brick, 1);
            var open = new ShopDeal(
                ResourceType.Wheat, ShopGenerator.BaseTradeRate, ResourceType.Sheep, 1);

            Assert.IsFalse(game.Shop.TryPurchase(game.State, PlayerId.Human, embargoed));
            Assert.IsTrue(game.Shop.TryPurchase(game.State, PlayerId.Human, open));

            Assert.AreEqual(ShopGenerator.BaseTradeRate, game.State.PlayerInventory.Wood);
            Assert.AreEqual(0, game.State.PlayerInventory.Wheat);
            Assert.AreEqual(1, game.State.PlayerInventory.Sheep);
        }

        [Test]
        public void GetEffectiveGiveAmount_Ai_ReturnsMaxValue_WhenAiShopEmbargoMatchesGive()
        {
            var game = new GameController(42, MapSize.Small);
            game.State.AiShopEmbargo = ResourceType.Stone;
            game.State.AiEmbargoDaysLeft = 1;

            var embargoed = new ShopDeal(
                ResourceType.Stone, ShopGenerator.BaseTradeRate, ResourceType.Wheat, 1);
            var open = new ShopDeal(
                ResourceType.Brick, ShopGenerator.BaseTradeRate, ResourceType.Wood, 1);

            Assert.AreEqual(int.MaxValue,
                game.Shop.GetEffectiveGiveAmount(game.State, PlayerId.Ai, embargoed));
            Assert.AreEqual(ShopGenerator.BaseTradeRate,
                game.Shop.GetEffectiveGiveAmount(game.State, PlayerId.Ai, open));
        }

        [Test]
        public void TryPurchase_Ai_BlockedByAiShopEmbargo_BuysOtherDeal()
        {
            var game = new GameController(42, MapSize.Small);
            game.State.AiShopEmbargo = ResourceType.Wood;
            game.State.AiEmbargoDaysLeft = 1;
            game.State.AiInventory = new ResourceBundle
            {
                Wood = ShopGenerator.BaseTradeRate,
                Wheat = ShopGenerator.BaseTradeRate
            };

            var embargoed = new ShopDeal(
                ResourceType.Wood, ShopGenerator.BaseTradeRate, ResourceType.Brick, 1);
            var open = new ShopDeal(
                ResourceType.Wheat, ShopGenerator.BaseTradeRate, ResourceType.Sheep, 1);

            Assert.IsFalse(game.Shop.TryPurchase(game.State, PlayerId.Ai, embargoed));
            Assert.IsTrue(game.Shop.TryPurchase(game.State, PlayerId.Ai, open));

            Assert.AreEqual(ShopGenerator.BaseTradeRate, game.State.AiInventory.Wood);
            Assert.AreEqual(0, game.State.AiInventory.Wheat);
            Assert.AreEqual(1, game.State.AiInventory.Sheep);
        }

        [Test]
        public void GetEffectiveGiveAmount_WithMarketDayBonus_ReturnsThreeForBaseDeal()
        {
            var game = CreateGame();
            new EventEngine().ApplyEvent(game.State, EventId.MarketDay);

            var deal = new ShopDeal(
                ResourceType.Wood, ShopGenerator.BaseTradeRate, ResourceType.Brick, 1);

            Assert.AreEqual(3,
                game.Shop.GetEffectiveGiveAmount(game.State, PlayerId.Human, deal));
            Assert.AreEqual(3,
                game.Shop.GetEffectiveGiveAmount(game.State, PlayerId.Ai, deal));
        }

        [Test]
        public void TryPurchase_WithMarketDayBonus_ChargesThreeNotFour()
        {
            var game = CreateGame();
            new EventEngine().ApplyEvent(game.State, EventId.MarketDay);
            game.State.PlayerInventory = new ResourceBundle { Wood = ShopGenerator.BaseTradeRate };

            var deal = new ShopDeal(
                ResourceType.Wood, ShopGenerator.BaseTradeRate, ResourceType.Brick, 1);

            Assert.IsTrue(game.Shop.TryPurchase(game.State, PlayerId.Human, deal));
            Assert.AreEqual(1, game.State.PlayerInventory.Wood,
                "Market Day should charge 3 wood, leaving 1 from the starting 4");
            Assert.AreEqual(1, game.State.PlayerInventory.Brick);
        }
    }
}
