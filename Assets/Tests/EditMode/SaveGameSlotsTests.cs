using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Save;
using CatanRoguelike.Core.Turn;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class SaveGameSlotsTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp() => _tempDir = Path.Combine(Path.GetTempPath(), "catan-save-tests-" + Guid.NewGuid());

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [Test]
        public void OnAutosavePoint_FiresWhenNightResolvesToDayPlayerActions()
        {
            var game = SaveGameRoundTripTestsHelper.CreateGamePastSetup(42);
            bool fired = false;
            game.OnAutosavePoint += () => fired = true;

            SaveGameRoundTripTestsHelper.PrepareNightAdvance(game);
            game.SkipNightCard();

            Assert.IsTrue(fired);
            Assert.AreEqual(GamePhase.DayPlayerActions, game.State.Phase);
            Assert.Greater(game.State.ShopDeals.Count, 0);
        }

        [Test]
        public void TwoSlots_Isolated_DoNotClobberEachOther()
        {
            var slot0 = new GameController(11, MapSize.Small);
            var slot1 = new GameController(22, MapSize.Small);

            SaveGameSlotStore.WriteSlot(_tempDir, 0, SaveGame.Serialize(slot0));
            SaveGameSlotStore.WriteSlot(_tempDir, 1, SaveGame.Serialize(slot1));

            Assert.IsTrue(SaveGameSlotStore.TryReadSlot(_tempDir, 0, out var json0));
            Assert.IsTrue(SaveGameSlotStore.TryReadSlot(_tempDir, 1, out var json1));

            var loaded0 = SaveGame.LoadGame(json0);
            var loaded1 = SaveGame.LoadGame(json1);

            Assert.AreEqual(11, loaded0.RunSeed);
            Assert.AreEqual(22, loaded1.RunSeed);
        }

        [Test]
        public void LegacySaveJson_LoadsFromSlot0Path()
        {
            var game = new GameController(99, MapSize.Small);
            var json = SaveGame.Serialize(game);
            var legacyPath = Path.Combine(_tempDir, SaveGameSlotStore.LegacySlot0FileName);
            Directory.CreateDirectory(_tempDir);
            File.WriteAllText(legacyPath, json);

            Assert.IsTrue(SaveGameSlotStore.TryReadSlot(_tempDir, 0, out var loadedJson));
            var loaded = SaveGame.LoadGame(loadedJson);
            Assert.AreEqual(99, loaded.RunSeed);
        }

        [Test]
        public void AutosaveMetadata_RoundTripsThroughSlot0()
        {
            var game = SaveGameRoundTripTestsHelper.CreateGamePastSetup(42);
            SaveGameRoundTripTestsHelper.PrepareNightAdvance(game);
            game.SkipNightCard();

            var savedAt = new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);
            var json = SaveGame.Serialize(game, SaveWriteOptions.Autosave(savedAt));
            SaveGameSlotStore.WriteSlot(_tempDir, 0, json);

            var autosaveUtc = SaveGameSlotStore.GetLastAutosaveUtc(_tempDir);
            Assert.IsNotNull(autosaveUtc);
            Assert.AreEqual(savedAt, autosaveUtc.Value);

            var doc = SaveGame.Parse(json);
            Assert.IsTrue(doc.IsAutosave);
            Assert.AreEqual(savedAt.ToString("o"), doc.SavedAtUtc);
        }

        [Test]
        public void MetaStartCardGranted_RestoredAfterLoad()
        {
            var game = SaveGameRoundTripTestsHelper.CreateGamePastSetup(42);
            SaveGameRoundTripTestsHelper.PrepareNightAdvance(game);
            game.SkipNightCard();

            var json = SaveGame.Serialize(game);
            var loaded = SaveGame.LoadGame(json);

            Assert.IsTrue(loaded.GetMetaStartCardGrantedForSave());
        }
    }

    /// <summary>Shared helpers for save tests (also used by round-trip tests).</summary>
    internal static class SaveGameRoundTripTestsHelper
    {
        public static GameController CreateGamePastSetup(int seed)
        {
            var game = new GameController(seed, MapSize.Small);
            CompleteRunSetup(game);
            Assert.AreEqual(GamePhase.NightPlayCard, game.State.Phase);
            return game;
        }

        public static void PrepareNightAdvance(GameController game)
        {
            if (game.State.TodayRolls.Count == 0 && game.State.TomorrowRolls.Count > 0)
                game.State.TodayRolls = new Dictionary<ResourceType, int>(game.State.TomorrowRolls);
            if (game.State.TodayDiceRolls.Count == 0 && game.State.TomorrowDiceRolls.Count > 0)
                game.State.TodayDiceRolls = new List<int>(game.State.TomorrowDiceRolls);
        }

        private static void CompleteRunSetup(GameController game)
        {
            game.SelectMap(MapSize.Small);
            game.SelectLeader(CatanRoguelike.Core.Leaders.LeaderId.Merchant);

            var uniques = (CatanRoguelike.Core.Buildings.UniqueBuildingId[])Enum.GetValues(
                typeof(CatanRoguelike.Core.Buildings.UniqueBuildingId));
            game.ToggleDraftUnique(uniques[0]);
            game.ToggleDraftUnique(uniques[1]);
            game.ConfirmRunSetup();

            while (game.State.IsSetupPhase)
                AdvanceSetupStep(game);
        }

        private static void AdvanceSetupStep(GameController game)
        {
            switch (game.State.Phase)
            {
                case GamePhase.SetupAiSettlement1:
                case GamePhase.SetupAiSettlement2:
                case GamePhase.SetupAiRoad1:
                case GamePhase.SetupAiRoad2:
                    game.RunAiSetupStep();
                    break;

                case GamePhase.SetupPlayerSettlement1:
                case GamePhase.SetupPlayerSettlement2:
                    var settlement = game.GetValidSettlements(PlayerId.Human).First();
                    Assert.IsTrue(game.PlaceSettlement(settlement, PlayerId.Human));
                    break;

                case GamePhase.SetupPlayerRoad1:
                case GamePhase.SetupPlayerRoad2:
                    var road = game.GetValidRoads(PlayerId.Human).First();
                    Assert.IsTrue(game.PlaceRoad(road, PlayerId.Human));
                    break;

                default:
                    throw new InvalidOperationException("unexpected setup phase " + game.State.Phase);
            }
        }
    }
}
