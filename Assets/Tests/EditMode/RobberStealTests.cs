using CatanRoguelike.Core;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    public class RobberStealTests
    {
        private const int Seed = 4242;
        private static readonly HexCoord BrickHex = new HexCoord(1, 0);
        private static readonly HexCoord SheepHex = new HexCoord(0, -1);

        [Test]
        public void DayMoveRobber_StealsFromVictimOnBlockedTile()
        {
            var game = CreateGameWithAiOnHex(BrickHex);
            game.State.AiInventory = new ResourceBundle { Brick = 3 };
            int humanBefore = game.State.PlayerInventory.Total;
            int aiBefore = game.State.AiInventory.Total;

            Assert.IsTrue(game.MoveRobber(BrickHex, PlayerId.Human, steal: true));

            Assert.AreEqual(aiBefore - 1, game.State.AiInventory.Total, "AI should lose one resource");
            Assert.AreEqual(humanBefore + 1, game.State.PlayerInventory.Total, "Human should gain one resource");
            Assert.AreEqual(2, game.State.AiInventory.Brick, "Stolen resource should be brick from the blocked tile");
            Assert.AreEqual(1, game.State.PlayerInventory.Brick);
            Assert.IsTrue(game.State.Board.GetTile(BrickHex).HasRobber);
        }

        [Test]
        public void KnightCard_StealsFromVictimOnBlockedTile()
        {
            var game = CreateGameWithAiOnHex(BrickHex);
            game.State.AiInventory = new ResourceBundle { Brick = 2 };
            game.State.PlayerHand.Add(CardId.Knight);

            int humanBefore = game.State.PlayerInventory.Total;
            int aiBefore = game.State.AiInventory.Total;

            Assert.IsTrue(game.CardEngine.PlayCard(game.State, PlayerId.Human, CardId.Knight, robberTile: BrickHex));

            Assert.AreEqual(aiBefore - 1, game.State.AiInventory.Total);
            Assert.AreEqual(humanBefore + 1, game.State.PlayerInventory.Total);
            Assert.AreEqual(1, game.State.AiInventory.Brick);
            Assert.AreEqual(1, game.State.PlayerInventory.Brick);
            Assert.IsFalse(game.State.PlayerHand.Contains(CardId.Knight));
        }

        [Test]
        public void DayMoveRobber_NoVictimOnTile_DoesNotSteal()
        {
            var game = new GameController(Seed);
            game.State.AiInventory = new ResourceBundle { Brick = 5 };
            var beforeHuman = game.State.PlayerInventory.Total;
            var beforeAi = game.State.AiInventory.Total;

            Assert.IsTrue(game.MoveRobber(SheepHex, PlayerId.Human, steal: true));

            Assert.AreEqual(beforeHuman, game.State.PlayerInventory.Total);
            Assert.AreEqual(beforeAi, game.State.AiInventory.Total);
            Assert.IsTrue(game.State.Board.GetTile(SheepHex).HasRobber);
        }

        [Test]
        public void DayMoveRobber_VictimHasNoResources_DoesNotSteal()
        {
            var game = CreateGameWithAiOnHex(BrickHex);
            game.State.AiInventory = ResourceBundle.Zero;
            var beforeHuman = game.State.PlayerInventory.Total;

            Assert.IsTrue(game.MoveRobber(BrickHex, PlayerId.Human, steal: true));

            Assert.AreEqual(beforeHuman, game.State.PlayerInventory.Total);
            Assert.AreEqual(0, game.State.AiInventory.Total);
        }

        [Test]
        public void DayMoveRobber_IsDeterministicWithSeed()
        {
            var first = RunDayStealScenario(Seed);
            var second = RunDayStealScenario(Seed);

            Assert.AreEqual(first.HumanBrick, second.HumanBrick);
            Assert.AreEqual(first.AiBrick, second.AiBrick);
            Assert.AreEqual(first.HumanWheat, second.HumanWheat);
            Assert.AreEqual(first.AiWheat, second.AiWheat);
        }

        private static (int HumanBrick, int AiBrick, int HumanWheat, int AiWheat) RunDayStealScenario(int seed)
        {
            var game = CreateGameWithAiOnHex(BrickHex, seed);
            game.State.AiInventory = new ResourceBundle { Brick = 2, Wheat = 2 };
            game.MoveRobber(BrickHex, PlayerId.Human, steal: true);
            return (
                game.State.PlayerInventory.Brick,
                game.State.AiInventory.Brick,
                game.State.PlayerInventory.Wheat,
                game.State.AiInventory.Wheat);
        }

        private static GameController CreateGameWithAiOnHex(HexCoord hex, int seed = Seed)
        {
            var game = new GameController(seed);
            var vertex = VertexGraph.Canonicalize(new Vertex(hex, 0));
            game.State.Board.VertexBuildings[vertex] = (BuildingType.Settlement, PlayerId.Ai);
            return game;
        }
    }
}
