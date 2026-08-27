using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core.Save;
using CatanRoguelike.Core.Turn;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class MetaProgressionTests
    {
        private const int Seed = 42;

        [TestCase(0, 1, PlayerId.Ai, 0)]
        [TestCase(5, 10, PlayerId.Human, 12)]
        [TestCase(10, 20, PlayerId.Human, 22)]
        [TestCase(8, 6, PlayerId.Ai, 11)]
        public void CalculateStarsEarned_IsDeterministic(int vp, int day, PlayerId winner, int expected)
        {
            Assert.AreEqual(expected, MetaProgression.CalculateStarsEarned(vp, day, winner));
        }

        [Test]
        public void FreshProfile_HasDefaultUnlocks_ForFullPreGameFlow()
        {
            var meta = MetaProgression.CreateFresh();

            Assert.IsTrue(meta.IsMapAvailable(MapSize.Small));
            Assert.IsFalse(meta.IsMapAvailable(MapSize.Medium));
            Assert.IsFalse(meta.IsMapAvailable(MapSize.Large));

            CollectionAssert.Contains(meta.GetAvailableLeaders().ToList(), LeaderId.Merchant);
            CollectionAssert.Contains(meta.GetAvailableLeaders().ToList(), LeaderId.Pioneer);
            Assert.AreEqual(2, meta.GetAvailableLeaders().Count());

            CollectionAssert.AreEquivalent(
                MetaCatalog.DefaultFreeUniques.ToList(),
                meta.GetDraftPool().ToList());
            Assert.AreEqual(2, meta.GetDraftPool().Count());
            Assert.AreEqual(RunProgression.DraftPickCount, meta.GetDraftPickCount());

            CollectionAssert.AreEquivalent(
                MetaCatalog.DefaultFreeCards.ToList(),
                meta.GetCardPool().ToList());
            Assert.AreEqual(7, meta.GetCardPool().Count());

            Assert.IsFalse(meta.IsUniqueAvailable(UniqueBuildingId.Monastery));
            Assert.IsFalse(meta.IsCardAvailable(CardId.BanditRaid));
            Assert.IsFalse(meta.IsCardAvailable(CardId.Forecast));

            Assert.IsFalse(meta.HasStartWheatBonus());
            Assert.IsFalse(meta.GetStartBonusCard().HasValue);
        }

        [Test]
        public void FreshProfile_CanCompletePreGameFlow_WithoutPurchases()
        {
            var meta = MetaProgression.CreateFresh();
            var game = new GameController(Seed, MapSize.Small, meta);

            game.SelectMap(MapSize.Small);
            game.SelectLeader(LeaderId.Merchant);
            game.ToggleDraftUnique(UniqueBuildingId.Sawmill);
            game.ToggleDraftUnique(UniqueBuildingId.GuildHall);
            game.ConfirmRunSetup();

            Assert.AreEqual(GamePhase.SetupPlayerSettlement1, game.State.Phase);
            Assert.IsTrue(game.State.RunSetupComplete);
            Assert.AreEqual(2, game.State.DraftedUniques.Count);
        }

        [Test]
        public void ToggleDraftUnique_RejectsLockedUniques_WithMeta()
        {
            var meta = MetaProgression.CreateFresh();
            var game = new GameController(Seed, MapSize.Small, meta);
            AdvanceToDraft(game);

            game.ToggleDraftUnique(UniqueBuildingId.Monastery);

            Assert.AreEqual(0, game.State.DraftedUniques.Count);
        }

        [Test]
        public void ConfirmRunSetup_RejectsLockedUniques()
        {
            var meta = MetaProgression.CreateFresh();
            var game = new GameController(Seed, MapSize.Small, meta);
            AdvanceToDraft(game);

            game.State.DraftedUniques.Add(UniqueBuildingId.Monastery);
            game.State.DraftedUniques.Add(UniqueBuildingId.Sawmill);
            game.ConfirmRunSetup();

            Assert.AreEqual(GamePhase.RunSelectDraft, game.State.Phase);
            Assert.IsFalse(game.State.RunSetupComplete);
        }

        [Test]
        public void UniqueUnlocks_ExpandDraftPool()
        {
            var meta = MetaProgression.CreateFresh();
            meta.AddStarsForTesting(10);

            Assert.IsTrue(meta.TryPurchase(MetaUnlockId.UniqueMonastery));
            CollectionAssert.Contains(meta.GetDraftPool().ToList(), UniqueBuildingId.Monastery);
            Assert.AreEqual(3, meta.GetDraftPool().Count());
        }

        [Test]
        public void CardPackUnlocks_ExpandDrawPool()
        {
            var meta = MetaProgression.CreateFresh();
            meta.AddStarsForTesting(10);

            Assert.IsTrue(meta.TryPurchase(MetaUnlockId.CardPackSabotage));
            Assert.IsTrue(meta.IsCardAvailable(CardId.BanditRaid));
            Assert.IsTrue(meta.IsCardAvailable(CardId.Embargo));
            Assert.IsFalse(meta.IsCardAvailable(CardId.Forecast));
            Assert.AreEqual(9, meta.GetCardPool().Count());

            Assert.IsTrue(meta.TryPurchase(MetaUnlockId.CardPackMarket));
            Assert.IsTrue(meta.IsCardAvailable(CardId.Forecast));
            Assert.AreEqual(12, meta.GetCardPool().Count());
        }

        [Test]
        public void DrawCard_ForHuman_WithFreshMeta_OnlyDrawsUnlockedCards()
        {
            var meta = MetaProgression.CreateFresh();
            var pool = meta.GetCardPool().ToList();
            var engine = new CardEngine(Seed);

            for (int i = 0; i < 300; i++)
            {
                var card = engine.DrawCard(forAi: false, humanPool: pool);
                Assert.IsTrue(meta.IsCardAvailable(card),
                    $"Human draw returned locked card {card}");
            }
        }

        [Test]
        public void DrawCard_ForAi_UsesFullAiPool_RegardlessOfMeta()
        {
            var meta = MetaProgression.CreateFresh();
            var engine = new CardEngine(Seed);
            var lockedPool = meta.GetCardPool().ToList();

            for (int i = 0; i < 100; i++)
            {
                var card = engine.DrawCard(forAi: true, humanPool: lockedPool);
                Assert.IsTrue(CardLibrary.Get(card).AiCanUse,
                    $"AI draw returned human-only card {card}");
            }
        }

        [Test]
        public void TryPurchase_DeductsStars_AndUnlocks()
        {
            var meta = MetaProgression.CreateFresh();
            meta.AddStarsForTesting(10);

            Assert.IsTrue(meta.TryPurchase(MetaUnlockId.MediumMap));
            Assert.IsTrue(meta.IsUnlocked(MetaUnlockId.MediumMap));
            Assert.IsTrue(meta.IsMapAvailable(MapSize.Medium));
            Assert.AreEqual(6, meta.Stars);
        }

        [Test]
        public void TryPurchase_Fails_WhenInsufficientStars()
        {
            var meta = MetaProgression.CreateFresh();
            meta.AddStarsForTesting(3);

            Assert.IsFalse(meta.TryPurchase(MetaUnlockId.MediumMap));
            Assert.IsFalse(meta.IsMapAvailable(MapSize.Medium));
        }

        [Test]
        public void TryAwardRun_GrantsOnce_PerRunKey()
        {
            var meta = MetaProgression.CreateFresh();

            Assert.IsTrue(meta.TryAwardRun(Seed, 8, 10, PlayerId.Human, out int first));
            Assert.AreEqual(15, first);
            Assert.AreEqual(15, meta.Stars);

            Assert.IsFalse(meta.TryAwardRun(Seed, 8, 10, PlayerId.Human, out int second));
            Assert.AreEqual(0, second);
            Assert.AreEqual(15, meta.Stars);
        }

        [Test]
        public void SerializeRoundTrip_PreservesStarsAndUnlocks()
        {
            var original = MetaProgression.CreateFresh();
            original.AddStarsForTesting(20);
            original.TryPurchase(MetaUnlockId.LeaderWarlord);
            original.TryPurchase(MetaUnlockId.UniqueMonastery);
            original.TryPurchase(MetaUnlockId.CardPackSabotage);
            original.TryAwardRun(Seed, 5, 4, PlayerId.Ai, out _);

            var json = MetaSave.Serialize(original);
            var loaded = MetaSave.Load(json);

            Assert.AreEqual(original.Stars, loaded.Stars);
            CollectionAssert.AreEquivalent(original.UnlockedIds.ToList(), loaded.UnlockedIds.ToList());
            CollectionAssert.AreEquivalent(original.AwardedRunKeys.ToList(), loaded.AwardedRunKeys.ToList());
            CollectionAssert.Contains(loaded.UnlockedIds.ToList(), MetaUnlockId.UniqueMonastery);
            CollectionAssert.Contains(loaded.UnlockedIds.ToList(), MetaUnlockId.CardPackSabotage);
        }

        [Test]
        public void SerializeRoundTrip_NewUnlockIds_UseCamelCase()
        {
            var meta = MetaProgression.CreateFresh();
            meta.AddStarsForTesting(20);
            meta.TryPurchase(MetaUnlockId.UniqueCaravanPost);
            meta.TryPurchase(MetaUnlockId.CardPackMarket);

            var json = MetaSave.Serialize(meta);

            StringAssert.Contains("uniqueCaravanPost", json);
            StringAssert.Contains("cardPackMarket", json);

            var loaded = MetaSave.Load(json);
            Assert.IsTrue(loaded.IsUnlocked(MetaUnlockId.UniqueCaravanPost));
            Assert.IsTrue(loaded.IsUnlocked(MetaUnlockId.CardPackMarket));
        }

        [Test]
        public void MetaSave_IsSeparateFromRunSave()
        {
            var meta = MetaProgression.CreateFresh();
            meta.AddStarsForTesting(7);
            meta.TryPurchase(MetaUnlockId.StartBonusWheat);

            var run = new GameController(Seed, MapSize.Small);
            CompleteRunSetup(run);
            var runJson = SaveGame.Serialize(run);
            var metaJson = MetaSave.Serialize(meta);

            StringAssert.DoesNotContain("stars", runJson);
            StringAssert.DoesNotContain("unlockedIds", runJson);
            StringAssert.Contains("\"formatVersion\"", metaJson);
            StringAssert.Contains("\"stars\"", metaJson);
            StringAssert.Contains("startBonusWheat", metaJson);

            SaveGame.LoadGame(runJson);
            var reloadedMeta = MetaSave.Load(metaJson);

            Assert.IsTrue(reloadedMeta.IsUnlocked(MetaUnlockId.StartBonusWheat));
            Assert.AreEqual(5, reloadedMeta.Stars);
        }

        [Test]
        public void StartBonusWheat_AppliedOnConfirmRunSetup()
        {
            var meta = MetaProgression.CreateFresh();
            meta.AddStarsForTesting(10);
            meta.TryPurchase(MetaUnlockId.StartBonusWheat);

            var game = new GameController(Seed, MapSize.Small, meta);
            AdvanceToDraft(game);
            game.ToggleDraftUnique(UniqueBuildingId.Sawmill);
            game.ToggleDraftUnique(UniqueBuildingId.GuildHall);
            game.ConfirmRunSetup();

            Assert.AreEqual(1, game.State.PlayerInventory.Wheat);
        }

        [Test]
        public void StartBonusCard_AddedOnFirstNight()
        {
            var meta = MetaProgression.CreateFresh();
            meta.AddStarsForTesting(10);
            meta.TryPurchase(MetaUnlockId.StartBonusCard);

            var game = new GameController(Seed, MapSize.Small, meta);
            CompleteRunSetup(game);

            CollectionAssert.Contains(game.State.PlayerHand, CardId.RoadBuilder);
        }

        [Test]
        public void ExtraDraftPick_IsCappedByAvailablePool()
        {
            var meta = MetaProgression.CreateFresh();
            meta.AddStarsForTesting(10);
            meta.TryPurchase(MetaUnlockId.ExtraDraftPick);

            Assert.AreEqual(2, meta.GetDraftPickCount());

            meta.TryPurchase(MetaUnlockId.UniqueMonastery);
            Assert.AreEqual(3, meta.GetDraftPickCount());
        }

        [Test]
        public void ExtraDraftPick_AllowsThreeUniques_WhenPoolHasThreeOrMore()
        {
            var meta = MetaProgression.CreateFresh();
            meta.AddStarsForTesting(20);
            meta.TryPurchase(MetaUnlockId.ExtraDraftPick);
            meta.TryPurchase(MetaUnlockId.UniqueMonastery);

            var game = new GameController(Seed, MapSize.Small, meta);
            AdvanceToDraft(game);
            game.ToggleDraftUnique(UniqueBuildingId.Sawmill);
            game.ToggleDraftUnique(UniqueBuildingId.GuildHall);
            game.ToggleDraftUnique(UniqueBuildingId.Monastery);
            game.ConfirmRunSetup();

            Assert.AreEqual(3, game.State.DraftedUniques.Count);
            Assert.IsTrue(game.State.RunSetupComplete);
        }

        [Test]
        public void MapGating_BlocksLockedSizes()
        {
            var meta = MetaProgression.CreateFresh();
            var game = new GameController(Seed, MapSize.Small, meta);

            game.SelectMap(MapSize.Large);
            Assert.AreEqual(GamePhase.RunSelectMap, game.State.Phase);
            Assert.AreEqual(MapSize.Small, game.State.MapSize);
        }

        [Test]
        public void NewGameController_DoesNotDeleteMetaProgression()
        {
            var meta = MetaProgression.CreateFresh();
            meta.AddStarsForTesting(4);
            meta.TryPurchase(MetaUnlockId.LeaderWarlord);

            _ = new GameController(Seed + 1, MapSize.Small, meta);

            Assert.IsTrue(meta.IsLeaderAvailable(LeaderId.Warlord));
            Assert.AreEqual(1, meta.Stars);
        }

        private static void AdvanceToDraft(GameController game)
        {
            game.SelectMap(MapSize.Small);
            game.SelectLeader(LeaderId.Merchant);
        }

        private static void CompleteRunSetup(GameController game)
        {
            AdvanceToDraft(game);
            game.ToggleDraftUnique(UniqueBuildingId.Sawmill);
            game.ToggleDraftUnique(UniqueBuildingId.GuildHall);
            game.ConfirmRunSetup();

            while (game.State.IsSetupPhase)
            {
                if (game.State.Phase is GamePhase.SetupAiSettlement1 or GamePhase.SetupAiSettlement2
                    or GamePhase.SetupAiRoad1 or GamePhase.SetupAiRoad2)
                    game.RunAiSetupStep();

                if (game.State.Phase is GamePhase.SetupPlayerSettlement1 or GamePhase.SetupPlayerSettlement2)
                {
                    var spot = game.GetValidSettlements(PlayerId.Human).First();
                    game.PlaceSettlement(spot, PlayerId.Human);
                }

                if (game.State.Phase is GamePhase.SetupPlayerRoad1 or GamePhase.SetupPlayerRoad2)
                {
                    var edge = game.GetValidRoads(PlayerId.Human).First();
                    game.PlaceRoad(edge, PlayerId.Human);
                }
            }
        }
    }
}
