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

        [Test]
        public void ComputeOverallDeadline_AddsOverallTimeout()
        {
            var deadline = GameViewCaptureFrameWait.ComputeOverallDeadline(50);
            Assert.AreEqual(50 + GameViewCaptureFrameWait.OverallTimeoutSeconds, deadline, 0.0001);
        }

        [Test]
        public void IsOverallTimedOutUtc_WhenPastDeadline_ReturnsTrue()
        {
            var started = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var deadline = GameViewCaptureFrameWait.ComputeOverallDeadlineUtc(started);

            Assert.False(GameViewCaptureFrameWait.IsOverallTimedOutUtc(started.AddSeconds(60), deadline));
            Assert.True(GameViewCaptureFrameWait.IsOverallTimedOutUtc(deadline.AddSeconds(1), deadline));
        }
    }
}
