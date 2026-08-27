using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core.Yield;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    public class DiceRollEngineTests
    {
        [Test]
        public void RollNightly_Act2_ReturnsTwoDiceSumsInRange()
        {
            var engine = new DiceRollEngine(12345);
            var rolls = engine.RollNightly(ActProgression.GetNightlyRollPasses(2));

            Assert.AreEqual(BalanceConfig.Act2NightlyRollPasses, rolls.Count);
            foreach (var roll in rolls)
                Assert.GreaterOrEqual(roll, 2);
            foreach (var roll in rolls)
                Assert.LessOrEqual(roll, 12);
        }
    }

    public class HybridProductionTests
    {
        private static readonly HexCoord WoodHex = new HexCoord(-1, 1);

        [Test]
        public void Production_RequiresMatchingDiceAndPositiveResourceRoll()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            board.Tiles[WoodHex].NumberToken = 8;
            var state = new GameState(board)
            {
                TodayDiceRolls = new List<int> { 8 },
                TodayRolls = new Dictionary<ResourceType, int>
                {
                    [ResourceType.Wood] = 2,
                    [ResourceType.Brick] = 0,
                    [ResourceType.Wheat] = 0,
                    [ResourceType.Sheep] = 0,
                    [ResourceType.Stone] = 0
                }
            };

            var vertex = VertexGraph.Canonicalize(new Vertex(WoodHex, 0));
            board.VertexBuildings[vertex] = (BuildingType.Settlement, PlayerId.Human);

            var production = ProductionCalculator.CalculateForPlayer(state, PlayerId.Human, state.TodayRolls);
            Assert.AreEqual(2, production.Wood);
        }

        [Test]
        public void Production_NoDiceMatch_YieldsNothing()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            board.Tiles[WoodHex].NumberToken = 8;
            var state = new GameState(board)
            {
                TodayDiceRolls = new List<int> { 3 },
                TodayRolls = new Dictionary<ResourceType, int>
                {
                    [ResourceType.Wood] = 2,
                    [ResourceType.Brick] = 0,
                    [ResourceType.Wheat] = 0,
                    [ResourceType.Sheep] = 0,
                    [ResourceType.Stone] = 0
                }
            };

            var vertex = VertexGraph.Canonicalize(new Vertex(WoodHex, 0));
            board.VertexBuildings[vertex] = (BuildingType.Settlement, PlayerId.Human);

            var production = ProductionCalculator.CalculateForPlayer(state, PlayerId.Human, state.TodayRolls);
            Assert.AreEqual(0, production.Wood);
        }

        [Test]
        public void Production_ZeroResourceRoll_StillYieldsOneWhenDiceMatch()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            board.Tiles[WoodHex].NumberToken = 8;
            var state = new GameState(board)
            {
                TodayDiceRolls = new List<int> { 8 },
                TodayRolls = new Dictionary<ResourceType, int>
                {
                    [ResourceType.Wood] = 0,
                    [ResourceType.Brick] = 0,
                    [ResourceType.Wheat] = 0,
                    [ResourceType.Sheep] = 0,
                    [ResourceType.Stone] = 0
                }
            };

            var vertex = VertexGraph.Canonicalize(new Vertex(WoodHex, 0));
            board.VertexBuildings[vertex] = (BuildingType.Settlement, PlayerId.Human);

            var production = ProductionCalculator.CalculateForPlayer(state, PlayerId.Human, state.TodayRolls);
            Assert.AreEqual(1, production.Wood, "Dice match floors yield at 1 even when weather roll is 0");
        }

        [Test]
        public void Production_DesertHex_NeverProduces()
        {
            var board = MapPresets.CreateBoard(MapSize.Large);
            var desert = new HexCoord(0, 0);
            board.Tiles[desert].IsDesert = true;
            board.Tiles[desert].NumberToken = null;

            var state = new GameState(board)
            {
                TodayDiceRolls = new List<int> { 2, 3 },
                TodayRolls = new Dictionary<ResourceType, int>
                {
                    [ResourceType.Stone] = 2,
                    [ResourceType.Wood] = 0,
                    [ResourceType.Brick] = 0,
                    [ResourceType.Wheat] = 0,
                    [ResourceType.Sheep] = 0
                }
            };

            var vertex = VertexGraph.Canonicalize(new Vertex(desert, 0));
            board.VertexBuildings[vertex] = (BuildingType.Settlement, PlayerId.Human);

            foreach (var hex in VertexGraph.GetHexesForVertex(vertex))
            {
                if (board.TryGetTile(hex, out var tile) && !tile.IsDesert)
                    tile.NumberToken = null;
            }

            var production = ProductionCalculator.CalculateForPlayer(state, PlayerId.Human, state.TodayRolls);
            Assert.AreEqual(0, production.Total);
        }

        [Test]
        public void Production_Act2DoubleDice_CanProduceTwiceOnSameToken()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            board.Tiles[WoodHex].NumberToken = 6;
            var state = new GameState(board)
            {
                TodayDiceRolls = new List<int> { 6, 6 },
                TodayRolls = new Dictionary<ResourceType, int>
                {
                    [ResourceType.Wood] = 1,
                    [ResourceType.Brick] = 0,
                    [ResourceType.Wheat] = 0,
                    [ResourceType.Sheep] = 0,
                    [ResourceType.Stone] = 0
                }
            };

            var vertex = VertexGraph.Canonicalize(new Vertex(WoodHex, 0));
            board.VertexBuildings[vertex] = (BuildingType.Settlement, PlayerId.Human);

            var production = ProductionCalculator.CalculateForPlayer(state, PlayerId.Human, state.TodayRolls);
            Assert.AreEqual(2, production.Wood, "Two matching dice passes should stack yield");
        }
    }
}
