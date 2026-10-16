using System.Collections;
using System.IO;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CatanRoguelike.Tests.PlayMode
{
    public class ColonistHexProjectionPlayModeTests
    {
        private const string ColonistScenePath = "Assets/_Project/Scenes/ColonistStylePrototype.unity";
        private const string ColonistSceneName = "ColonistStylePrototype";

        [Timeout(90000)]
        [UnityTest]
        public IEnumerator HexTop_ProjectedAspectRatio_IsNearPointyTopClassic()
        {
            if (!File.Exists(ColonistScenePath))
            {
                Assert.Inconclusive($"Colonist prototype not found at {ColonistScenePath}.");
                yield break;
            }

            Screen.SetResolution(GameSceneCapture.CaptureWidth, GameSceneCapture.CaptureHeight, false);
            yield return null;

            var loadOp = SceneManager.LoadSceneAsync(ColonistSceneName, LoadSceneMode.Single);
            yield return loadOp;
            for (int i = 0; i < 6; i++)
                yield return null;

            var boardView = UnityEngine.Object.FindFirstObjectByType<BoardView>();
            var tableCamera = UnityEngine.Object.FindFirstObjectByType<TableCamera>();
            var camera = Camera.main;
            Assert.IsNotNull(boardView);
            Assert.IsNotNull(tableCamera);
            Assert.IsNotNull(camera);

            var tile = UnityEngine.Object.FindFirstObjectByType<HexTileView>();
            Assert.IsNotNull(tile, "Colonist board should include at least one hex tile.");

            float worldXz = ColonistCaptureProjection.MeasureHexTopWorldXzAspectRatio(tile.transform);
            Debug.Log($"Colonist hex world XZ width/height={worldXz:F3}");
            Assert.Greater(worldXz, 0.80f, "Hex footprint should not be stretched wide in world XZ.");
            Assert.Less(worldXz, 0.92f, "Hex footprint should not be stretched tall in world XZ.");

            float ratio = ColonistCaptureProjection.MeasureHexTopScreenAspectRatio(
                tile.transform,
                camera,
                tableCamera,
                GameSceneCapture.CaptureWidth,
                GameSceneCapture.CaptureHeight);

            Debug.Log($"Colonist hex projected aspect width/height={ratio:F3}");
            Assert.Greater(ratio, 1.05f, "Hex should not appear vertically squashed (too tall on screen).");
            Assert.Less(ratio, 1.28f, "Hex should not appear horizontally stretched (too wide on screen).");
        }
    }
}
