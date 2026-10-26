using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Trade;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class PlayerTradeResolverTests
    {
        [Test]
        public void EvaluateAiResponses_AcceptsWhenAiCanPay()
        {
            var game = new GameController(5, MapSize.Small);
            var human = game.State.PlayerInventory;
            human.Wood = 2;
            game.State.PlayerInventory = human;
            var ai = game.State.AiInventory;
            ai.Brick = 2;
            game.State.AiInventory = ai;

            var offer = new PlayerTradeOffer(
                new ResourceBundle { Wood = 1 },
                new ResourceBundle { Brick = 1 });
            var responses = PlayerTradeResolver.EvaluateAiResponses(game.State, offer);
            Assert.AreEqual(1, responses.Count);
            Assert.IsTrue(responses[0].Accept);
        }

        [Test]
        public void TryExecute_SwapsInventories()
        {
            var game = new GameController(6, MapSize.Small);
            game.State.PlayerInventory = new ResourceBundle { Wood = 2 };
            game.State.AiInventory = new ResourceBundle { Brick = 2 };
            var offer = new PlayerTradeOffer(
                new ResourceBundle { Wood = 1 },
                new ResourceBundle { Brick = 1 });

            Assert.IsTrue(PlayerTradeResolver.TryExecute(game.State, PlayerId.Ai, offer));
            Assert.AreEqual(1, game.State.PlayerInventory.Wood);
            Assert.AreEqual(1, game.State.PlayerInventory.Brick);
            Assert.AreEqual(1, game.State.AiInventory.Wood);
            Assert.AreEqual(1, game.State.AiInventory.Brick);
        }
    }
}
