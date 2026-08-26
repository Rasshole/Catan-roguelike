using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Yield;
using NUnit.Framework;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    public class CardEngineTests
    {
        private const int Seed = 42;

        private static GameState CreateState()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            return new GameState(board);
        }

        private static Dictionary<ResourceType, int> Rolls(
            int wood, int brick, int wheat, int sheep, int stone) =>
            new()
            {
                [ResourceType.Wood] = wood,
                [ResourceType.Brick] = brick,
                [ResourceType.Wheat] = wheat,
                [ResourceType.Sheep] = sheep,
                [ResourceType.Stone] = stone
            };

        private static void AddToHand(GameState state, PlayerId player, CardId card)
        {
            var hand = player == PlayerId.Human ? state.PlayerHand : state.AiHand;
            hand.Add(card);
        }

        [Test]
        public void CardLibrary_AllCards_ContainsEveryCardId()
        {
            var expected = System.Enum.GetValues(typeof(CardId)).Cast<CardId>().ToList();
            CollectionAssert.AreEquivalent(expected, CardLibrary.AllCards);
        }

        [Test]
        public void PlayCard_NotInHand_ReturnsFalse()
        {
            var state = CreateState();
            var engine = new CardEngine(Seed);

            Assert.IsFalse(engine.PlayCard(state, PlayerId.Human, CardId.YearOfPlenty, ResourceType.Wood));
            Assert.AreEqual(0, state.PlayerInventory.Wood);
        }

        [Test]
        public void DrawToHand_RespectsMaxHandSize()
        {
            var state = CreateState();
            var engine = new CardEngine(Seed);

            engine.DrawToHand(state, PlayerId.Human, BalanceConfig.MaxHandSize + 3);

            Assert.AreEqual(BalanceConfig.MaxHandSize, state.PlayerHand.Count);
        }

        [Test]
        public void DrawCard_ForAi_NeverReturnsHumanOnlyCards()
        {
            var engine = new CardEngine(Seed);

            for (int i = 0; i < 300; i++)
            {
                var card = engine.DrawCard(forAi: true);
                Assert.IsTrue(CardLibrary.Get(card).AiCanUse,
                    $"AI draw returned human-only card {card}");
                CollectionAssert.DoesNotContain(
                    new[] { CardId.HarborCharter },
                    card);
            }
        }

        [Test]
        public void MerchantsLedger_SetsZeroRollToOne()
        {
            var state = CreateState();
            state.TomorrowRolls = Rolls(0, 1, 1, 1, 1);
            AddToHand(state, PlayerId.Human, CardId.MerchantsLedger);
            var engine = new CardEngine(Seed);

            Assert.IsTrue(engine.PlayCard(state, PlayerId.Human, CardId.MerchantsLedger, ResourceType.Wood));

            Assert.AreEqual(1, state.TomorrowRolls[ResourceType.Wood]);
            Assert.IsFalse(state.PlayerHand.Contains(CardId.MerchantsLedger));
        }

        [Test]
        public void MerchantsLedger_LeavesNonZeroRollUnchanged()
        {
            var state = CreateState();
            state.TomorrowRolls = Rolls(2, 1, 1, 1, 1);
            AddToHand(state, PlayerId.Human, CardId.MerchantsLedger);
            var engine = new CardEngine(Seed);

            Assert.IsTrue(engine.PlayCard(state, PlayerId.Human, CardId.MerchantsLedger, ResourceType.Wood));

            Assert.AreEqual(2, state.TomorrowRolls[ResourceType.Wood]);
        }

        [Test]
        public void MerchantsLedger_WithoutResource_FailsAndKeepsCard()
        {
            var state = CreateState();
            state.TomorrowRolls = Rolls(0, 1, 1, 1, 1);
            AddToHand(state, PlayerId.Human, CardId.MerchantsLedger);
            var engine = new CardEngine(Seed);

            Assert.IsFalse(engine.PlayCard(state, PlayerId.Human, CardId.MerchantsLedger));
            Assert.AreEqual(0, state.TomorrowRolls[ResourceType.Wood]);
            Assert.IsTrue(state.PlayerHand.Contains(CardId.MerchantsLedger));
        }

        [Test]
        public void Drought_CapsRollAboveOne()
        {
            var state = CreateState();
            state.TomorrowRolls = Rolls(2, 1, 1, 1, 1);
            AddToHand(state, PlayerId.Human, CardId.Drought);
            var engine = new CardEngine(Seed);

            Assert.IsTrue(engine.PlayCard(state, PlayerId.Human, CardId.Drought, ResourceType.Wood));

            Assert.AreEqual(1, state.TomorrowRolls[ResourceType.Wood]);
            Assert.IsFalse(state.PlayerHand.Contains(CardId.Drought));
        }

        [Test]
        public void Drought_LeavesRollAtOneOrBelow()
        {
            var state = CreateState();
            state.TomorrowRolls = Rolls(0, 1, 1, 1, 1);
            AddToHand(state, PlayerId.Human, CardId.Drought);
            var engine = new CardEngine(Seed);

            Assert.IsTrue(engine.PlayCard(state, PlayerId.Human, CardId.Drought, ResourceType.Wood));

            Assert.AreEqual(0, state.TomorrowRolls[ResourceType.Wood]);
        }

        [Test]
        public void Drought_WithoutResource_FailsAndKeepsCard()
        {
            var state = CreateState();
            state.TomorrowRolls = Rolls(2, 1, 1, 1, 1);
            AddToHand(state, PlayerId.Human, CardId.Drought);
            var engine = new CardEngine(Seed);

            Assert.IsFalse(engine.PlayCard(state, PlayerId.Human, CardId.Drought));
            Assert.AreEqual(2, state.TomorrowRolls[ResourceType.Wood]);
            Assert.IsTrue(state.PlayerHand.Contains(CardId.Drought));
        }

        [Test]
        public void FertileSeason_IncrementsRoll_CappedAtTwo()
        {
            var state = CreateState();
            state.TomorrowRolls = Rolls(1, 1, 1, 1, 1);
            AddToHand(state, PlayerId.Human, CardId.FertileSeason);
            var engine = new CardEngine(Seed);

            Assert.IsTrue(engine.PlayCard(state, PlayerId.Human, CardId.FertileSeason, ResourceType.Wood));

            Assert.AreEqual(2, state.TomorrowRolls[ResourceType.Wood]);
        }

        [Test]
        public void FertileSeason_AtCap_StaysAtTwo()
        {
            var state = CreateState();
            state.TomorrowRolls = Rolls(2, 1, 1, 1, 1);
            AddToHand(state, PlayerId.Human, CardId.FertileSeason);
            var engine = new CardEngine(Seed);

            Assert.IsTrue(engine.PlayCard(state, PlayerId.Human, CardId.FertileSeason, ResourceType.Wood));

            Assert.AreEqual(2, state.TomorrowRolls[ResourceType.Wood]);
        }

        [Test]
        public void FertileSeason_WithoutResource_FailsAndKeepsCard()
        {
            var state = CreateState();
            state.TomorrowRolls = Rolls(1, 1, 1, 1, 1);
            AddToHand(state, PlayerId.Human, CardId.FertileSeason);
            var engine = new CardEngine(Seed);

            Assert.IsFalse(engine.PlayCard(state, PlayerId.Human, CardId.FertileSeason));
            Assert.AreEqual(1, state.TomorrowRolls[ResourceType.Wood]);
            Assert.IsTrue(state.PlayerHand.Contains(CardId.FertileSeason));
        }

        [Test]
        public void YearOfPlenty_AddsTwoResources()
        {
            var state = CreateState();
            AddToHand(state, PlayerId.Human, CardId.YearOfPlenty);
            var engine = new CardEngine(Seed);

            Assert.IsTrue(engine.PlayCard(state, PlayerId.Human, CardId.YearOfPlenty, ResourceType.Brick));

            Assert.AreEqual(2, state.PlayerInventory.Brick);
            Assert.IsFalse(state.PlayerHand.Contains(CardId.YearOfPlenty));
        }

        [Test]
        public void YearOfPlenty_WithoutResource_FailsAndKeepsCard()
        {
            var state = CreateState();
            AddToHand(state, PlayerId.Human, CardId.YearOfPlenty);
            var engine = new CardEngine(Seed);

            Assert.IsFalse(engine.PlayCard(state, PlayerId.Human, CardId.YearOfPlenty));
            Assert.AreEqual(0, state.PlayerInventory.Total);
            Assert.IsTrue(state.PlayerHand.Contains(CardId.YearOfPlenty));
        }

        [Test]
        public void Monopoly_StealsHalfRoundedUp()
        {
            var state = CreateState();
            state.AiInventory = new ResourceBundle { Wheat = 5 };
            AddToHand(state, PlayerId.Human, CardId.Monopoly);
            var engine = new CardEngine(Seed);

            Assert.IsTrue(engine.PlayCard(state, PlayerId.Human, CardId.Monopoly, ResourceType.Wheat));

            Assert.AreEqual(3, state.PlayerInventory.Wheat);
            Assert.AreEqual(2, state.AiInventory.Wheat);
            Assert.IsFalse(state.PlayerHand.Contains(CardId.Monopoly));
        }

        [Test]
        public void Monopoly_WithMonopolyFullPerk_StealsAll()
        {
            var state = CreateState();
            state.AcquiredPerks.Add(LevelUpPerkId.MonopolyFull);
            state.AiInventory = new ResourceBundle { Sheep = 4 };
            AddToHand(state, PlayerId.Human, CardId.Monopoly);
            var engine = new CardEngine(Seed);

            Assert.IsTrue(engine.PlayCard(state, PlayerId.Human, CardId.Monopoly, ResourceType.Sheep));

            Assert.AreEqual(4, state.PlayerInventory.Sheep);
            Assert.AreEqual(0, state.AiInventory.Sheep);
        }

        [Test]
        public void Monopoly_OpponentHasZero_StillConsumesCard()
        {
            var state = CreateState();
            state.AiInventory = ResourceBundle.Zero;
            AddToHand(state, PlayerId.Human, CardId.Monopoly);
            var engine = new CardEngine(Seed);

            Assert.IsTrue(engine.PlayCard(state, PlayerId.Human, CardId.Monopoly, ResourceType.Stone));

            Assert.AreEqual(0, state.PlayerInventory.Stone);
            Assert.IsFalse(state.PlayerHand.Contains(CardId.Monopoly));
        }

        [Test]
        public void Monopoly_WithoutResource_FailsAndKeepsCard()
        {
            var state = CreateState();
            state.AiInventory = new ResourceBundle { Wood = 3 };
            AddToHand(state, PlayerId.Human, CardId.Monopoly);
            var engine = new CardEngine(Seed);

            Assert.IsFalse(engine.PlayCard(state, PlayerId.Human, CardId.Monopoly));
            Assert.AreEqual(3, state.AiInventory.Wood);
            Assert.IsTrue(state.PlayerHand.Contains(CardId.Monopoly));
        }

        [Test]
        public void RoadBuilder_SetsPendingAndOneFreeCharge()
        {
            var state = CreateState();
            AddToHand(state, PlayerId.Human, CardId.RoadBuilder);
            var engine = new CardEngine(Seed);

            Assert.IsTrue(engine.PlayCard(state, PlayerId.Human, CardId.RoadBuilder));

            Assert.AreEqual(CardId.RoadBuilder, state.PendingCard);
            Assert.AreEqual(1, state.FreeRoadCharges);
            Assert.IsFalse(state.PlayerHand.Contains(CardId.RoadBuilder));
        }

        [Test]
        public void RoadBuilder_WithDoubleRoadBuilderPerk_GivesTwoCharges()
        {
            var state = CreateState();
            state.AcquiredPerks.Add(LevelUpPerkId.DoubleRoadBuilder);
            AddToHand(state, PlayerId.Human, CardId.RoadBuilder);
            var engine = new CardEngine(Seed);

            Assert.IsTrue(engine.PlayCard(state, PlayerId.Human, CardId.RoadBuilder));

            Assert.AreEqual(2, state.FreeRoadCharges);
        }

        [Test]
        public void MasterBuilder_PlayCard_SetsPendingCard()
        {
            var state = CreateState();
            AddToHand(state, PlayerId.Human, CardId.MasterBuilder);
            var engine = new CardEngine(Seed);

            Assert.IsTrue(engine.PlayCard(state, PlayerId.Human, CardId.MasterBuilder));

            Assert.AreEqual(CardId.MasterBuilder, state.PendingCard);
            Assert.IsFalse(state.PlayerHand.Contains(CardId.MasterBuilder));
        }

        [Test]
        public void Forecast_RerollsAllTomorrowRolls_Seeded()
        {
            var state = CreateState();
            state.TomorrowRolls = Rolls(0, 2, 1, 0, 2);
            AddToHand(state, PlayerId.Human, CardId.Forecast);
            var expected = new RollEngine(Seed).RollNightly(2);
            var engine = new CardEngine(Seed);

            Assert.IsTrue(engine.PlayCard(state, PlayerId.Human, CardId.Forecast));

            CollectionAssert.AreEquivalent(expected, state.TomorrowRolls);
            Assert.IsFalse(state.PlayerHand.Contains(CardId.Forecast));
        }

        [Test]
        public void Forecast_IsDeterministicWithSeed()
        {
            var first = RunForecastScenario(Seed);
            var second = RunForecastScenario(Seed);

            CollectionAssert.AreEquivalent(first, second);
        }

        [Test]
        public void HarborCharter_SetsHarborCharterPending()
        {
            var state = CreateState();
            AddToHand(state, PlayerId.Human, CardId.HarborCharter);
            var engine = new CardEngine(Seed);

            Assert.IsTrue(engine.PlayCard(state, PlayerId.Human, CardId.HarborCharter));

            Assert.IsTrue(state.HarborCharterPending);
            Assert.IsFalse(state.PlayerHand.Contains(CardId.HarborCharter));
        }

        [Test]
        public void Embargo_WithoutResource_FailsAndKeepsCard()
        {
            var state = CreateState();
            AddToHand(state, PlayerId.Human, CardId.Embargo);
            var engine = new CardEngine(Seed);

            Assert.IsFalse(engine.PlayCard(state, PlayerId.Human, CardId.Embargo));
            Assert.IsFalse(state.AiShopEmbargo.HasValue);
            Assert.IsTrue(state.PlayerHand.Contains(CardId.Embargo));
        }

        [Test]
        public void Embargo_WithEmbargoExtendedPerk_BlocksTwoDays()
        {
            var state = CreateState();
            state.AcquiredPerks.Add(LevelUpPerkId.EmbargoExtended);
            AddToHand(state, PlayerId.Human, CardId.Embargo);
            var engine = new CardEngine(Seed);

            Assert.IsTrue(engine.PlayCard(state, PlayerId.Human, CardId.Embargo, ResourceType.Brick));

            Assert.AreEqual(ResourceType.Brick, state.AiShopEmbargo);
            Assert.AreEqual(2, state.AiEmbargoDaysLeft);
        }

        [Test]
        public void Knight_InvalidTarget_FailsAndKeepsCard()
        {
            var state = CreateState();
            AddToHand(state, PlayerId.Human, CardId.Knight);
            var engine = new CardEngine(Seed);
            var offBoardHex = new HexCoord(99, 99);

            Assert.IsFalse(engine.PlayCard(state, PlayerId.Human, CardId.Knight, robberTarget: offBoardHex));
            Assert.IsTrue(state.PlayerHand.Contains(CardId.Knight));
            Assert.IsFalse(state.Board.TryGetTile(offBoardHex, out _));
        }

        [Test]
        public void Knight_WithKnightMovesRobberTwice_DisablesRandomAiRoad()
        {
            var state = CreateState();
            var hex = new HexCoord(0, 0);
            var edge = VertexGraph.GetEdgeBetween(
                VertexGraph.Canonicalize(new Vertex(hex, 0)),
                VertexGraph.Canonicalize(new Vertex(hex, 1)));
            state.Board.Roads[edge] = PlayerId.Ai;
            state.AcquiredPerks.Add(LevelUpPerkId.KnightMovesRobberTwice);
            AddToHand(state, PlayerId.Human, CardId.Knight);
            var engine = new CardEngine(Seed);

            Assert.IsTrue(engine.PlayCard(state, PlayerId.Human, CardId.Knight, robberTarget: hex));

            Assert.AreEqual(1, state.Board.DisabledRoads.Count);
            CollectionAssert.Contains(state.Board.DisabledRoads, edge);
        }

        [Test]
        public void BanditRaid_OnHumanRoad_FailsAndKeepsCard()
        {
            var state = CreateState();
            var hex = new HexCoord(0, 0);
            var edge = VertexGraph.GetEdgeBetween(
                VertexGraph.Canonicalize(new Vertex(hex, 0)),
                VertexGraph.Canonicalize(new Vertex(hex, 1)));
            state.Board.Roads[edge] = PlayerId.Human;
            AddToHand(state, PlayerId.Human, CardId.BanditRaid);
            var engine = new CardEngine(Seed);

            Assert.IsFalse(engine.PlayCard(state, PlayerId.Human, CardId.BanditRaid, roadTarget: edge));
            Assert.AreEqual(0, state.Board.DisabledRoads.Count);
            Assert.IsTrue(state.PlayerHand.Contains(CardId.BanditRaid));
        }

        private static Dictionary<ResourceType, int> RunForecastScenario(int seed)
        {
            var state = CreateState();
            state.TomorrowRolls = Rolls(0, 2, 1, 0, 2);
            AddToHand(state, PlayerId.Human, CardId.Forecast);
            new CardEngine(seed).PlayCard(state, PlayerId.Human, CardId.Forecast);
            return state.TomorrowRolls;
        }
    }
}
