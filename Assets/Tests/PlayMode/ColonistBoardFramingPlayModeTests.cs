using System.Collections;
using System.IO;
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

            var boardView = Object.FindFirstObjectByType<BoardView>();
            var tableCamera = Object.FindFirstObjectByType<TableCamera>();
            var camera = Camera.main;
            Assert.IsNotNull(boardView);
            Assert.IsNotNull(tableCamera);
            Assert.IsNotNull(camera);

            tableCamera.ApplyCapturePose(width, height);
            camera.pixelRect = new Rect(0f, 0f, width, height);

            Vector3 center = boardView.GetBoardWorldCenter();
            float extent = boardView.GetColonistFramingExtentRadius();
            var worldPoints = new Vector3[8];
            ColonistBoardFraming.GetExtentSamplePoints(center, extent, worldPoints);

            var screenPoints = new Vector2[worldPoints.Length];
            for (int i = 0; i < worldPoints.Length; i++)
                screenPoints[i] = ColonistBoardFraming.WorldToScreenPixelsFromBottom(camera, worldPoints[i]);

            Assert.IsTrue(
                ColonistBoardFraming.ArePointsInsidePlayArea(screenPoints, width, height),
                "Board+sand extent should fit inside colonist play area with margin.");
        }
    }
}
