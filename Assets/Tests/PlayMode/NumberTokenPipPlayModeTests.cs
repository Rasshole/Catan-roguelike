using System.Collections;
using System.IO;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CatanRoguelike.Tests.PlayMode
{
    public class NumberTokenPipPlayModeTests
    {
        private const string ColonistScenePath = "Assets/_Project/Scenes/ColonistStylePrototype.unity";
        private const string ColonistSceneName = "ColonistStylePrototype";

        [Timeout(90000)]
        [UnityTest]
        public IEnumerator NumberTokenPips_MatchClassicCounts_OnColonistBoard()
        {
            if (!File.Exists(ColonistScenePath))
            {
                Assert.Inconclusive($"Colonist prototype not found at {ColonistScenePath}.");
                yield break;
            }

            var loadOp = SceneManager.LoadSceneAsync(ColonistSceneName, LoadSceneMode.Single);
            yield return loadOp;
            for (int i = 0; i < 8; i++)
                yield return null;

            var tiles = Object.FindObjectsByType<HexTileView>(FindObjectsSortMode.None);
            Assert.Greater(tiles.Length, 0);

            int checkedTiles = 0;
            foreach (var tile in tiles)
            {
                if (tile.Data.IsDesert || !tile.Data.NumberToken.HasValue)
                    continue;

                int token = tile.Data.NumberToken.Value;
                int expected = NumberTokenPipVisuals.GetExpectedPipCount(token);
                Assert.AreEqual(expected, tile.PipChildCount,
                    $"Token {token} on {tile.Data.Coord} should spawn {expected} pip children.");
                checkedTiles++;
            }

            Assert.Greater(checkedTiles, 6, "Board should expose multiple numbered tokens for pip checks.");
        }
    }
}
