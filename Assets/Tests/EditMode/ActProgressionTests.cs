using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Progression;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class ActProgressionTests
    {
        [TestCase(1, 1)]
        [TestCase(4, 1)]
        [TestCase(5, 2)]
        [TestCase(8, 2)]
        [TestCase(9, 3)]
        [TestCase(20, 3)]
        public void GetAct_MapsDayThresholds(int day, int expectedAct)
        {
            Assert.AreEqual(expectedAct, ActProgression.GetAct(day));
        }

        [Test]
        public void GetYieldConfig_Act1_DoublePassMaxThree()
        {
            var (passes, maxRoll) = ActProgression.GetYieldConfig(3);
            Assert.AreEqual(BalanceConfig.Act1NightlyRollPasses, passes);
            Assert.AreEqual(BalanceConfig.Act1MaxRoll, maxRoll);
        }

        [Test]
        public void GetYieldConfig_Act2_TriplePassMaxThree()
        {
            var (passes, maxRoll) = ActProgression.GetYieldConfig(5);
            Assert.AreEqual(BalanceConfig.Act2NightlyRollPasses, passes);
            Assert.AreEqual(BalanceConfig.Act2MaxRoll, maxRoll);
        }

        [Test]
        public void GetYieldConfig_Act3_TriplePassMaxThree()
        {
            var (passes, maxRoll) = ActProgression.GetYieldConfig(9);
            Assert.AreEqual(BalanceConfig.Act3NightlyRollPasses, passes);
            Assert.AreEqual(BalanceConfig.Act3MaxRoll, maxRoll);
        }

        [Test]
        public void GetEventChance_IncreasesByAct()
        {
            Assert.Less(ActProgression.GetEventChance(1), ActProgression.GetEventChance(2));
            Assert.Less(ActProgression.GetEventChance(2), ActProgression.GetEventChance(3));
        }

        [Test]
        public void GetMapExpansionTarget_SmallAct2_ToMedium()
        {
            Assert.AreEqual(MapSize.Medium,
                ActProgression.GetMapExpansionTarget(MapSize.Small, 2));
        }

        [Test]
        public void GetMapExpansionTarget_SmallAct3_ToLarge()
        {
            Assert.AreEqual(MapSize.Large,
                ActProgression.GetMapExpansionTarget(MapSize.Small, 3));
        }

        [Test]
        public void GetMapExpansionTarget_MediumAct3_ToLarge()
        {
            Assert.AreEqual(MapSize.Large,
                ActProgression.GetMapExpansionTarget(MapSize.Medium, 3));
        }

        [Test]
        public void GetMapExpansionTarget_LargeAct3_NoExpansion()
        {
            Assert.IsNull(ActProgression.GetMapExpansionTarget(MapSize.Large, 3));
        }
    }
}
