using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Shop;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class AiControllerShopTests
    {
        private static GameController CreateGame()
        {
            return new GameController(seed: 42, MapSize.Small);
        }

        [Test]
        public void ExecuteDayTurn_BuysShopDeal_WhenAiCanAffordEffectiveCost()
        {
            var game = CreateGame();
            var deal = new ShopDeal(ResourceType.Wood, ShopGenerator.BaseTradeRate, ResourceType.Brick, 1);
            game.State.ShopDeals.Add(deal);

            game.State.AiInventory = new ResourceBundle { Wood = ShopGenerator.BaseTradeRate };

            var ai = new AiController(42);
            ai.ExecuteDayTurn(game);

            Assert.AreEqual(0, game.State.AiInventory.Wood);
            Assert.AreEqual(1, game.State.AiInventory.Brick);
        }

        [Test]
        public void ExecuteDayTurn_SkipsShopDeal_WhenAiCannotAffordEffectiveCost()
        {
            var game = CreateGame();
            var deal = new ShopDeal(ResourceType.Wood, ShopGenerator.BaseTradeRate, ResourceType.Brick, 1);
            game.State.ShopDeals.Add(deal);

            int shortByOne = ShopGenerator.BaseTradeRate - 1;
            game.State.AiInventory = new ResourceBundle { Wood = shortByOne };

            var ai = new AiController(42);
            ai.ExecuteDayTurn(game);

            Assert.AreEqual(shortByOne, game.State.AiInventory.Wood);
            Assert.AreEqual(0, game.State.AiInventory.Brick);
        }
    }
}
