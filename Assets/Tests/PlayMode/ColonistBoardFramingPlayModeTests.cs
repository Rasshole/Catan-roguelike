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
    public class ColonistBoardFramingPlayModeTests
    {
        private const string ColonistScenePath = "Assets/_Project/Scenes/ColonistStylePrototype.unity";
        private const string ColonistSceneName = "ColonistStylePrototype";

        [Timeout(90000)]
        [UnityTest]
        public IEnumerator BoardExtent_ProjectsInsidePlayArea_WithMargin()
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

            Vector3 center = boardView.GetBoardWorldCenter();
            float extent = boardView.GetColonistFramingExtentRadius();
            var worldPoints = new Vector3[8];
            ColonistBoardFraming.GetExtentSamplePoints(center, extent, worldPoints);

            var screenPoints = new Vector2[worldPoints.Length];
            for (int i = 0; i < worldPoints.Length; i++)
                screenPoints[i] = ColonistBoardFraming.WorldToLayoutPixels(camera, worldPoints[i], layoutW, layoutH);

            bool inside = ColonistBoardFraming.ArePointsInsidePlayArea(screenPoints, layoutW, layoutH);
            if (!inside)
                ColonistBoardFraming.LogPlayAreaFitDiagnostics(screenPoints, layoutW, layoutH);

            Assert.IsTrue(inside, "Board+sand extent should fit inside colonist play area with margin.");

            float diameter = extent * 2f;
            float ortho = camera.orthographicSize;
            float aspect = Mathf.Max(0.01f, camera.aspect);
            float boardLayoutWidthPixels = diameter / (2f * ortho * aspect) * layoutW;

            var play = ColonistPlayAreaLayout.Compute(layoutW, layoutH);
            float margin = ColonistUiScale.ScaleReferencePixels(
                ColonistBoardFraming.PlayAreaMarginPixels, layoutW, layoutH);
            float usablePlayWidth = Mathf.Max(8f, play.PlayWidth - margin * 2f);
            float boardWidthOfPlayRect = boardLayoutWidthPixels / usablePlayWidth;
            Debug.Log(
                $"ColonistBoardFraming boardWidthOfPlayRect={boardWidthOfPlayRect:F3} " +
                $"boardLayoutWidthPx={boardLayoutWidthPixels:F1} usablePlayWidth={usablePlayWidth:F1} " +
                $"layout={layoutW}x{layoutH} diameter={diameter:F3} ortho={ortho:F4}");
            Assert.LessOrEqual(
                boardWidthOfPlayRect,
                0.9f,
                "Board+sand should fit inside margined play width without shrinking ortho.");
        }
    }
}
