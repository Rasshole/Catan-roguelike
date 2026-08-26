using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core.Turn;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    /// <summary>
    /// Pre-game run setup: map select → leader → unique draft → confirm → AI setup.
    /// Level-up interval/perk draft is covered by RunProgressionTests.
    /// </summary>
    public class RunProgressionDraftFlowTests
    {
        private const int Seed = 42;

        private static GameController NewGame(MapSize preview = MapSize.Small) =>
            new GameController(Seed, preview);

        private static void AdvanceToDraft(GameController game, MapSize mapSize, LeaderId leader)
        {
            game.SelectMap(mapSize);
            game.SelectLeader(leader);
        }

        [Test]
        public void NewGame_StartsAtRunSelectMap_WithPreviewBoard()
        {
            var game = NewGame(MapSize.Medium);

            Assert.AreEqual(GamePhase.RunSelectMap, game.State.Phase);
            Assert.AreEqual(MapSize.Medium, game.State.MapSize);
            Assert.AreEqual(MapPresets.GetHexCount(MapSize.Medium), game.State.Board.Tiles.Count);
            Assert.IsFalse(game.State.RunSetupComplete);
            Assert.AreEqual(0, game.State.DraftedUniques.Count);
            Assert.That(game.State.StatusMessage, Does.Contain("kortstørrelse"));
        }

        [TestCase(MapSize.Small, 7)]
        [TestCase(MapSize.Medium, 13)]
        [TestCase(MapSize.Large, 19)]
        public void SelectMap_TransitionsToLeaderSelect_AndRebuildsBoard(MapSize mapSize, int expectedTiles)
        {
            var game = NewGame();
            Assert.AreEqual(GamePhase.RunSelectMap, game.State.Phase);

            game.SelectMap(mapSize);

            Assert.AreEqual(GamePhase.RunSelectLeader, game.State.Phase);
            Assert.AreEqual(mapSize, game.State.MapSize);
            Assert.AreEqual(expectedTiles, game.State.Board.Tiles.Count);
            Assert.AreEqual(LeaderId.Merchant, game.State.Leader,
                "map reset should restore default leader before selection");
            Assert.AreEqual(0, game.State.DraftedUniques.Count);
            Assert.IsFalse(game.State.RunSetupComplete);
            Assert.Greater(game.State.Ports.Count, 0, "ports should be discovered for the new board");
            Assert.That(game.State.StatusMessage, Does.Contain("leader"));
        }

        [Test]
        public void SelectMap_IsIgnored_WhenNotInRunSelectMap()
        {
            var game = NewGame();
            game.SelectMap(MapSize.Small);
            int tilesAfterFirstSelect = game.State.Board.Tiles.Count;

            game.SelectMap(MapSize.Large);

            Assert.AreEqual(GamePhase.RunSelectLeader, game.State.Phase);
            Assert.AreEqual(MapSize.Small, game.State.MapSize);
            Assert.AreEqual(tilesAfterFirstSelect, game.State.Board.Tiles.Count);
        }

        [Test]
        public void SelectLeader_TransitionsToDraft_WithLeaderInStatus()
        {
            var game = NewGame();
            AdvanceToDraft(game, MapSize.Small, LeaderId.Pioneer);

            Assert.AreEqual(GamePhase.RunSelectDraft, game.State.Phase);
            Assert.AreEqual(LeaderId.Pioneer, game.State.Leader);
            Assert.That(game.State.StatusMessage, Does.Contain(LeaderLibrary.Get(LeaderId.Pioneer).Name));
            Assert.That(game.State.StatusMessage, Does.Contain(RunProgression.DraftPickCount.ToString()));
        }

        [Test]
        public void SelectLeader_IsIgnored_WhenNotInRunSelectLeader()
        {
            var game = NewGame();
            game.SelectLeader(LeaderId.Warlord);

            Assert.AreEqual(GamePhase.RunSelectMap, game.State.Phase);
            Assert.AreEqual(LeaderId.Merchant, game.State.Leader);
        }

        [Test]
        public void PreMapActions_AreIgnored()
        {
            var game = NewGame();

            game.SelectLeader(LeaderId.Architect);
            game.ToggleDraftUnique(UniqueBuildingId.Sawmill);
            game.ToggleDraftUnique(UniqueBuildingId.Monastery);
            game.ConfirmRunSetup();

            Assert.AreEqual(GamePhase.RunSelectMap, game.State.Phase);
            Assert.AreEqual(0, game.State.DraftedUniques.Count);
            Assert.IsFalse(game.State.RunSetupComplete);
        }

        [Test]
        public void ToggleDraftUnique_AddRemoveRespectsDraftPickCount()
        {
            var game = NewGame();
            AdvanceToDraft(game, MapSize.Small, LeaderId.Merchant);

            game.ToggleDraftUnique(UniqueBuildingId.Sawmill);
            game.ToggleDraftUnique(UniqueBuildingId.GuildHall);
            Assert.AreEqual(RunProgression.DraftPickCount, game.State.DraftedUniques.Count);
            CollectionAssert.Contains(game.State.DraftedUniques, UniqueBuildingId.Sawmill);
            CollectionAssert.Contains(game.State.DraftedUniques, UniqueBuildingId.GuildHall);

            game.ToggleDraftUnique(UniqueBuildingId.Monastery);
            Assert.AreEqual(RunProgression.DraftPickCount, game.State.DraftedUniques.Count,
                "cannot draft more than DraftPickCount");
            CollectionAssert.DoesNotContain(game.State.DraftedUniques, UniqueBuildingId.Monastery);

            game.ToggleDraftUnique(UniqueBuildingId.Sawmill);
            Assert.AreEqual(1, game.State.DraftedUniques.Count);
            CollectionAssert.DoesNotContain(game.State.DraftedUniques, UniqueBuildingId.Sawmill);

            game.ToggleDraftUnique(UniqueBuildingId.CaravanPost);
            game.ToggleDraftUnique(UniqueBuildingId.FortressOutpost);
            Assert.AreEqual(RunProgression.DraftPickCount, game.State.DraftedUniques.Count);
            CollectionAssert.Contains(game.State.DraftedUniques, UniqueBuildingId.CaravanPost);
            CollectionAssert.Contains(game.State.DraftedUniques, UniqueBuildingId.FortressOutpost);
        }

        [Test]
        public void ToggleDraftUnique_IsIgnored_WhenNotInRunSelectDraft()
        {
            var game = NewGame();
            game.SelectMap(MapSize.Small);

            game.ToggleDraftUnique(UniqueBuildingId.Sawmill);

            Assert.AreEqual(GamePhase.RunSelectLeader, game.State.Phase);
            Assert.AreEqual(0, game.State.DraftedUniques.Count);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void ConfirmRunSetup_IsIgnored_WithTooFewDraftPicks(int pickCount)
        {
            var game = NewGame();
            AdvanceToDraft(game, MapSize.Small, LeaderId.Merchant);

            if (pickCount >= 1)
                game.ToggleDraftUnique(UniqueBuildingId.Sawmill);

            game.ConfirmRunSetup();

            Assert.AreEqual(GamePhase.RunSelectDraft, game.State.Phase);
            Assert.IsFalse(game.State.RunSetupComplete);
            Assert.AreEqual(0, game.State.Board.CountBuildings(PlayerId.Ai, BuildingType.Settlement));
        }

        [Test]
        public void ConfirmRunSetup_IsIgnored_WhenNotInRunSelectDraft()
        {
            var game = NewGame();
            game.SelectMap(MapSize.Small);

            game.ToggleDraftUnique(UniqueBuildingId.Sawmill);
            game.ToggleDraftUnique(UniqueBuildingId.GuildHall);
            game.ConfirmRunSetup();

            Assert.AreEqual(GamePhase.RunSelectLeader, game.State.Phase);
            Assert.IsFalse(game.State.RunSetupComplete);
            Assert.AreEqual(0, game.State.DraftedUniques.Count);
        }

        [Timeout(2000)]
        [Test]
        public void ConfirmRunSetup_TransitionsIntoAiSetup_WithDraftedUniques()
        {
            var game = NewGame();
            AdvanceToDraft(game, MapSize.Small, LeaderId.Merchant);
            game.ToggleDraftUnique(UniqueBuildingId.Sawmill);
            game.ToggleDraftUnique(UniqueBuildingId.GuildHall);

            game.ConfirmRunSetup();

            Assert.IsTrue(game.State.RunSetupComplete);
            Assert.IsTrue(game.State.HasUnique(UniqueBuildingId.Sawmill));
            Assert.IsTrue(game.State.HasUnique(UniqueBuildingId.GuildHall));
            Assert.AreEqual(2, game.State.Board.CountBuildings(PlayerId.Ai, BuildingType.Settlement));
            Assert.AreEqual(2, game.State.Board.CountRoads(PlayerId.Ai));
            Assert.AreEqual(0, game.State.Board.CountBuildings(PlayerId.Human, BuildingType.Settlement));
            Assert.AreEqual(GamePhase.SetupPlayerSettlement1, game.State.Phase);
            Assert.That(game.State.StatusMessage, Does.Contain("settlement"));
        }
    }
}
