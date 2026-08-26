using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class AiEmbargoStrategyTests
    {
        private static GameController CreateGame()
        {
            return new GameController(seed: 42, MapSize.Small);
        }

        [Test]
        public void AiPool_ContainsEmbargo()
        {
            CollectionAssert.Contains(CardLibrary.AiPool, CardId.Embargo);
            CollectionAssert.DoesNotContain(CardLibrary.AiPool, CardId.HarborCharter);
            Assert.IsTrue(CardLibrary.Get(CardId.HarborCharter).AiCanUse == false);
        }

        [Test]
        public void DrawCard_ForAi_CanDrawEmbargoFromPool()
        {
            var engine = new CardEngine(seed: 7);
            bool sawEmbargo = false;

            for (int i = 0; i < 200; i++)
            {
                if (engine.DrawCard(forAi: true) == CardId.Embargo)
                {
                    sawEmbargo = true;
                    break;
                }
            }

            Assert.IsTrue(sawEmbargo, "AI draw pool should include Embargo");
        }

        [Test]
        public void PlayEmbargo_AsHuman_SetsAiShopEmbargo()
        {
            var game = CreateGame();
            game.State.PlayerHand.Add(CardId.Embargo);

            Assert.IsTrue(game.CardEngine.PlayCard(
                game.State, PlayerId.Human, CardId.Embargo, ResourceType.Wheat));

            Assert.AreEqual(ResourceType.Wheat, game.State.AiShopEmbargo);
            Assert.AreEqual(1, game.State.AiEmbargoDaysLeft);
            Assert.IsFalse(game.State.PlayerHand.Contains(CardId.Embargo));
        }

        [Test]
        public void ExecuteNightPlan_PlayEmbargo_SetsPlayerShopEmbargoOnHumanResource()
        {
            var game = CreateGame();
            game.State.AiHand.Add(CardId.Embargo);
            game.State.PlayerInventory = new ResourceBundle { Wheat = 6, Wood = 1 };
            game.State.ShopDeals.Add(new ShopDeal(
                ResourceType.Wood, ShopGenerator.BaseTradeRate, ResourceType.Brick, 1));

            var ai = new AiController(42);
            ai.ExecuteNightPlan(game);

            Assert.AreEqual(ResourceType.Wheat, game.State.PlayerShopEmbargo);
            Assert.AreEqual(1, game.State.PlayerEmbargoDaysLeft);
            Assert.IsFalse(game.State.AiHand.Contains(CardId.Embargo));
            Assert.IsFalse(game.State.AiShopEmbargo.HasValue);
        }

        [Test]
        public void EmbargoTargetSelector_PrefersShopGiveOverSmallInventory()
        {
            var game = CreateGame();
            game.State.PlayerInventory = new ResourceBundle { Wheat = 1 };
            game.State.ShopDeals.Add(new ShopDeal(
                ResourceType.Brick, ShopGenerator.BaseTradeRate, ResourceType.Stone, 1));
            game.State.ShopDeals.Add(new ShopDeal(
                ResourceType.Brick, ShopGenerator.BaseTradeRate, ResourceType.Wood, 1));

            var target = EmbargoTargetSelector.PickTarget(game.State);

            Assert.AreEqual(ResourceType.Brick, target);
        }

        [Test]
        public void ExecuteDayTurn_SkipsEmbargoedShopDeal_BuysOtherDeal()
        {
            var game = CreateGame();
            game.State.AiShopEmbargo = ResourceType.Wood;
            game.State.AiEmbargoDaysLeft = 1;

            var embargoedDeal = new ShopDeal(
                ResourceType.Wood, ShopGenerator.BaseTradeRate, ResourceType.Brick, 1);
            var openDeal = new ShopDeal(
                ResourceType.Wheat, ShopGenerator.BaseTradeRate, ResourceType.Sheep, 1);
            game.State.ShopDeals.Add(embargoedDeal);
            game.State.ShopDeals.Add(openDeal);

            game.State.AiInventory = new ResourceBundle
            {
                Wood = ShopGenerator.BaseTradeRate,
                Wheat = ShopGenerator.BaseTradeRate
            };

            var ai = new AiController(42);
            ai.ExecuteDayTurn(game);

            Assert.AreEqual(ShopGenerator.BaseTradeRate, game.State.AiInventory.Wood,
                "embargoed wood deal should be skipped, not purchased");
            Assert.AreEqual(0, game.State.AiInventory.Wheat);
            Assert.AreEqual(1, game.State.AiInventory.Sheep);
        }

        [Test]
        public void TryPurchase_AsHuman_BlockedByPlayerShopEmbargo()
        {
            var game = CreateGame();
            game.State.PlayerShopEmbargo = ResourceType.Stone;
            game.State.PlayerEmbargoDaysLeft = 1;
            game.State.PlayerInventory = new ResourceBundle { Stone = ShopGenerator.BaseTradeRate };

            var deal = new ShopDeal(ResourceType.Stone, ShopGenerator.BaseTradeRate, ResourceType.Wheat, 1);

            Assert.AreEqual(int.MaxValue, game.Shop.GetEffectiveGiveAmount(game.State, PlayerId.Human, deal));
            Assert.IsFalse(game.Shop.TryPurchase(game.State, PlayerId.Human, deal));
            Assert.AreEqual(ShopGenerator.BaseTradeRate, game.State.PlayerInventory.Stone);
        }
    }
}
