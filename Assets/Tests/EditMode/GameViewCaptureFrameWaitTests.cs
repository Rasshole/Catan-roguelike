using CatanRoguelike.Editor;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class GameViewCaptureFrameWaitTests
    {
        [Test]
        public void HasReachedTarget_WhenAtOrPastTarget_ReturnsTrue()
        {
            Assert.True(GameViewCaptureFrameWait.HasReachedTarget(10, 10));
            Assert.True(GameViewCaptureFrameWait.HasReachedTarget(11, 10));
            Assert.False(GameViewCaptureFrameWait.HasReachedTarget(9, 10));
        }

        [Test]
        public void IsTimedOut_WhenPastDeadline_ReturnsTrue()
        {
            Assert.False(GameViewCaptureFrameWait.IsTimedOut(100, 120));
            Assert.True(GameViewCaptureFrameWait.IsTimedOut(121, 120));
        }

        [Test]
        public void ComputeDeadline_AddsPhaseTimeout()
        {
            var deadline = GameViewCaptureFrameWait.ComputeDeadline(50);
            Assert.AreEqual(50 + GameViewCaptureFrameWait.PhaseTimeoutSeconds, deadline, 0.0001);
        }
    }
}
