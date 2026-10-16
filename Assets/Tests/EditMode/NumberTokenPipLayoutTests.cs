using CatanRoguelike.Core.Yield;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class NumberTokenPipLayoutTests
    {
        [Test]
        public void GetPipCount_MatchesClassicWeights()
        {
            Assert.AreEqual(1, NumberTokenPipLayout.GetPipCount(2));
            Assert.AreEqual(1, NumberTokenPipLayout.GetPipCount(12));
            Assert.AreEqual(5, NumberTokenPipLayout.GetPipCount(6));
            Assert.AreEqual(5, NumberTokenPipLayout.GetPipCount(8));
            Assert.AreEqual(3, NumberTokenPipLayout.GetPipCount(4));
            Assert.AreEqual(2, NumberTokenPipLayout.GetPipCount(3));
            Assert.AreEqual(2, NumberTokenPipLayout.GetPipCount(11));
            Assert.AreEqual(4, NumberTokenPipLayout.GetPipCount(5));
            Assert.AreEqual(4, NumberTokenPipLayout.GetPipCount(9));
        }

        [Test]
        public void GetPipOffsetsX_ReturnsCenteredRow()
        {
            var offsets = NumberTokenPipLayout.GetPipOffsetsX(3);
            Assert.AreEqual(3, offsets.Length);
            Assert.AreEqual(0f, offsets[1], 0.0001f);
        }

        [Test]
        public void GetPipOffsetsX_ZeroPips_ReturnsEmpty()
        {
            Assert.AreEqual(0, NumberTokenPipLayout.GetPipOffsetsX(0).Length);
        }

        [Test]
        public void RedNumbers_AreSixAndEight()
        {
            Assert.IsTrue(NumberTokenLibrary.IsRedNumber(6));
            Assert.IsTrue(NumberTokenLibrary.IsRedNumber(8));
            Assert.IsFalse(NumberTokenLibrary.IsRedNumber(5));
        }
    }
}
