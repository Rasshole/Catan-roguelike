using System.Collections;
using System.IO;
using CatanRoguelike.Core.Map;
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
        public IEnumerator NonDesertHexes_UseNatureDecorNotCornerResourceGlyphs()
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

            int tilesChecked = 0;
            foreach (var tile in Object.FindObjectsByType<HexTileView>(FindObjectsSortMode.None))
            {
                if (tile.Data.IsDesert || BoardState.IsSeaFishGround(tile.Data))
                    continue;

                Assert.IsNull(
                    tile.transform.Find("ResourceIcon"),
                    $"Hex {tile.Data.Coord} should not show a corner ResourceIcon (terrain decor only).");
                Assert.IsNotNull(
                    tile.transform.Find("TileNatureProps"),
                    $"Hex {tile.Data.Coord} should have TileNatureProps terrain decor.");
                tilesChecked++;
            }

            Assert.Greater(tilesChecked, 6, "Board should show multiple productive land hexes.");
        }
    }
}
