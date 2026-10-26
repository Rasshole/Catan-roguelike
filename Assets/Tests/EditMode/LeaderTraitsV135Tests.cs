using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Civilizations;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests
{
    /// <summary>v135: every civ's leader has its own passive and its own portrait.</summary>
    public class LeaderTraitsV135Tests
    {
        private static GameState State(CivilizationId civ)
        {
            var state = new GameState(MapPresets.CreateBoard());
            state.Civilization = civ;
            state.Leader = CivilizationLibrary.Get(civ).Leader;
            return state;
        }

        [Test]
        public void AllTenCivs_HaveDistinctLeaderTitleAndPassive()
        {
            var pool = CivilizationLibrary.Pool;
            Assert.AreEqual(10, pool.Select(c => c.LeaderAbility).Distinct().Count());
            Assert.AreEqual(10, pool.Select(c => c.LeaderTitle).Distinct().Count());
            Assert.AreNotEqual(CivilizationLibrary.Get(CivilizationId.SunKingdom).LeaderAbility,
                CivilizationLibrary.Get(CivilizationId.SeaFolk).LeaderAbility);
        }

        [Test]
        public void AllTenCivs_HaveAPortraitTexture()
        {
            foreach (var civ in CivilizationLibrary.Pool)
            {
                string key = string.IsNullOrEmpty(civ.PortraitKey) ? civ.Id.ToString() : civ.PortraitKey;
                var tex = Resources.Load<Texture2D>("Art/Leaders/" + key);
                Assert.IsNotNull(tex, $"{civ.Name}: Art/Leaders/{key}");
            }

            foreach (var id in new[] { "SeaFolk", "SteppeNomads", "HighlandClans", "ShepherdKingdom", "Academy", "SunKingdom" })
                Assert.IsNotNull(Resources.Load<Texture2D>("Art/Leaders/" + id), id);
        }

        [Test]
        public void ArchetypePassive_OnlyAppliesToTheArchetypeCiv()
        {
            // Sea Folk and Sun Kingdom have the Merchant perk pool but NOT the Merchant shop discount.
            Assert.IsFalse(LeaderTraits.HasArchetypePassive(State(CivilizationId.SeaFolk), LeaderId.Merchant));
            Assert.IsFalse(LeaderTraits.HasArchetypePassive(State(CivilizationId.SunKingdom), LeaderId.Merchant));
            Assert.IsTrue(LeaderTraits.HasArchetypePassive(State(CivilizationId.MerchantRepublic), LeaderId.Merchant));
            Assert.IsTrue(LeaderTraits.HasArchetypePassive(State(CivilizationId.NorthernRaiders), LeaderId.Warlord));
            Assert.AreEqual(1, ModifierService.GetKnightStealAmount(State(CivilizationId.HighlandClans), PlayerId.Human));
            Assert.AreEqual(2, ModifierService.GetKnightStealAmount(State(CivilizationId.NorthernRaiders), PlayerId.Human));

            // Legacy (no civ chosen): the leader archetype passive applies as before.
            var legacy = new GameState(MapPresets.CreateBoard()) { Leader = LeaderId.Merchant };
            Assert.IsTrue(LeaderTraits.HasArchetypePassive(legacy, LeaderId.Merchant));
        }

        [Test]
        public void MorningTraits_GiveFishWoolAndScarcest()
        {
            var sea = State(CivilizationId.SeaFolk);
            int fish = sea.PlayerFish;
            Assert.IsNotNull(LeaderTraits.OnMorning(sea));
            Assert.AreEqual(fish + 1, sea.PlayerFish);

            var steppe = State(CivilizationId.SteppeNomads);
            int wool = steppe.PlayerInventory[ResourceType.Sheep];
            LeaderTraits.OnMorning(steppe);
            Assert.AreEqual(wool + 1, steppe.PlayerInventory[ResourceType.Sheep]);

            var academy = State(CivilizationId.Academy);
            var inv = ResourceBundle.Zero;
            foreach (var r in ResourceTypes.Productive)
                inv.Add(r, 3);
            inv.Set(ResourceType.Brick, 1);
            academy.PlayerInventory = inv;
            LeaderTraits.OnMorning(academy);
            Assert.AreEqual(2, academy.PlayerInventory[ResourceType.Brick]);

            Assert.IsNull(LeaderTraits.OnMorning(State(CivilizationId.MerchantRepublic)));
        }

        [Test]
        public void SevenAndDiceTraits()
        {
            var clans = State(CivilizationId.HighlandClans);
            int ore = clans.PlayerInventory[ResourceType.Stone];
            LeaderTraits.OnSevenRolled(clans);
            Assert.AreEqual(ore + 1, clans.PlayerInventory[ResourceType.Stone]);

            var sun = State(CivilizationId.SunKingdom);
            int wheat = sun.PlayerInventory[ResourceType.Wheat];
            LeaderTraits.OnSevenRolled(sun);
            Assert.AreEqual(wheat + 1, sun.PlayerInventory[ResourceType.Wheat]);

            var shep = State(CivilizationId.ShepherdKingdom);
            Assert.AreEqual(2, LeaderTraits.DiceBonus(shep, PlayerId.Human, new[] { 2, 7, 12 })[ResourceType.Wheat]);
            Assert.AreEqual(0, LeaderTraits.DiceBonus(shep, PlayerId.Ai, new[] { 2 })[ResourceType.Wheat]);
        }
    }
}
