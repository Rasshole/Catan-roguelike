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
        private const int HudReadyMaxFrames = 120;

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

            gameManager.EnsureRunHudReady();

            Assert.IsFalse(gameManager.Controller.State.IsSetupPhase,
                "Harness should finish setup before capture.");

            RunHudController hud = null;
            for (int i = 0; i < HudReadyMaxFrames; i++)
            {
                hud = gameManager.GetComponent<RunHudController>();
                if (hud != null && hud.IsHudCaptureReady())
                    break;
                gameManager.EnsureRunHudReady();
                yield return null;
            }

            Assert.IsNotNull(hud, "RunHudController should be present on the colonist prototype.");
            int graphicCount = hud != null ? hud.CountHudGraphics() : 0;
            Debug.Log($"Colonist capture HUD graphics count: {graphicCount}");
            Assert.IsTrue(hud.IsHudCaptureReady(),
                $"HUD canvas should be built with graphics before capture (found {graphicCount}).");

            for (int i = 0; i < 4; i++)
                yield return null;

            Texture2D boardOnly = GameSceneCapture.CaptureMainCameraToTexture(null);
            Texture2D withHud = GameSceneCapture.CaptureMainCameraToTexture(hud);
            try
            {
                AssertTextureHasVisibleContent(withHud);
                AssertBottomBandShowsHud(boardOnly, withHud);

                var envPath = Environment.GetEnvironmentVariable(GameSceneCapture.OutputPathEnvVar);
                if (!string.IsNullOrWhiteSpace(envPath))
                {
                    var outputPath = envPath.Trim();
                    var directory = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);
                    File.WriteAllBytes(outputPath, withHud.EncodeToPNG());
                }
            }
            finally
            {
                if (boardOnly != null)
                    UnityEngine.Object.Destroy(boardOnly);
                if (withHud != null)
                    UnityEngine.Object.Destroy(withHud);
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

        private static void AssertBottomBandShowsHud(Texture2D boardOnly, Texture2D withHud)
        {
            int w = withHud.width;
            int h = withHud.height;
            int bandStartY = (int)(h * 0.9f);
            int differing = 0;
            int samples = 0;

            for (int x = 0; x < w; x += 32)
            {
                for (int y = bandStartY; y < h; y += 8)
                {
                    var a = boardOnly.GetPixel(x, y);
                    var b = withHud.GetPixel(x, y);
                    if (ColorDistance(a, b) > 0.04f)
                        differing++;
                    samples++;
                }
            }

            Assert.Greater(differing, samples / 10,
                "Bottom 10% of frame should differ when HUD is composited (hand/action bar).");
        }

        private static float ColorDistance(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
        }
    }
}
