using System.Linq;
using CatanRoguelike.Core.Yield;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class RollEngineTests
    {
        [Test]
        public void RollNightly_HasAtMostOneZeroAndOneTwo()
        {
            var engine = new RollEngine(12345);

            for (int i = 0; i < 200; i++)
            {
                var rolls = engine.RollNightly(2);
                Assert.AreEqual(5, rolls.Count);
                Assert.LessOrEqual(rolls.Values.Count(v => v == 0), 1);
                Assert.LessOrEqual(rolls.Values.Count(v => v == 2), 1);
            }
        }

        [Test]
        public void RollNightly_ValuesStayInRange()
        {
            var engine = new RollEngine(99);
            var rolls = engine.RollNightly(2);

            foreach (var value in rolls.Values)
            {
                Assert.GreaterOrEqual(value, 0);
                Assert.LessOrEqual(value, 2);
            }
        }

        [Test]
        public void RollNightlyCombined_MaxRollThree_HasAtMostOneThree()
        {
            var engine = new RollEngine(55);
            for (int i = 0; i < 100; i++)
            {
                var rolls = engine.RollNightlyCombined(2, 3);
                Assert.LessOrEqual(rolls.Values.Count(v => v == 3), 1);
            }
        }
    }
}
