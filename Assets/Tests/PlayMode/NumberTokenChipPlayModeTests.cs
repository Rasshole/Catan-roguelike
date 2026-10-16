using System.Collections;
using System.IO;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CatanRoguelike.Tests.PlayMode
{
    public class NumberTokenChipPlayModeTests
    {
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string GameSceneName = "Game";

        [Timeout(90000)]
        [UnityTest]
        public IEnumerator NumberTokens_HaveOpaqueChipsWithLabelsAboveChipTop()
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

            var tiles = Object.FindObjectsByType<HexTileView>(FindObjectsSortMode.None);
            int checkedTiles = 0;
            foreach (var tile in tiles)
            {
                if (tile.Data.IsDesert || !tile.Data.NumberToken.HasValue)
                    continue;

                Assert.IsTrue(tile.HasOpaqueTokenChip, $"Token {tile.Data.NumberToken} should use opaque chip material.");
                Assert.Greater(tile.LabelWorldY, tile.ChipTopWorldY + 0.001f,
                    $"Label should sit above chip top for token {tile.Data.NumberToken}.");
                Assert.IsFalse(string.IsNullOrEmpty(tile.TokenLabelText),
                    $"Token {tile.Data.NumberToken} should show a number label.");
                checkedTiles++;
            }

            Assert.Greater(checkedTiles, 6, "Board should show multiple numbered tokens after setup.");
        }
    }
}
