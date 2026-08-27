using CatanRoguelike.Game;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class GameSceneCaptureTests
    {
        [Test]
        public void ResolveOutputPath_DefaultsWhenEnvMissingOrBlank()
        {
            Assert.AreEqual(GameSceneCapture.DefaultOutputPath, GameSceneCapture.ResolveOutputPath());
        }

        [Test]
        public void CaptureDimensions_MatchScreenshotContract()
        {
            Assert.AreEqual(1920, GameSceneCapture.CaptureWidth);
            Assert.AreEqual(1080, GameSceneCapture.CaptureHeight);
        }
    }
}
