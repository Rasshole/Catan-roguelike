using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Civilizations;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Save;
using CatanRoguelike.Core.Trade;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Core.Yield;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    /// <summary>v131: civilizations (pick + passives/drawbacks) and the data-driven event deck.</summary>
    public class CivilizationAndEventV131Tests
    {
        private static readonly HexCoord BrickHex = new(1, 0);
        private static readonly HexCoord StoneHex = new(-1, 0);

        private static GameState CreateState(CivilizationId civ = CivilizationId.None)
        {
            var state = new GameState(MapPresets.CreateBoard(MapSize.Small)) { Civilization = civ };
            return state;
        }

        private static Dictionary<ResourceType, int> Rolls(int v) =>
            ResourceTypes.Productive.ToDictionary(r => r, _ => v);

        private static void PlaceTwoSettlementsOnHex(BoardState board, HexCoord hex, PlayerId player)
        {
            board.VertexBuildings[VertexGraph.Canonicalize(new Vertex(hex, 0))] = (BuildingType.Settlement, player);
            board.VertexBuildings[VertexGraph.Canonicalize(new Vertex(hex, 3))] = (BuildingType.Settlement, player);
        }

        private static HexCoord FindTile(GameState state, ResourceType resource) =>
            state.Board.Tiles.Values.First(t => t.Resource == resource && !t.IsDesert && !t.IsFishTile).Coord;

        private static ResourceBundle ProduceOn(GameState state, HexCoord hex, int token = 8)
        {
            state.Board.Tiles[hex].NumberToken = token;
            if (state.Board.RobberTile == hex)
                state.Board.PlaceRobber(state.Board.Tiles.Values.First(t => !t.Coord.Equals(hex)).Coord);
            state.TodayDiceRolls = new List<int> { token };
            return ProductionCalculator.CalculateForPlayer(state, PlayerId.Human, Rolls(1));
        }

        private static GameController ControllerAtCivPick(int seed = 7)
        {
            var game = new GameController(seed, MapSize.Small) { CivilizationPickEnabled = true };
            game.SelectMap(MapSize.Small);
            game.SelectLeader(LeaderId.Merchant);
            var uniques = (UniqueBuildingId[])Enum.GetValues(typeof(UniqueBuildingId));
            game.ToggleDraftUnique(uniques[0]);
            game.ToggleDraftUnique(uniques[1]);
            game.ConfirmRunSetup();
            return game;
        }

        // ---------------- library + offer ----------------

        [Test]
        public void Pool_HasTenDistinctCivilizations_WithTextAndArtKeys()
        {
            Assert.AreEqual(10, CivilizationLibrary.Pool.Count);
            Assert.AreEqual(10, CivilizationLibrary.Pool.Select(c => c.Id).Distinct().Count());
            foreach (var civ in CivilizationLibrary.Pool)
            {
                Assert.AreNotEqual(CivilizationId.None, civ.Id);
                Assert.IsNotEmpty(civ.Name);
                Assert.IsNotEmpty(civ.Tagline);
                Assert.IsNotEmpty(civ.Passive);
                Assert.IsNotEmpty(civ.EmblemKey);
                Assert.LessOrEqual(civ.Passive.Length, 110, civ.Name + " passive should fit on a card");
            }
        }

        [Test]
        public void Offer_IsDeterministicPerSeed_AndThreeDistinct()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var a = CivilizationOffer.Generate(seed);
                var b = CivilizationOffer.Generate(seed);
                CollectionAssert.AreEqual(a, b);
                Assert.AreEqual(3, a.Count);
                Assert.AreEqual(3, a.Distinct().Count());
                Assert.IsFalse(a.Contains(CivilizationId.None));
            }
        }

        [Test]
        public void Offer_VariesAcrossSeeds_AndCoversWholePool()
        {
            var seen = new HashSet<CivilizationId>();
            var offers = new HashSet<string>();
            for (int seed = 0; seed < 60; seed++)
            {
                var offer = CivilizationOffer.Generate(seed);
                seen.UnionWith(offer);
                offers.Add(string.Join(",", offer));
            }

            Assert.AreEqual(10, seen.Count);
            Assert.Greater(offers.Count, 20);
        }

        // ---------------- pick flow ----------------

        [Timeout(4000)]
        [Test]
        public void ConfirmRunSetup_WithPickEnabled_OffersThreeAndWaits()
        {
            var game = ControllerAtCivPick();

            Assert.AreEqual(GamePhase.RunSelectCivilization, game.State.Phase);
            Assert.AreEqual(3, game.State.CivilizationOffer.Count);
            Assert.IsTrue(game.State.IsSetupPhase);
            Assert.AreEqual(0, game.State.Board.CountBuildings(PlayerId.Ai, BuildingType.Settlement),
                "Board setup must wait for the civilization pick");
        }

        [Timeout(4000)]
        [Test]
        public void ChooseCivilization_RejectsUnofferedAndStartsBoardSetup()
        {
            var game = ControllerAtCivPick();
            var notOffered = CivilizationLibrary.Pool.Select(c => c.Id)
                .First(id => !game.State.CivilizationOffer.Contains(id));

            Assert.IsFalse(game.ChooseCivilization(notOffered));
            Assert.AreEqual(GamePhase.RunSelectCivilization, game.State.Phase);

            var pick = game.State.CivilizationOffer[1];
            Assert.IsTrue(game.ChooseCivilization(pick));
            Assert.AreEqual(pick, game.State.Civilization);
            Assert.AreEqual(GamePhase.SetupPlayerSettlement1, game.State.Phase);
            Assert.IsFalse(game.ChooseCivilization(pick), "Cannot pick twice");
        }

        [Timeout(2000)]
        [Test]
        public void ConfirmRunSetup_WithPickDisabled_KeepsLegacyFlow()
        {
            var game = new GameController(seed: 1, MapSize.Small);
            game.SelectMap(MapSize.Small);
            game.SelectLeader(LeaderId.Merchant);
            game.ToggleDraftUnique(UniqueBuildingId.Sawmill);
            game.ToggleDraftUnique(UniqueBuildingId.GuildHall);
            game.ConfirmRunSetup();

            Assert.AreEqual(GamePhase.SetupPlayerSettlement1, game.State.Phase);
            Assert.AreEqual(CivilizationId.None, game.State.Civilization);
        }

        [Test]
        public void Shepherd_StartBonus_GivesTwoWool()
        {
            var state = CreateState(CivilizationId.ShepherdKingdom);
            CivilizationRules.ApplyStartBonus(state);
            Assert.AreEqual(CivilizationRules.ShepherdStartWool, state.PlayerInventory.Sheep);

            var other = CreateState(CivilizationId.SeaFolk);
            CivilizationRules.ApplyStartBonus(other);
            Assert.AreEqual(0, other.PlayerInventory.Total);
        }

        // ---------------- build costs ----------------

        private static ResourceBundle Cost(CivilizationId civ, ResourceBundle baseCost, bool s, bool c, bool r,
            PlayerId player = PlayerId.Human) =>
            CivilizationRules.ApplyCostModifiers(CreateState(civ), player, baseCost, s, c, r);

        [Test]
        public void Nomads_SettlementsNoBrick_CitiesOneMoreOre()
        {
            Assert.AreEqual(0, Cost(CivilizationId.SteppeNomads, BalanceConfig.SettlementCost, true, false, false).Brick);
            Assert.AreEqual(BalanceConfig.SettlementCost.Wood,
                Cost(CivilizationId.SteppeNomads, BalanceConfig.SettlementCost, true, false, false).Wood);
            Assert.AreEqual(BalanceConfig.CityCost.Stone + 1,
                Cost(CivilizationId.SteppeNomads, BalanceConfig.CityCost, false, true, false).Stone);
        }

        [Test]
        public void Brickwrights_RoadsNoLumber()
        {
            var cost = Cost(CivilizationId.Brickwrights, BalanceConfig.RoadCost, false, false, true);
            Assert.AreEqual(0, cost.Wood);
            Assert.AreEqual(BalanceConfig.RoadCost.Brick, cost.Brick);
        }

        [Test]
        public void Shepherd_CitiesOneMoreWheat_Academy_SettlementsOneMoreWheat()
        {
            Assert.AreEqual(BalanceConfig.CityCost.Wheat + 1,
                Cost(CivilizationId.ShepherdKingdom, BalanceConfig.CityCost, false, true, false).Wheat);
            Assert.AreEqual(BalanceConfig.SettlementCost.Wheat + 1,
                Cost(CivilizationId.Academy, BalanceConfig.SettlementCost, true, false, false).Wheat);
        }

        [Test]
        public void CostModifiers_NeverAffectTheAi()
        {
            var cost = Cost(CivilizationId.SteppeNomads, BalanceConfig.SettlementCost, true, false, false, PlayerId.Ai);
            Assert.AreEqual(BalanceConfig.SettlementCost.Brick, cost.Brick);
        }

        [Test]
        public void CostModifiers_FlowThroughModifierService()
        {
            var state = CreateState(CivilizationId.Brickwrights);
            var cost = ModifierService.ApplyLeaderCostModifiers(state, PlayerId.Human, BalanceConfig.RoadCost,
                isSettlement: false, isCity: false, isRoad: true);
            Assert.AreEqual(0, cost.Wood);
        }

        // ---------------- trade ----------------

        [Test]
        public void Merchant_BankRateIsThree_RaidersFive_DefaultFour()
        {
            var resource = ResourceType.Wheat;
            int Rate(CivilizationId civ)
            {
                var state = CreateState(civ);
                state.Ports.Clear();
                return BankTradeCalculator.GetGiveRateForResource(state, PlayerId.Human, resource);
            }

            Assert.AreEqual(BankTradeCalculator.BankRate, Rate(CivilizationId.None));
            Assert.AreEqual(CivilizationRules.MerchantBankRate, Rate(CivilizationId.MerchantRepublic));
            Assert.AreEqual(CivilizationRules.RaiderBankRate, Rate(CivilizationId.NorthernRaiders));
        }

        [Test]
        public void BaseBankRate_NeverWorsensPortsForMerchant_AndIgnoresAi()
        {
            var state = CreateState(CivilizationId.MerchantRepublic);
            Assert.AreEqual(2, CivilizationRules.BaseBankRate(state, PlayerId.Human, 2));
            Assert.AreEqual(4, CivilizationRules.BaseBankRate(state, PlayerId.Ai, 4));
        }

        // ---------------- production ----------------

        [Test]
        public void Highland_MountainsPlusOneOrePerHit_PasturesMinusOne()
        {
            var baseline = CreateState();
            var highland = CreateState(CivilizationId.HighlandClans);
            var stone = FindTile(baseline, ResourceType.Stone);
            PlaceTwoSettlementsOnHex(baseline.Board, stone, PlayerId.Human);
            PlaceTwoSettlementsOnHex(highland.Board, stone, PlayerId.Human);

            int baseOre = ProduceOn(baseline, stone).Stone;
            int civOre = ProduceOn(highland, stone).Stone;
            Assert.Greater(baseOre, 0);
            Assert.AreEqual(baseOre + 2, civOre, "+1 ore per settlement hit (two settlements)");

            Assert.AreEqual(-1, CivilizationRules.HexYieldDeltaPerHit(highland, PlayerId.Human, ResourceType.Sheep));
            Assert.AreEqual(0, CivilizationRules.HexYieldDeltaPerHit(highland, PlayerId.Ai, ResourceType.Stone));
        }

        [Test]
        public void Shepherd_PasturesPlusOneWool()
        {
            var baseline = CreateState();
            var shepherd = CreateState(CivilizationId.ShepherdKingdom);
            var pasture = FindTile(baseline, ResourceType.Sheep);
            PlaceTwoSettlementsOnHex(baseline.Board, pasture, PlayerId.Human);
            PlaceTwoSettlementsOnHex(shepherd.Board, pasture, PlayerId.Human);

            Assert.AreEqual(ProduceOn(baseline, pasture).Sheep + 2, ProduceOn(shepherd, pasture).Sheep);
        }

        [Test]
        public void ForestFolk_GainsLumberOnTwoAndTwelve()
        {
            var state = CreateState(CivilizationId.ForestFolk);
            Assert.AreEqual(0, CivilizationRules.DiceBonus(state, PlayerId.Human, new List<int> { 8 }).Wood);
            Assert.AreEqual(1, CivilizationRules.DiceBonus(state, PlayerId.Human, new List<int> { 2 }).Wood,
                "At least 1 lumber even with no forest bordered");

            var forest = FindTile(state, ResourceType.Wood);
            PlaceTwoSettlementsOnHex(state.Board, forest, PlayerId.Human);
            int forests = CivilizationRules.DiceBonus(state, PlayerId.Human, new List<int> { 12 }).Wood;
            Assert.GreaterOrEqual(forests, 1);
            Assert.AreEqual(forests * 2, CivilizationRules.DiceBonus(state, PlayerId.Human, new List<int> { 2, 12 }).Wood);
            Assert.AreEqual(0, CivilizationRules.DiceBonus(CreateState(), PlayerId.Human, new List<int> { 2 }).Wood);
            Assert.AreEqual(0, CivilizationRules.DiceBonus(state, PlayerId.Ai, new List<int> { 2 }).Wood);
        }

        [Test]
        public void SunKingdom_RobberNeverBlocksHuman()
        {
            var state = CreateState(CivilizationId.SunKingdom);
            var hex = FindTile(state, ResourceType.Brick);
            state.Board.PlaceRobber(hex);
            Assert.IsFalse(ModifierService.IsRobberBlocking(state, hex, PlayerId.Human));

            var plain = CreateState();
            plain.Board.PlaceRobber(hex);
            Assert.IsTrue(ModifierService.IsRobberBlocking(plain, hex, PlayerId.Human));
        }

        // ---------------- sevens ----------------

        [Test]
        public void Brickwrights_GainBrickOnSeven()
        {
            var state = CreateState(CivilizationId.Brickwrights);
            string log = CivilizationRules.OnSevenRolled(state, new Random(1));
            Assert.AreEqual(1, state.PlayerInventory.Brick);
            Assert.That(log, Does.Contain("Brick"));
        }

        [Test]
        public void Raiders_StealOneFromOpponentOnSeven()
        {
            var state = CreateState(CivilizationId.NorthernRaiders);
            state.AiInventory = new ResourceBundle { Wheat = 2 };
            CivilizationRules.OnSevenRolled(state, new Random(1));
            Assert.AreEqual(1, state.AiInventory.Wheat);
            Assert.AreEqual(1, state.PlayerInventory.Wheat);

            var broke = CreateState(CivilizationId.NorthernRaiders);
            Assert.That(CivilizationRules.OnSevenRolled(broke, new Random(1)), Does.Contain("nothing"));
            Assert.AreEqual(0, broke.PlayerInventory.Total);
        }

        [Test]
        public void OtherCivs_SevenDoesNothing()
        {
            var state = CreateState(CivilizationId.SeaFolk);
            state.AiInventory = new ResourceBundle { Wheat = 2 };
            Assert.IsNull(CivilizationRules.OnSevenRolled(state, new Random(1)));
            Assert.AreEqual(0, state.PlayerInventory.Total);
        }

        // ---------------- cards + fish ----------------

        [Test]
        public void Academy_DrawsOneExtraCardPerNight()
        {
            Assert.AreEqual(1, CivilizationRules.ExtraNightDraws(CreateState(CivilizationId.Academy)));
            Assert.AreEqual(0, CivilizationRules.ExtraNightDraws(CreateState(CivilizationId.SeaFolk)));
        }

        [Test]
        public void SeaFolk_FishActionsCostOneLess_MinimumOne_AndCatchBonus()
        {
            var sea = CreateState(CivilizationId.SeaFolk);
            foreach (FishermenSpendAction action in Enum.GetValues(typeof(FishermenSpendAction)))
            {
                int baseCost = FishermenRules.GetFishCost(action);
                Assert.AreEqual(Math.Max(1, baseCost - 1), CivilizationRules.FishCost(sea, PlayerId.Human, action));
                Assert.AreEqual(baseCost, CivilizationRules.FishCost(sea, PlayerId.Ai, action));
            }

            Assert.AreEqual(1, CivilizationRules.FishCatchBonus(sea, PlayerId.Human, 2));
            Assert.AreEqual(0, CivilizationRules.FishCatchBonus(sea, PlayerId.Human, 0));
            Assert.AreEqual(0, CivilizationRules.FishCatchBonus(CreateState(), PlayerId.Human, 2));
        }

        // ---------------- events: library + cadence ----------------

        [Test]
        public void EventPool_HasEighteenEvents_WithUniqueIdsAndArt()
        {
            Assert.AreEqual(18, EventLibrary.Pool.Count);
            Assert.AreEqual(18, EventLibrary.Pool.Select(e => e.Id).Distinct().Count());
            foreach (var def in EventLibrary.Pool)
            {
                Assert.IsNotEmpty(def.Name);
                Assert.IsNotEmpty(def.Description);
                Assert.Greater(def.Act1Weight + def.Act2Weight + def.Act3Weight, 0, def.Name);
            }
        }

        [Test]
        public void Cadence_EveryThirdDay_EverySecondInAct3()
        {
            Assert.IsFalse(EventEngine.IsEventDay(0, 1));
            Assert.IsFalse(EventEngine.IsEventDay(1, 1));
            Assert.IsTrue(EventEngine.IsEventDay(3, 1));
            Assert.IsTrue(EventEngine.IsEventDay(6, 2));
            Assert.IsTrue(EventEngine.IsEventDay(4, 3));
            Assert.IsFalse(EventEngine.IsEventDay(5, 3));
            Assert.AreEqual(2, EventEngine.DaysUntilNextEvent(4, 1));
            Assert.AreEqual(0, EventEngine.DaysUntilNextEvent(6, 1));
        }

        [Test]
        public void RollNightEvent_IsDeterministicPerSeed()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                var a = CreateState();
                var b = CreateState();
                a.Board.DayNumber = b.Board.DayNumber = 3;
                Assert.AreEqual(new EventEngine(seed).RollNightEvent(a, 1), new EventEngine(seed).RollNightEvent(b, 1));
            }
        }

        [Test]
        public void Squall_OnlyRolledWhenFishermenEnabled()
        {
            var state = CreateState();
            state.FishermenOfCatanEnabled = false;
            for (int seed = 0; seed < 300; seed++)
                Assert.AreNotEqual(EventId.Squall, new EventEngine(seed).PickWeightedEvent(1, state));

            state.FishermenOfCatanEnabled = true;
            bool seen = false;
            for (int seed = 0; seed < 300 && !seen; seed++)
                seen = new EventEngine(seed).PickWeightedEvent(1, state) == EventId.Squall;
            Assert.IsTrue(seen);
        }

        [Test]
        public void EveryEvent_AppliesAndClearsWithoutThrowing()
        {
            foreach (var def in EventLibrary.Pool)
            {
                var state = CreateState();
                state.Board.DayNumber = 6;
                state.FishermenOfCatanEnabled = true;
                var engine = new EventEngine(5);
                Assert.DoesNotThrow(() => engine.ApplyEvent(state, def.Id), def.Name);
                Assert.AreEqual(def.Id, state.ActiveEvent);
                Assert.AreEqual(6, state.EventDay);
                Assert.That(state.EventMessage, Does.StartWith(def.Name), def.Name);
                engine.ClearDailyEventEffects(state);
                Assert.AreEqual(EventId.None, state.ActiveEvent);
                Assert.AreEqual(100, state.EventProductionPercent);
                Assert.IsNull(state.EventBlockedResource);
                Assert.IsNull(state.EventBonusResource);
                Assert.IsFalse(state.EventFishStorm);
                Assert.IsEmpty(state.EventHighlightHexes);
            }
        }

        // ---------------- events: board ----------------

        private static Dictionary<HexCoord, int?> Tokens(GameState s) =>
            s.Board.Tiles.ToDictionary(kv => kv.Key, kv => kv.Value.NumberToken);

        [Test]
        public void Earthquake_SwapsExactlyTwoDifferentTokens()
        {
            var state = CreateState();
            var before = Tokens(state);
            new EventEngine(3).ApplyEvent(state, EventId.Earthquake);
            var after = Tokens(state);

            var changed = before.Keys.Where(k => before[k] != after[k]).ToList();
            Assert.AreEqual(2, changed.Count);
            Assert.AreEqual(before[changed[0]], after[changed[1]]);
            Assert.AreEqual(before[changed[1]], after[changed[0]]);
            CollectionAssert.AreEquivalent(changed, state.EventHighlightHexes);
            Assert.That(state.EventDetail, Does.Contain("swapped"));
        }

        [Test]
        public void ShiftingFortunes_MovesOneTokenByOne_NeverToSeven()
        {
            for (int seed = 0; seed < 40; seed++)
            {
                var state = CreateState();
                var before = Tokens(state);
                new EventEngine(seed).ApplyEvent(state, EventId.ShiftingFortunes);
                var after = Tokens(state);
                var changed = before.Keys.Where(k => before[k] != after[k]).ToList();
                Assert.AreEqual(1, changed.Count);
                int b = before[changed[0]].Value, a = after[changed[0]].Value;
                Assert.AreNotEqual(7, a);
                Assert.LessOrEqual(Math.Abs(a - b), 2, "a step of 1, or 2 when hopping over 7");
            }
        }

        [Test]
        public void ShiftToken_ClampsAndSkipsSeven()
        {
            Assert.AreEqual(8, EventEffects.ShiftToken(6, 1));
            Assert.AreEqual(6, EventEffects.ShiftToken(8, -1));
            Assert.AreEqual(3, EventEffects.ShiftToken(2, -1));
            Assert.AreEqual(11, EventEffects.ShiftToken(12, 1));
            Assert.AreEqual(5, EventEffects.ShiftToken(4, 1));
        }

        [Test]
        public void RestlessRobber_MovesRobberToANewLandTile()
        {
            var state = CreateState();
            var start = state.Board.RobberTile;
            new EventEngine(9).ApplyEvent(state, EventId.RestlessRobber);
            Assert.IsTrue(state.Board.RobberTile.HasValue);
            Assert.AreNotEqual(start, state.Board.RobberTile);
            Assert.IsFalse(state.Board.Tiles[state.Board.RobberTile.Value].IsFishTile);
            Assert.That(state.EventHighlightHexes, Does.Contain(state.Board.RobberTile.Value));
        }

        // ---------------- events: production ----------------

        private static (ResourceBundle baseline, ResourceBundle withEvent) ProduceWith(EventId id,
            Action<GameState> tweak = null, ResourceType resource = ResourceType.Brick)
        {
            var baseline = CreateState();
            var evented = CreateState();
            var hex = FindTile(baseline, resource);
            PlaceTwoSettlementsOnHex(baseline.Board, hex, PlayerId.Human);
            PlaceTwoSettlementsOnHex(evented.Board, hex, PlayerId.Human);
            new EventEngine(4).ApplyEvent(evented, id);
            tweak?.Invoke(evented);
            return (ProduceOn(baseline, hex), ProduceOn(evented, hex));
        }

        [Test]
        public void DoubleHarvest_DoublesProduction()
        {
            var (b, e) = ProduceWith(EventId.DoubleHarvest);
            Assert.Greater(b.Brick, 0);
            Assert.AreEqual(b.Brick * 2, e.Brick);
            Assert.AreEqual(200, CreateStateWith(EventId.DoubleHarvest).EventProductionPercent);
        }

        [Test]
        public void LeanTimes_HalvesProductionRoundedDown()
        {
            var (b, e) = ProduceWith(EventId.LeanTimes);
            Assert.AreEqual(b.Brick * 50 / 100, e.Brick);
        }

        [Test]
        public void Blight_BlocksOneResource()
        {
            var (b, e) = ProduceWith(EventId.Blight, s => s.EventBlockedResource = ResourceType.Brick);
            Assert.Greater(b.Brick, 0);
            Assert.AreEqual(0, e.Brick);
            Assert.IsTrue(CreateStateWith(EventId.Blight).EventBlockedResource.HasValue);
        }

        [Test]
        public void Bountiful_AddsOnePerHitOnOneTerrain()
        {
            var (b, e) = ProduceWith(EventId.Bountiful, s => s.EventBonusResource = ResourceType.Brick);
            Assert.AreEqual(b.Brick + 2, e.Brick, "+1 per settlement hit");
            var state = CreateStateWith(EventId.Bountiful);
            Assert.IsTrue(state.EventBonusResource.HasValue);
            Assert.IsNotEmpty(state.EventHighlightHexes);
        }

        [Test]
        public void ApplyEventModifiers_BlockThenPercent()
        {
            var state = CreateState();
            state.EventBlockedResource = ResourceType.Wood;
            state.EventProductionPercent = 200;
            var result = ProductionCalculator.ApplyEventModifiers(state, new ResourceBundle { Wood = 3, Wheat = 2 });
            Assert.AreEqual(0, result.Wood);
            Assert.AreEqual(4, result.Wheat);
        }

        private static GameState CreateStateWith(EventId id)
        {
            var state = CreateState();
            new EventEngine(4).ApplyEvent(state, id);
            return state;
        }

        // ---------------- events: gifts + sea ----------------

        [Test]
        public void HarvestFestival_GivesBothPlayersTheSameResource()
        {
            var state = CreateStateWith(EventId.HarvestFestival);
            Assert.AreEqual(1, state.PlayerInventory.Total);
            Assert.AreEqual(1, state.AiInventory.Total);
            foreach (var r in ResourceTypes.Productive)
                Assert.AreEqual(state.PlayerInventory[r], state.AiInventory[r]);
        }

        [Test]
        public void RoyalPatronage_EveryoneDrawsACard()
        {
            var state = CreateState();
            int human = state.PlayerHand.Count, ai = state.AiHand.Count;
            new EventEngine(4).ApplyEvent(state, EventId.RoyalPatronage);
            Assert.AreEqual(human + 1, state.PlayerHand.Count);
            Assert.AreEqual(ai + 1, state.AiHand.Count);
        }

        [Test]
        public void Squall_StopsFishing()
        {
            var state = CreateState();
            state.FishermenOfCatanEnabled = true;
            new EventEngine(4).ApplyEvent(state, EventId.Squall);
            Assert.IsTrue(state.EventFishStorm);
        }

        // ---------------- save ----------------

        [Test]
        public void Save_RoundTripsCivilizationAndEventFields()
        {
            var game = new GameController(11, MapSize.Small);
            var s = game.State;
            s.Civilization = CivilizationId.HighlandClans;
            s.CivilizationOffer.AddRange(new[] { CivilizationId.HighlandClans, CivilizationId.SeaFolk, CivilizationId.Academy });
            s.ActiveEvent = EventId.Bountiful;
            s.EventProductionPercent = 50;
            s.EventBlockedResource = ResourceType.Wheat;
            s.EventBonusResource = ResourceType.Stone;
            s.EventFishStorm = true;
            s.EventDetail = "detail text";
            s.EventDay = 6;

            var restored = SaveGame.RestoreState(SaveGame.Parse(SaveGame.Serialize(game)));

            Assert.AreEqual(CivilizationId.HighlandClans, restored.Civilization);
            CollectionAssert.AreEqual(s.CivilizationOffer, restored.CivilizationOffer);
            Assert.AreEqual(50, restored.EventProductionPercent);
            Assert.AreEqual(ResourceType.Wheat, restored.EventBlockedResource);
            Assert.AreEqual(ResourceType.Stone, restored.EventBonusResource);
            Assert.IsTrue(restored.EventFishStorm);
            Assert.AreEqual("detail text", restored.EventDetail);
            Assert.AreEqual(6, restored.EventDay);
        }

        [Test]
        public void Save_OldSaveWithoutV131Fields_DefaultsSafely()
        {
            var game = new GameController(11, MapSize.Small);
            string json = SaveGame.Serialize(game);
            json = System.Text.RegularExpressions.Regex.Replace(json, "\"EventProductionPercent\":\\s*\\d+,?", "");
            json = System.Text.RegularExpressions.Regex.Replace(json, "\"EventDay\":\\s*-?\\d+,?", "");
            var restored = SaveGame.RestoreState(SaveGame.Parse(json));
            Assert.AreEqual(100, restored.EventProductionPercent);
            Assert.AreEqual(-1, restored.EventDay);
            Assert.AreEqual(CivilizationId.None, restored.Civilization);
        }
    }
}
