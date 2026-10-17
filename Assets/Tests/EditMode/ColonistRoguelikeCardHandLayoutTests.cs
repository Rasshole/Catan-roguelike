using CatanRoguelike.Game;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class ColonistRoguelikeCardHandLayoutTests
    {
        [Test]
        public void FanPoses_AreSymmetricForFiveCards()
        {
            var poses = ColonistRoguelikeCardHandLayout.ComputeFanPoses(5);
            Assert.AreEqual(5, poses.Length);
            Assert.AreEqual(-poses[0].X, poses[4].X, 0.001f);
            Assert.AreEqual(-poses[0].RotationZ, poses[4].RotationZ, 0.001f);
            Assert.AreEqual(poses[2].X, 0f, 0.001f);
            Assert.AreEqual(poses[2].RotationZ, 0f, 0.001f);
        }

        [Test]
        public void FanPoses_OuterCardsRotateOutward()
        {
            var poses = ColonistRoguelikeCardHandLayout.ComputeFanPoses(5);
            // UI +Z rotation is counter-clockwise: in a held fan the left card's top leans left
            // (positive Z) and the right card's top leans right (negative Z). The previous signs
            // produced an inverted fan with the tops converging.
            Assert.Greater(poses[0].RotationZ, 0f, "Left card should lean left (CCW).");
            Assert.Less(poses[4].RotationZ, 0f, "Right card should lean right (CW).");
        }
    }
}
