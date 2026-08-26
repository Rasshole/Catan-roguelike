using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class ModifierServiceNightUniquesTests
    {
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

        [Test]
        public void Monastery_TiedZeroRolls_UsesDescendingResourceTypeNotEnumAscending()
        {
            var state = CreateState();
            state.DraftedUniques.Add(UniqueBuildingId.Monastery);
            state.TomorrowRolls = Rolls(0, 1, 1, 1, 0);

            ModifierService.ApplyNightUniques(state);

            Assert.AreEqual(0, state.TomorrowRolls[ResourceType.Wood],
                "Wood should stay 0; ascending enum order would have bumped Wood first");
            Assert.AreEqual(1, state.TomorrowRolls[ResourceType.Stone],
                "Stone is the tied minimum 0 with tie-break ResourceType descending");
            Assert.IsTrue(state.MonasteryUsed);
        }

        [Test]
        public void Monastery_SingleZeroRoll_BumpsOnlyLowestRollResource()
        {
            var state = CreateState();
            state.DraftedUniques.Add(UniqueBuildingId.Monastery);
            state.TomorrowRolls = Rolls(1, 1, 0, 2, 1);

            ModifierService.ApplyNightUniques(state);

            Assert.AreEqual(1, state.TomorrowRolls[ResourceType.Wheat]);
            Assert.AreEqual(1, state.TomorrowRolls[ResourceType.Wood]);
            Assert.AreEqual(2, state.TomorrowRolls[ResourceType.Sheep]);
            Assert.IsTrue(state.MonasteryUsed);
        }

        [Test]
        public void Monastery_OncePerRun_SecondNightDoesNotBumpAgain()
        {
            var state = CreateState();
            state.DraftedUniques.Add(UniqueBuildingId.Monastery);
            state.TomorrowRolls = Rolls(0, 1, 1, 1, 1);

            ModifierService.ApplyNightUniques(state);
            Assert.AreEqual(1, state.TomorrowRolls[ResourceType.Wood]);
            Assert.IsTrue(state.MonasteryUsed);

            state.TomorrowRolls = Rolls(0, 1, 1, 1, 1);
            ModifierService.ApplyNightUniques(state);

            Assert.AreEqual(0, state.TomorrowRolls[ResourceType.Wood],
                "Monastery should not fire again after the once-per-run save");
        }

        [Test]
        public void Monastery_NoZeroRolls_IsNoOp()
        {
            var state = CreateState();
            state.DraftedUniques.Add(UniqueBuildingId.Monastery);
            state.TomorrowRolls = Rolls(1, 1, 1, 1, 2);

            ModifierService.ApplyNightUniques(state);

            Assert.IsFalse(state.MonasteryUsed);
            Assert.AreEqual(2, state.TomorrowRolls[ResourceType.Stone]);
        }

        [Test]
        public void RollInsurance_PicksScarcestInventoryAmongZeroRolls()
        {
            var state = CreateState();
            state.AcquiredPerks.Add(LevelUpPerkId.RollInsurance);
            state.TomorrowRolls = Rolls(0, 1, 1, 1, 0);
            state.PlayerInventory = new ResourceBundle { Wood = 8, Stone = 1 };

            ModifierService.ApplyNightUniques(state);

            Assert.AreEqual(0, state.TomorrowRolls[ResourceType.Wood]);
            Assert.AreEqual(1, state.TomorrowRolls[ResourceType.Stone]);
        }

        [Test]
        public void RollInsurance_ScarcestTieBreak_UsesAscendingResourceType()
        {
            var state = CreateState();
            state.AcquiredPerks.Add(LevelUpPerkId.RollInsurance);
            state.TomorrowRolls = Rolls(0, 0, 1, 1, 1);
            state.PlayerInventory = new ResourceBundle { Wood = 2, Brick = 2 };

            ModifierService.ApplyNightUniques(state);

            Assert.AreEqual(1, state.TomorrowRolls[ResourceType.Wood]);
            Assert.AreEqual(0, state.TomorrowRolls[ResourceType.Brick]);
        }

        [Test]
        public void RollInsurance_NoZeroRolls_IsNoOp()
        {
            var state = CreateState();
            state.AcquiredPerks.Add(LevelUpPerkId.RollInsurance);
            state.TomorrowRolls = Rolls(1, 1, 2, 1, 1);

            ModifierService.ApplyNightUniques(state);

            Assert.AreEqual(2, state.TomorrowRolls[ResourceType.Wheat]);
        }

        [Test]
        public void MonasteryThenRollInsurance_CanBumpDifferentZeroRollsSameNight()
        {
            var state = CreateState();
            state.DraftedUniques.Add(UniqueBuildingId.Monastery);
            state.AcquiredPerks.Add(LevelUpPerkId.RollInsurance);
            state.TomorrowRolls = Rolls(0, 1, 1, 1, 0);
            state.PlayerInventory = new ResourceBundle { Wood = 8, Stone = 1 };

            ModifierService.ApplyNightUniques(state);

            Assert.AreEqual(1, state.TomorrowRolls[ResourceType.Wood],
                "Roll insurance should bump the remaining scarcest 0");
            Assert.AreEqual(1, state.TomorrowRolls[ResourceType.Stone],
                "Monastery should bump the tied minimum 0 first (Stone via descending tie-break)");
            Assert.IsTrue(state.MonasteryUsed);
        }
    }
}
