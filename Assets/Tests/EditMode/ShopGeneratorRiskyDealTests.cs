using CatanRoguelike.Core;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    public class ShopGeneratorRiskyDealTests
    {
        private static readonly HexCoord BrickHex = new HexCoord(1, 0);
        private static readonly HexCoord SheepHex = new HexCoord(0, -1);
        private static readonly HexCoord RobberStart = new HexCoord(1, -1);

        private static ShopDeal CreateRiskyDeal(ResourceType give = ResourceType.Wood) =>
            new ShopDeal(give, ShopGenerator.RiskyTradeRate, ResourceType.Brick, 1,
                risky: true, riskDescription: ShopDealDisplay.RiskyRobberConsequence);

        [Test]
        public void TryPurchase_RiskyDeal_Human_PlacesRobberOnHumanBestTile()
        {
            var game = new GameController(42);
            PlaceTwoSettlementsOnHex(game.State.Board, BrickHex, PlayerId.Human);
            game.State.PlayerInventory = new ResourceBundle { Wood = ShopGenerator.RiskyTradeRate };

            Assert.IsTrue(game.Shop.TryPurchase(game.State, PlayerId.Human, CreateRiskyDeal()));
            Assert.AreEqual(BrickHex, game.State.Board.RobberTile);
        }

        [Test]
        public void TryPurchase_RiskyDeal_Ai_PlacesRobberOnAiBestTile()
        {
            var game = new GameController(42);
            PlaceTwoSettlementsOnHex(game.State.Board, SheepHex, PlayerId.Ai);
            game.State.AiInventory = new ResourceBundle { Wood = ShopGenerator.RiskyTradeRate };

            Assert.IsTrue(game.Shop.TryPurchase(game.State, PlayerId.Ai, CreateRiskyDeal()));
            Assert.AreEqual(SheepHex, game.State.Board.RobberTile);
        }

        [Test]
        public void TryPurchase_RiskyDeal_HumanWithRiskyDealsSafe_SkipsPenalty()
        {
            var game = new GameController(42);
            PlaceTwoSettlementsOnHex(game.State.Board, BrickHex, PlayerId.Human);
            game.State.AcquiredPerks.Add(LevelUpPerkId.RiskyDealsSafe);
            game.State.PlayerInventory = new ResourceBundle { Wood = ShopGenerator.RiskyTradeRate };

            Assert.IsTrue(game.Shop.TryPurchase(game.State, PlayerId.Human, CreateRiskyDeal()));
            Assert.AreEqual(RobberStart, game.State.Board.RobberTile);
        }

        [Test]
        public void TryPurchase_RiskyDeal_AiStillPenalized_WhenHumanHasRiskyDealsSafe()
        {
            var game = new GameController(42);
            PlaceTwoSettlementsOnHex(game.State.Board, SheepHex, PlayerId.Ai);
            game.State.AcquiredPerks.Add(LevelUpPerkId.RiskyDealsSafe);
            game.State.AiInventory = new ResourceBundle { Wood = ShopGenerator.RiskyTradeRate };

            Assert.IsTrue(game.Shop.TryPurchase(game.State, PlayerId.Ai, CreateRiskyDeal()));
            Assert.AreEqual(SheepHex, game.State.Board.RobberTile);
        }

        private static void PlaceTwoSettlementsOnHex(BoardState board, HexCoord hex, PlayerId player)
        {
            var corner0 = VertexGraph.Canonicalize(new Vertex(hex, 0));
            var corner3 = VertexGraph.Canonicalize(new Vertex(hex, 3));
            board.VertexBuildings[corner0] = (BuildingType.Settlement, player);
            board.VertexBuildings[corner3] = (BuildingType.Settlement, player);
        }
    }
}
