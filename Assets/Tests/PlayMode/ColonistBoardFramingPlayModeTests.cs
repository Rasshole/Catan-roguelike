using System.Collections;
using System.Collections.Generic;
using System.IO;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CatanRoguelike.Tests.PlayMode
{
    public class ColonistBoardFramingPlayModeTests
    {
        private const string ColonistScenePath = "Assets/_Project/Scenes/ColonistStylePrototype.unity";
        private const string ColonistSceneName = "ColonistStylePrototype";

        [Timeout(90000)]
        [UnityTest]
        public IEnumerator BoardContent_FitsPlayArea_WithTightFill()
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
            if (gameManager != null && gameManager.Controller != null)
            {
                if (gameManager.Controller.State.Phase == GamePhase.RunSelectMap)
                    GameScenePlayHarness.CompleteRunSelectAndSetup(gameManager.Controller, gameManager.Meta);
                GameScenePlayHarness.AdvanceToDayPlayerActions(gameManager.Controller);
                gameManager.EnsureRunHudReady();
                for (int i = 0; i < 4; i++)
                    yield return null;
            }

            var boardView = Object.FindFirstObjectByType<BoardView>();
            var tableCamera = Object.FindFirstObjectByType<TableCamera>();
            var camera = Camera.main;
            Assert.IsNotNull(boardView);
            Assert.IsNotNull(tableCamera);
            Assert.IsNotNull(camera);

            ColonistBoardFraming.GetLayoutPixelSize(camera, width, height, out int layoutW, out int layoutH);
            tableCamera.ApplyCapturePose(layoutW, layoutH);
            camera.pixelRect = new Rect(0f, 0f, layoutW, layoutH);
            yield return null;

            var samples = new List<Vector3>(512);
            boardView.FillColonistFramingSamples(samples);
            Assert.Greater(samples.Count, 8, "Colonist framing should sample land, sand, and port badges.");

            var screenPoints = new Vector2[samples.Count];
            for (int i = 0; i < samples.Count; i++)
                screenPoints[i] = ColonistBoardFraming.WorldToLayoutPixels(camera, samples[i], layoutW, layoutH);

            bool inside = ColonistBoardFraming.ArePointsInsideFitMargin(screenPoints, layoutW, layoutH);
            if (!inside)
            {
                ColonistBoardFraming.LogPlayAreaFitDiagnostics(screenPoints, layoutW, layoutH);
                if (ColonistBoardFraming.TryGetFirstSampleOutsideFitMargin(
                        samples, camera, layoutW, layoutH, out int badIndex, out Vector3 badWorld, out Vector2 badPx))
                {
                    Debug.LogWarning(
                        $"ColonistBoardFraming offender sample[{badIndex}] world={badWorld} screen=({badPx.x:F1},{badPx.y:F1})");
                }
            }

            Assert.IsTrue(
                inside,
                "Land+sand+badges should fit inside colonist play area (3% margin). See offender log if failing.");

            ColonistBoardFraming.MeasurePlayAreaFill(camera, samples, layoutW, layoutH, out float fillWidth, out float fillHeight);
            float fill = Mathf.Max(fillWidth, fillHeight);
            Debug.Log(
                $"ColonistBoardFraming fillWidth={fillWidth:F3} fillHeight={fillHeight:F3} " +
                $"layout={layoutW}x{layoutH} ortho={camera.orthographicSize:F4}");
            Assert.GreaterOrEqual(
                fill,
                ColonistBoardFraming.MinPlayAreaFillRatio,
                $"Board content should fill at least {ColonistBoardFraming.MinPlayAreaFillRatio:P0} of play rect " +
                $"(fillW={fillWidth:F3} fillH={fillHeight:F3}).");
        }
    }
}
