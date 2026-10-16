using System.Collections;
using System.IO;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CatanRoguelike.Tests.PlayMode
{
    public class ColonistResourceIconPlayModeTests
    {
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string GameSceneName = "Game";

        [Timeout(90000)]
        [UnityTest]
        public IEnumerator NonDesertHexes_ShowResourceIconsInCameraFrustum()
        {
            if (!File.Exists(GameScenePath))
            {
                Assert.Inconclusive($"Game scene not found at {GameScenePath}.");
                yield break;
            }

            var loadOp = SceneManager.LoadSceneAsync(GameSceneName, LoadSceneMode.Single);
            yield return loadOp;
            for (int i = 0; i < 4; i++)
                yield return null;

            var gameManager = Object.FindFirstObjectByType<GameManager>();
            GameScenePlayHarness.CompleteRunSelectAndSetup(gameManager.Controller, gameManager.Meta);
            for (int i = 0; i < 4; i++)
                yield return null;

            var camera = Camera.main;
            Assert.IsNotNull(camera, "Main camera required for icon visibility check.");
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);

            var tiles = Object.FindObjectsByType<HexTileView>(FindObjectsSortMode.None);
            int iconsChecked = 0;
            foreach (var tile in tiles)
            {
                if (tile.Data.IsDesert)
                    continue;

                var iconTransform = tile.transform.Find("ResourceIcon");
                Assert.IsNotNull(iconTransform, $"Hex {tile.Data.Coord} should have a ResourceIcon child.");
                var renderer = iconTransform.GetComponent<Renderer>();
                Assert.IsNotNull(renderer, $"ResourceIcon on {tile.Data.Coord} should have a Renderer.");
                Assert.IsTrue(renderer.enabled && iconTransform.gameObject.activeInHierarchy,
                    $"ResourceIcon on {tile.Data.Coord} should be active.");
                Assert.IsTrue(
                    BuiltInMaterials.IsUnlitMaterial(renderer.sharedMaterial),
                    $"ResourceIcon on {tile.Data.Coord} should use an unlit material.");

                Assert.IsTrue(
                    GeometryUtility.TestPlanesAABB(planes, renderer.bounds),
                    $"ResourceIcon on {tile.Data.Coord} should be inside the camera frustum.");
                iconsChecked++;
            }

            Assert.Greater(iconsChecked, 6, "Board should show resource icons on multiple non-desert hexes.");
        }
    }
}
