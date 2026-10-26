using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Yield;
using NUnit.Framework;

namespace CatanRoguelike.Tests.EditMode
{
    public class FishermenRulesTests
    {
        [Test]
        public void FishermenSpend_CostsMatchColonistTable()
        {
            Assert.AreEqual(2, FishermenRules.GetFishCost(FishermenSpendAction.MoveRobberOffBoard));
            Assert.AreEqual(3, FishermenRules.GetFishCost(FishermenSpendAction.StealFromOpponent));
            Assert.AreEqual(4, FishermenRules.GetFishCost(FishermenSpendAction.FreeRoad));
            Assert.AreEqual(5, FishermenRules.GetFishCost(FishermenSpendAction.DrawResource));
            Assert.AreEqual(7, FishermenRules.GetFishCost(FishermenSpendAction.DrawDevelopmentCard));
        }

        [Test]
        public void CreateBoard_WithFishermen_AddsFishHexesWithTokens()
        {
            var board = MapPresets.CreateBoard(MapSize.Large, 42, new MapGenerationOptions(true));
            Assert.Greater(board.SeaFishGrounds.Count, 0);
            Assert.IsTrue(board.SeaFishGrounds.Values.Any(t => t.NumberToken.HasValue));
        }

        [Test]
        public void TrySpendFish_MoveRobberOffBoard_ClearsRobber()
        {
            var game = new GameController(7, MapSize.Large);
            game.FishermenMapOption = true;
            game.SelectMap(MapSize.Large);
            game.State.FishermenOfCatanEnabled = true;
            game.State.PlayerFish = 2;
            Assert.IsTrue(game.State.Board.RobberTile.HasValue);

            Assert.IsTrue(game.TrySpendFish(FishermenSpendAction.MoveRobberOffBoard));
            Assert.IsFalse(game.State.Board.RobberTile.HasValue);
            Assert.AreEqual(0, game.State.PlayerFish);
        }
    }
}
