using System;
using CatanRoguelike.Game;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class GameSceneCaptureTests
    {
        [Test]
        public void ResolveOutputPath_DefaultsWhenEnvMissingOrBlank()
        {
            var original = Environment.GetEnvironmentVariable(GameSceneCapture.OutputPathEnvVar);
            try
            {
                Environment.SetEnvironmentVariable(GameSceneCapture.OutputPathEnvVar, null);
                Assert.AreEqual(GameSceneCapture.DefaultOutputPath, GameSceneCapture.ResolveOutputPath());

                Environment.SetEnvironmentVariable(GameSceneCapture.OutputPathEnvVar, string.Empty);
                Assert.AreEqual(GameSceneCapture.DefaultOutputPath, GameSceneCapture.ResolveOutputPath());

                Environment.SetEnvironmentVariable(GameSceneCapture.OutputPathEnvVar, "   ");
                Assert.AreEqual(GameSceneCapture.DefaultOutputPath, GameSceneCapture.ResolveOutputPath());
            }
            finally
            {
                Environment.SetEnvironmentVariable(GameSceneCapture.OutputPathEnvVar, original);
            }
        }

        [Test]
        public void CaptureDimensions_MatchScreenshotContract()
        {
            Assert.AreEqual(1920, GameSceneCapture.CaptureWidth);
            Assert.AreEqual(1080, GameSceneCapture.CaptureHeight);
        }
    }
}
