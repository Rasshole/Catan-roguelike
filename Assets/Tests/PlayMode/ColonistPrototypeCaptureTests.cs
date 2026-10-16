using System;
using System.Collections;
using System.IO;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CatanRoguelike.Tests.PlayMode
{
    public class ColonistPrototypeCaptureTests
    {
        private const string ColonistScenePath = "Assets/_Project/Scenes/ColonistStylePrototype.unity";
        private const string ColonistSceneName = "ColonistStylePrototype";

        [Timeout(90000)]
        [UnityTest]
        public IEnumerator ColonistPrototype_CaptureWithHud_WritesPng()
        {
            if (!File.Exists(ColonistScenePath))
            {
                Assert.Inconclusive(
                    $"Colonist prototype not found at {ColonistScenePath}. " +
                    "Run Catan Roguelike → Build Colonist Style Prototype Scene.");
            }

            Screen.SetResolution(GameSceneCapture.CaptureWidth, GameSceneCapture.CaptureHeight, false);
            yield return null;

            var loadOp = SceneManager.LoadSceneAsync(ColonistSceneName, LoadSceneMode.Single);
            Assert.IsNotNull(loadOp, $"Failed to load '{ColonistSceneName}'. Add it to Build Settings.");
            yield return loadOp;

            for (int i = 0; i < 4; i++)
                yield return null;

            var gameManager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            Assert.IsNotNull(gameManager, "GameManager should exist after scene load.");
            Assert.IsNotNull(gameManager.Controller, "Controller should be initialized after Start().");
            Assert.IsNotNull(gameManager.Meta, "Meta should be loaded after Start().");

            if (gameManager.Controller.State.Phase == GamePhase.RunSelectMap)
                GameScenePlayHarness.CompleteRunSelectAndSetup(gameManager.Controller, gameManager.Meta);

            Assert.IsFalse(gameManager.Controller.State.IsSetupPhase,
                "Harness should finish setup before capture.");

            var hud = UnityEngine.Object.FindFirstObjectByType<RunHudController>();
            Assert.IsNotNull(hud, "RunHudController should be present on the colonist prototype.");

            for (int i = 0; i < 12; i++)
                yield return null;
            yield return new WaitForEndOfFrame();

            Texture2D capture = GameSceneCapture.CaptureMainCameraToTexture(hud);
            try
            {
                AssertTextureHasVisibleContent(capture);

                var envPath = Environment.GetEnvironmentVariable(GameSceneCapture.OutputPathEnvVar);
                if (!string.IsNullOrWhiteSpace(envPath))
                {
                    var outputPath = envPath.Trim();
                    var directory = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);
                    File.WriteAllBytes(outputPath, capture.EncodeToPNG());
                }
            }
            finally
            {
                if (capture != null)
                    UnityEngine.Object.Destroy(capture);
            }
        }

        private static void AssertTextureHasVisibleContent(Texture2D texture)
        {
            Assert.IsNotNull(texture, "Capture texture should be created.");
            Assert.AreEqual(GameSceneCapture.CaptureWidth, texture.width);
            Assert.AreEqual(GameSceneCapture.CaptureHeight, texture.height);

            float luminance = 0f;
            int samples = 0;
            int w = texture.width;
            int h = texture.height;
            int[] xs = { w / 4, w / 2, (3 * w) / 4 };
            int[] ys = { h / 4, h / 2, (3 * h) / 4 };
            foreach (int x in xs)
            {
                foreach (int y in ys)
                {
                    var c = texture.GetPixel(x, y);
                    luminance += c.r + c.g + c.b;
                    samples++;
                }
            }

            Assert.Greater(luminance, 0.05f * samples,
                "Capture should contain non-black pixels (HUD + board).");
        }
    }
}
