using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Core.Yield;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    public class EventEngineTests
    {
        private static readonly HexCoord BrickHex = new HexCoord(1, 0);
        private static readonly HexCoord StoneHex = new HexCoord(0, -1);

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

        private static void PlaceTwoSettlementsOnHex(BoardState board, HexCoord hex, PlayerId player)
        {
            var corner0 = VertexGraph.Canonicalize(new Vertex(hex, 0));
            var corner3 = VertexGraph.Canonicalize(new Vertex(hex, 3));
            board.VertexBuildings[corner0] = (BuildingType.Settlement, player);
            board.VertexBuildings[corner3] = (BuildingType.Settlement, player);
        }

        [Test]
        public void ApplyEvent_Storm_SetsStormTileOnBoard()
        {
            var state = CreateState();
            var engine = new EventEngine(18);

            engine.ApplyEvent(state, EventId.Storm);

            Assert.AreEqual(EventId.Storm, state.ActiveEvent);
            Assert.IsTrue(state.EventStormTile.HasValue);
            Assert.IsTrue(state.Board.Tiles.ContainsKey(state.EventStormTile.Value));
            Assert.That(state.EventMessage, Does.Contain("Storm"));
        }

        [Test]
        public void ApplyEvent_Storm_ProductionSkipsStormHex()
        {
            var state = CreateState();
            PlaceTwoSettlementsOnHex(state.Board, BrickHex, PlayerId.Human);
            var rolls = Rolls(0, 2, 0, 0, 0);
            state.Board.Tiles[BrickHex].NumberToken = 8;
            state.TodayDiceRolls = new List<int> { 8 };

            state.EventStormTile = BrickHex;
            state.ActiveEvent = EventId.Storm;

            var production = ProductionCalculator.CalculateForPlayer(state, PlayerId.Human, rolls);

            Assert.AreEqual(0, production.Brick,
                "Storm tile should produce nothing even when brick roll and dice match");
        }

        [Test]
        public void ApplyEvent_Famine_CapsWheatTomorrowRollAtOne()
        {
            var state = CreateState();
            state.TomorrowRolls = Rolls(1, 1, 2, 1, 1);
            var engine = new EventEngine();

            engine.ApplyEvent(state, EventId.Famine);

            Assert.AreEqual(EventId.Famine, state.ActiveEvent);
            Assert.AreEqual(1, state.TomorrowRolls[ResourceType.Wheat]);
            Assert.AreEqual(1, state.TomorrowRolls[ResourceType.Sheep],
                "Famine should only cap wheat rolls");
            Assert.That(state.EventMessage, Does.Contain("Famine"));
        }

        [Test]
        public void ApplyEvent_GoldRush_DoublesStoneProduction()
        {
            var state = CreateState();
            PlaceTwoSettlementsOnHex(state.Board, StoneHex, PlayerId.Human);
            var rolls = Rolls(0, 0, 0, 0, 1);
            state.Board.Tiles[StoneHex].NumberToken = 10;
            state.TodayDiceRolls = new List<int> { 10 };
            var engine = new EventEngine();

            engine.ApplyEvent(state, EventId.GoldRush);

            Assert.IsTrue(state.EventStoneDouble);
            var production = ProductionCalculator.CalculateForPlayer(state, PlayerId.Human, rolls);
            Assert.GreaterOrEqual(production.Stone, 2,
                "Gold Rush should double stone production when dice match token");
        }

        [Test]
        public void ApplyEvent_MarketDay_ReducesShopRateToThreeToOne()
        {
            var state = CreateState();
            var engine = new EventEngine();

            engine.ApplyEvent(state, EventId.MarketDay);

            Assert.AreEqual(1, state.EventShopBonus);
            int give = ModifierService.GetShopGiveAmount(state, PlayerId.Ai,
                ShopGenerator.BaseTradeRate, ResourceType.Wood);
            Assert.AreEqual(3, give, "Market Day should make standard 4:1 trades 3:1");
            Assert.That(state.EventMessage, Does.Contain("Market Day"));
        }

        [Test]
        public void ApplyEvent_GoodHarvest_BumpsAllRollsByOneRespectingCap()
        {
            var state = CreateState();
            state.TomorrowRolls = Rolls(0, 1, 1, 2, 1);
            var engine = new EventEngine();

            engine.ApplyEvent(state, EventId.GoodHarvest);

            Assert.AreEqual(EventId.GoodHarvest, state.ActiveEvent);
            Assert.AreEqual(1, state.TomorrowRolls[ResourceType.Wood]);
            Assert.AreEqual(2, state.TomorrowRolls[ResourceType.Brick]);
            Assert.AreEqual(2, state.TomorrowRolls[ResourceType.Wheat]);
            Assert.AreEqual(2, state.TomorrowRolls[ResourceType.Sheep],
                "Rolls already at cap 2 should stay at 2");
            Assert.AreEqual(2, state.TomorrowRolls[ResourceType.Stone]);
        }

        [Test]
        public void ApplyEvent_BanditRaid_PlacesRobberOnHumanBestTile()
        {
            var state = CreateState();
            PlaceTwoSettlementsOnHex(state.Board, BrickHex, PlayerId.Human);
            var engine = new EventEngine(14);

            engine.ApplyEvent(state, EventId.BanditRaid);

            Assert.AreEqual(EventId.BanditRaid, state.ActiveEvent);
            Assert.AreEqual(BrickHex, state.Board.RobberTile);
            Assert.That(state.EventMessage, Does.Contain("Bandit Raid"));
        }

        [Test]
        public void ClearDailyEventEffects_ClearsFlagsAndActiveEvent()
        {
            var state = CreateState();
            var engine = new EventEngine();

            engine.ApplyEvent(state, EventId.GoldRush);
            Assert.IsTrue(state.EventStoneDouble);

            engine.ClearDailyEventEffects(state);

            Assert.AreEqual(EventId.None, state.ActiveEvent);
            Assert.IsNull(state.EventStormTile);
            Assert.IsFalse(state.EventStoneDouble);
            Assert.AreEqual(0, state.EventShopBonus);
        }

        [Test]
        public void ClearDailyEventEffects_DoesNotRestoreModifiedTomorrowRolls()
        {
            var state = CreateState();
            state.TomorrowRolls = Rolls(1, 1, 2, 1, 1);
            var engine = new EventEngine();

            engine.ApplyEvent(state, EventId.Famine);
            Assert.AreEqual(1, state.TomorrowRolls[ResourceType.Wheat]);

            engine.ClearDailyEventEffects(state);

            Assert.AreEqual(1, state.TomorrowRolls[ResourceType.Wheat],
                "Famine cap should persist in rolls after event flags clear");
        }

        [Test]
        public void MaybeRollEvent_SameSeed_ProducesSameFirstOutcome()
        {
            var first = new EventEngine(18).MaybeRollEvent();
            var second = new EventEngine(18).MaybeRollEvent();

            Assert.AreEqual(first, second);
            Assert.AreEqual(EventId.Storm, first);
        }

        [Test]
        public void MaybeRollEvent_Seed1_ReturnsNone()
        {
            Assert.AreEqual(EventId.None, new EventEngine(1).MaybeRollEvent());
        }

        [Test]
        public void MaybeRollEvent_Seed22_ReturnsFamine()
        {
            Assert.AreEqual(EventId.Famine, new EventEngine(22).MaybeRollEvent());
        }

        [Test]
        public void MaybeRollEvent_Seed16_ReturnsMarketDay()
        {
            Assert.AreEqual(EventId.MarketDay, new EventEngine(16).MaybeRollEvent());
        }

        [Test]
        public void MaybeRollEvent_Seed43_ReturnsGoldRush()
        {
            Assert.AreEqual(EventId.GoldRush, new EventEngine(43).MaybeRollEvent());
        }

        [Test]
        public void MaybeRollEvent_Seed20_ReturnsGoodHarvest()
        {
            Assert.AreEqual(EventId.GoodHarvest, new EventEngine(20).MaybeRollEvent());
        }

        [Test]
        public void MaybeRollEvent_Seed14_ReturnsBanditRaid()
        {
            Assert.AreEqual(EventId.BanditRaid, new EventEngine(14).MaybeRollEvent());
        }

        [Test]
        public void MaybeRollEvent_Seed18_ReturnsStorm()
        {
            Assert.AreEqual(EventId.Storm, new EventEngine(18).MaybeRollEvent());
        }

        [Test]
        public void BeginNight_AppliesSeededEventAndModifiesTomorrowRolls()
        {
            var baseline = new RollEngine(20).RollNightly(2);
            var expected = baseline.ToDictionary(kv => kv.Key, kv => System.Math.Min(kv.Value + 1, 2));

            var game = new GameController(20);
            game.BeginNight();

            Assert.AreEqual(EventId.GoodHarvest, game.State.ActiveEvent);
            foreach (var kv in expected)
                Assert.AreEqual(kv.Value, game.State.TomorrowRolls[kv.Key],
                    $"Tomorrow roll for {kv.Key} should include Good Harvest bump");
        }

        [Test]
        public void BeginNight_Seed18_AppliesStormBeforeDayProduction()
        {
            var game = new GameController(18);
            game.BeginNight();

            Assert.AreEqual(EventId.Storm, game.State.ActiveEvent);
            Assert.IsTrue(game.State.EventStormTile.HasValue);

            game.State.TodayRolls = new Dictionary<ResourceType, int>(game.State.TomorrowRolls);

            Assert.AreEqual(EventId.Storm, game.State.ActiveEvent,
                "Storm flag should remain through the production day");
            CollectionAssert.AreEquivalent(game.State.TomorrowRolls, game.State.TodayRolls);
        }

        [Timeout(2000)]
        [Test]
        public void EndPlayerDay_ClearsActiveEventBeforeNextNight()
        {
            var game = new GameController(42);
            game.State.Phase = GamePhase.DayPlayerActions;
            game.Events.ApplyEvent(game.State, EventId.MarketDay);
            Assert.AreEqual(1, game.State.EventShopBonus);

            game.EndPlayerDay();

            Assert.AreEqual(EventId.None, game.State.ActiveEvent);
            Assert.AreEqual(0, game.State.EventShopBonus);
        }
    }
}
