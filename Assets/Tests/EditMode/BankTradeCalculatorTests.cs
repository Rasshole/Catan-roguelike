using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Trade;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class BankTradeCalculatorTests
    {
        [Test]
        public void GetGiveRate_UsesBankByDefault()
        {
            var game = new GameController(1, MapSize.Small);
            Assert.AreEqual(BankTradeCalculator.BankRate,
                BankTradeCalculator.GetGiveRateForResource(game.State, PlayerId.Human, ResourceType.Wood));
        }

        [Test]
        public void TryExecute_BankTrade_SpendsFourGivesOne()
        {
            var game = new GameController(1, MapSize.Small);
            var state = game.State;
            var inv = state.PlayerInventory;
            inv.Wood = 4;
            state.PlayerInventory = inv;

            Assert.IsTrue(BankTradeCalculator.TryExecute(
                state, PlayerId.Human, ResourceType.Wood, ResourceType.Brick, 1));
            Assert.AreEqual(0, state.PlayerInventory.Wood);
            Assert.AreEqual(1, state.PlayerInventory.Brick);
        }

        [Test]
        public void CanTrade_FailsWhenInsufficientResources()
        {
            var state = new GameController(2, MapSize.Small).State;
            Assert.IsFalse(BankTradeCalculator.CanTrade(
                state, PlayerId.Human, ResourceType.Wood, ResourceType.Brick, 1));
        }
    }
}
