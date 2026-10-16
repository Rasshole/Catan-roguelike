using System.Collections;
using System.Collections.Generic;
using System.IO;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CatanRoguelike.Tests.PlayMode
{
    public class ColonistBoardFramingPlayModeTests
    {
        private const string ColonistScenePath = "Assets/_Project/Scenes/ColonistStylePrototype.unity";
        private const string ColonistSceneName = "ColonistStylePrototype";
        private const int HudReadyMaxFrames = 120;

        [Timeout(90000)]
        [UnityTest]
        public IEnumerator BoardContent_FitsPlayArea_WithTightFill_OnCapturePath()
        {
            if (!File.Exists(ColonistScenePath))
            {
                Assert.Inconclusive($"Colonist prototype not found at {ColonistScenePath}.");
                yield break;
            }

            int width = GameSceneCapture.CaptureWidth;
            int height = GameSceneCapture.CaptureHeight;
            Screen.SetResolution(width, height, false);
            yield return null;

            var loadOp = SceneManager.LoadSceneAsync(ColonistSceneName, LoadSceneMode.Single);
            yield return loadOp;
            for (int i = 0; i < 8; i++)
                yield return null;

            var gameManager = Object.FindFirstObjectByType<GameManager>();
            Assert.IsNotNull(gameManager);
            if (gameManager.Controller.State.Phase == GamePhase.RunSelectMap)
                GameScenePlayHarness.CompleteRunSelectAndSetup(gameManager.Controller, gameManager.Meta);
            GameScenePlayHarness.AdvanceToDayPlayerActions(gameManager.Controller);
            gameManager.EnsureRunHudReady();

            RunHudController hud = null;
            for (int i = 0; i < HudReadyMaxFrames; i++)
            {
                hud = gameManager.GetComponent<RunHudController>();
                if (hud != null && hud.IsHudCaptureReady())
                    break;
                gameManager.EnsureRunHudReady();
                yield return null;
            }

            Assert.IsNotNull(hud);
            Assert.IsTrue(hud.IsHudCaptureReady());
            hud.Refresh(gameManager.Controller.State);
            Canvas.ForceUpdateCanvases();
            if (hud.HudCanvas != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(hud.HudCanvas.GetComponent<RectTransform>());
            yield return null;

            var boardView = Object.FindFirstObjectByType<BoardView>();
            var tableCamera = Object.FindFirstObjectByType<TableCamera>();
            Assert.IsNotNull(boardView);
            Assert.IsNotNull(tableCamera);

            Texture2D boardOnly = GameSceneCapture.CaptureBoardOnlyTexture(width, height);
            try
            {
                var camera = Camera.main;
                Assert.IsNotNull(camera);
                Assert.IsNotNull(boardOnly);

                ColonistBoardFraming.LogCaptureFramingState(camera, width, height);

                var samples = new List<Vector3>(512);
                boardView.FillColonistFramingSamples(samples);
                Assert.Greater(samples.Count, 8, "Colonist framing should sample land, sand, and port badges.");

                var screenPoints = new Vector2[samples.Count];
                for (int i = 0; i < samples.Count; i++)
                    screenPoints[i] = ColonistBoardFraming.WorldToLayoutPixels(camera, samples[i], width, height);

                bool inside = ColonistBoardFraming.ArePointsInsideFitMargin(screenPoints, width, height);
                if (!inside)
                {
                    ColonistBoardFraming.LogPlayAreaFitDiagnostics(screenPoints, width, height);
                    if (ColonistBoardFraming.TryGetFirstSampleOutsideFitMargin(
                            samples, camera, width, height, out int badIndex, out Vector3 badWorld, out Vector2 badPx))
                    {
                        Debug.LogWarning(
                            $"ColonistBoardFraming offender sample[{badIndex}] world={badWorld} screen=({badPx.x:F1},{badPx.y:F1})");
                    }
                }

                Assert.IsTrue(
                    inside,
                    "Land+sand+badges should fit inside colonist play area (3% margin) on capture camera pose.");

                ColonistBoardFraming.MeasurePlayAreaFill(camera, samples, width, height, out float fillWidth, out float fillHeight);
                float fill = Mathf.Max(fillWidth, fillHeight);
                Debug.Log(
                    $"ColonistBoardFraming fillWidth={fillWidth:F3} fillHeight={fillHeight:F3} " +
                    $"layout={width}x{height} ortho={camera.orthographicSize:F4}");
                Assert.GreaterOrEqual(
                    fill,
                    ColonistBoardFraming.MinPlayAreaFillRatio,
                    $"Board content should fill at least {ColonistBoardFraming.MinPlayAreaFillRatio:P0} of play rect " +
                    $"(fillW={fillWidth:F3} fillH={fillHeight:F3}).");

                ColonistBoardFraming.AssertBoardTopBelowTurnBanner(camera, boardView, width, height);
            }
            finally
            {
                if (boardOnly != null)
                    Object.Destroy(boardOnly);
            }
        }
    }
}
