using System;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Civilizations;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core.Turn;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    /// <summary>v134 (DECIDED): civilizations and leaders are one concept — a civ is led by a leader.</summary>
    public class CivLeaderMergeV134Tests
    {
        [Test]
        public void EveryCiv_HasANamedLeader_AndALeaderAbility()
        {
            foreach (var civ in CivilizationLibrary.Pool)
            {
                Assert.IsNotEmpty(civ.LeaderName, civ.Name);
                Assert.IsNotEmpty(civ.LeaderAbility, civ.Name);
                Assert.IsTrue(LeaderLibrary.All.ContainsKey(civ.Leader));
            }

            Assert.AreEqual(10, CivilizationLibrary.Pool.Select(c => c.LeaderName).Distinct().Count());
            foreach (LeaderId leader in Enum.GetValues(typeof(LeaderId)))
                Assert.GreaterOrEqual(CivilizationLibrary.Pool.Count(c => c.Leader == leader), 2,
                    $"{leader} should lead at least two civs");
        }

        [Test]
        public void ExistingFourPortraits_AreReusedByTheMatchingCivs()
        {
            Assert.AreEqual("Merchant", CivilizationLibrary.Get(CivilizationId.MerchantRepublic).PortraitKey);
            Assert.AreEqual("Pioneer", CivilizationLibrary.Get(CivilizationId.ForestFolk).PortraitKey);
            Assert.AreEqual("Warlord", CivilizationLibrary.Get(CivilizationId.NorthernRaiders).PortraitKey);
            Assert.AreEqual("Architect", CivilizationLibrary.Get(CivilizationId.Brickwrights).PortraitKey);
            Assert.AreEqual(6, CivilizationLibrary.Pool.Count(c => string.IsNullOrEmpty(c.PortraitKey)),
                "six civs use their own Art/Leaders/{CivId} portrait (v135)");
            foreach (var civ in CivilizationLibrary.Pool.Where(c => !string.IsNullOrEmpty(c.PortraitKey)))
                Assert.AreEqual(civ.Leader.ToString(), civ.PortraitKey, "portrait matches the leader archetype");
        }

        [Test]
        public void Offer_WithFilter_OnlyContainsAvailableLeaders_AndStaysThree()
        {
            var meta = new MetaProgression();
            for (int seed = 0; seed < 40; seed++)
            {
                var offer = CivilizationOffer.Generate(seed, 3, d => meta.IsLeaderAvailable(d.Leader));
                Assert.AreEqual(3, offer.Count);
                Assert.IsTrue(offer.All(id => meta.IsLeaderAvailable(CivilizationLibrary.Get(id).Leader)));
                CollectionAssert.AreEqual(offer, CivilizationOffer.Generate(seed, 3, d => meta.IsLeaderAvailable(d.Leader)));
            }
        }

        [Timeout(4000)]
        [Test]
        public void Flow_MapThenCivThenDraft_NoSeparateLeaderStep()
        {
            var game = new GameController(11, MapSize.Small, new MetaProgression()) { CivilizationPickEnabled = true };
            game.SelectMap(MapSize.Small);
            Assert.AreEqual(GamePhase.RunSelectCivilization, game.State.Phase);
            Assert.AreEqual(3, game.State.CivilizationOffer.Count);

            var pick = game.State.CivilizationOffer[2];
            Assert.IsTrue(game.ChooseCivilization(pick));
            Assert.AreEqual(GamePhase.RunSelectDraft, game.State.Phase);
            Assert.AreEqual(CivilizationLibrary.Get(pick).Leader, game.State.Leader);

            var pool = game.Meta.GetDraftPool().ToList();
            game.ToggleDraftUnique(pool[0]);
            game.ToggleDraftUnique(pool[1]);
            game.ConfirmRunSetup();
            Assert.AreEqual(GamePhase.SetupPlayerSettlement1, game.State.Phase);
            Assert.AreEqual(pick, game.State.Civilization);
        }

        [Timeout(4000)]
        [Test]
        public void LegacyFlow_LeaderThenDraft_StillOffersCivLast_WhenNoCivChosen()
        {
            // A save taken mid-setup before v134 sits in RunSelectLeader/Draft with no civ: it still works.
            var game = new GameController(5, MapSize.Small) { CivilizationPickEnabled = false };
            game.SelectMap(MapSize.Small);
            Assert.AreEqual(GamePhase.RunSelectLeader, game.State.Phase);
            game.SelectLeader(LeaderId.Pioneer);
            game.ToggleDraftUnique(UniqueBuildingId.Sawmill);
            game.ToggleDraftUnique(UniqueBuildingId.GuildHall);
            game.CivilizationPickEnabled = true;
            game.ConfirmRunSetup();
            Assert.AreEqual(GamePhase.RunSelectCivilization, game.State.Phase);
            Assert.IsTrue(game.ChooseCivilization(game.State.CivilizationOffer[0]));
            Assert.AreEqual(GamePhase.SetupPlayerSettlement1, game.State.Phase);
        }
    }
}
