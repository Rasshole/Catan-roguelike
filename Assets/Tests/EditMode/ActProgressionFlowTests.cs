using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Core.Yield;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    public class ActYieldTests
    {
        [Test]
        public void RollNightlyCombined_Act2_CanProduceHigherTotalsThanSinglePass()
        {
            var engine = new RollEngine(424242);
            var single = engine.RollNightly(2);
            var combined = engine.RollNightlyCombined(2, 2);

            int singleSum = single.Values.Sum();
            int combinedSum = combined.Values.Sum();

            Assert.GreaterOrEqual(combinedSum, singleSum);
            Assert.LessOrEqual(combined.Values.Count(v => v == 0), 1);
            Assert.LessOrEqual(combined.Values.Count(v => v == 2), 1);
        }

        [Test]
        public void RollNightlyCombined_Act3_AllowsMaxRollThree()
        {
            var engine = new RollEngine(777777);
            bool sawThree = false;

            for (int i = 0; i < 300; i++)
            {
                var rolls = engine.RollNightlyCombined(2, 3);
                if (rolls.Values.Any(v => v == 3))
                {
                    sawThree = true;
                    Assert.LessOrEqual(rolls.Values.Count(v => v == 3), 1);
                    break;
                }
            }

            Assert.IsTrue(sawThree, "Act 3 max roll 3 should appear in combined rolls");
        }

        [Test]
        public void Act1AndAct2Rolls_Differ_ForSameSeed()
        {
            var engine = new RollEngine(12345);
            var act1 = engine.RollNightly(2);

            var engine2 = new RollEngine(12345);
            var act2 = engine2.RollNightlyCombined(2, 2);

            CollectionAssert.AreNotEquivalent(act1, act2);
        }
    }

    public class ActEventTests
    {
        [Test]
        public void MaybeRollEvent_Act1_Seed18_ReturnsStorm()
        {
            Assert.AreEqual(EventId.Storm, new EventEngine(18).MaybeRollEvent(1));
        }

        [Test]
        public void MaybeRollEvent_Act2_HigherChanceThanAct1_ForSameSeed()
        {
            var act1 = new EventEngine(3).MaybeRollEvent(1);
            var act2 = new EventEngine(3).MaybeRollEvent(2);

            Assert.AreEqual(EventId.None, act1);
            Assert.AreNotEqual(EventId.None, act2);
        }

        [Test]
        public void PickWeightedEvent_Act3_PrefersHardEvents_OverGoodHarvest()
        {
            int hard = 0;
            int harvest = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                var id = new EventEngine(seed).MaybeRollEvent(3);
                if (id == EventId.None) continue;
                if (id is EventId.Storm or EventId.Famine or EventId.BanditRaid)
                    hard++;
                if (id == EventId.GoodHarvest)
                    harvest++;
            }

            Assert.Greater(hard, harvest);
        }
    }

    public class ActAiTests
    {
        private static readonly HexCoord BrickHex = new HexCoord(1, 0);

        private static void PlaceTwoSettlementsOnHex(BoardState board, HexCoord hex, PlayerId player)
        {
            var corner0 = VertexGraph.Canonicalize(new Vertex(hex, 0));
            var corner3 = VertexGraph.Canonicalize(new Vertex(hex, 3));
            board.VertexBuildings[corner0] = (BuildingType.Settlement, player);
            board.VertexBuildings[corner3] = (BuildingType.Settlement, player);
        }

        [Test]
        public void ExecuteNightPlan_Act2_PlaysTwoCards_WhenHandHasTwo()
        {
            var game = new GameController(99, MapSize.Small);
            game.State.Phase = GamePhase.NightAiPlan;
            game.State.Board.DayNumber = 6;
            game.State.AiHand.Clear();
            game.State.AiHand.Add(CardId.YearOfPlenty);
            game.State.AiHand.Add(CardId.YearOfPlenty);
            game.State.TodayRolls = new Dictionary<ResourceType, int>
            {
                [ResourceType.Wheat] = 2,
                [ResourceType.Wood] = 1,
                [ResourceType.Brick] = 1,
                [ResourceType.Sheep] = 1,
                [ResourceType.Stone] = 1
            };

            int wheatBefore = game.State.AiInventory.Wheat;
            game.Ai.ExecuteNightPlan(game);

            Assert.AreEqual(0, game.State.AiHand.Count);
            Assert.Greater(game.State.AiInventory.Wheat, wheatBefore);
        }

        [Test]
        public void ExecuteNightPlan_Act1_PlaysOnlyOneCard()
        {
            var game = new GameController(99, MapSize.Small);
            game.State.Phase = GamePhase.NightAiPlan;
            game.State.Board.DayNumber = 3;
            game.State.AiHand.Clear();
            game.State.AiHand.Add(CardId.YearOfPlenty);
            game.State.AiHand.Add(CardId.YearOfPlenty);
            game.State.TodayRolls = new Dictionary<ResourceType, int>
            {
                [ResourceType.Wheat] = 2,
                [ResourceType.Wood] = 1,
                [ResourceType.Brick] = 1,
                [ResourceType.Sheep] = 1,
                [ResourceType.Stone] = 1
            };

            game.Ai.ExecuteNightPlan(game);

            Assert.AreEqual(1, game.State.AiHand.Count);
        }

        [Test]
        public void ExecuteNightPlan_Act2_PicksKnight_WhenHumanOnHex()
        {
            var game = new GameController(14, MapSize.Small);
            game.State.Phase = GamePhase.NightAiPlan;
            game.State.Board.DayNumber = 7;
            PlaceTwoSettlementsOnHex(game.State.Board, BrickHex, PlayerId.Human);
            game.State.AiHand.Clear();
            game.State.AiHand.Add(CardId.YearOfPlenty);
            game.State.AiHand.Add(CardId.Knight);
            game.State.TodayRolls = new Dictionary<ResourceType, int>
            {
                [ResourceType.Wheat] = 2,
                [ResourceType.Wood] = 1,
                [ResourceType.Brick] = 1,
                [ResourceType.Sheep] = 1,
                [ResourceType.Stone] = 1
            };

            game.Ai.ExecuteNightPlan(game);

            Assert.AreEqual(BrickHex, game.State.Board.RobberTile);
        }
    }

    public class ActIntegrationTests
    {
        [Test]
        public void EndDay_WhenDayBecomesSix_ExpandsSmallMapToMedium()
        {
            var game = new GameController(7, MapSize.Small);
            game.SelectMap(MapSize.Small);
            game.State.Board.DayNumber = 5;
            game.State.Phase = GamePhase.DayPlayerActions;

            game.EndPlayerDay();

            if (game.State.Phase == GamePhase.LevelUpChoice)
                game.ChooseLevelUpPerk(game.State.PendingLevelUpChoices[0]);

            Assert.AreEqual(6, game.State.Board.DayNumber);
            Assert.AreEqual(MapSize.Medium, game.State.MapSize);
            Assert.AreEqual(13, game.State.Board.Tiles.Count);
            Assert.AreEqual(2, ActProgression.GetAct(game.State.Board.DayNumber));
            Assert.That(game.State.ActUnlockMessage, Does.Contain("Act 2"));
        }
    }
}
