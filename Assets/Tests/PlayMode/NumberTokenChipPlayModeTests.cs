using System.Collections;
using System.IO;
using CatanRoguelike.Core.Yield;
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

                int token = tile.Data.NumberToken.Value;
                Assert.IsTrue(tile.ChipUsesUnlitMaterial, $"Token {token} chip should use unlit material.");
                Assert.IsTrue(tile.IsChipFlat, $"Token {token} chip should be flat (d={tile.ChipWorldDiameter:F3} h={tile.ChipWorldHeight:F3}).");

                var faceRenderer = tile.TokenFaceRenderer;
                var rimRenderer = tile.TokenRimRenderer;
                Assert.IsNotNull(faceRenderer, $"Token {token} should have a face renderer.");
                Assert.IsNotNull(rimRenderer, $"Token {token} should have a rim renderer.");
                Assert.IsTrue(faceRenderer.enabled && rimRenderer.enabled, $"Token {token} chip renderers should be enabled.");
                float hexTop = tile.HexTopWorldY;
                Assert.GreaterOrEqual(faceRenderer.bounds.min.y, hexTop - 0.002f,
                    $"Token {token} face should sit on/above hex top (faceMinY={faceRenderer.bounds.min.y:F4} hexTop={hexTop:F4}).");
                Assert.GreaterOrEqual(rimRenderer.bounds.min.y, hexTop - 0.002f,
                    $"Token {token} rim should sit on/above hex top (rimMinY={rimRenderer.bounds.min.y:F4} hexTop={hexTop:F4}).");
                float faceWidth = Mathf.Max(faceRenderer.bounds.size.x, faceRenderer.bounds.size.z);
                float rimWidth = Mathf.Max(rimRenderer.bounds.size.x, rimRenderer.bounds.size.z);
                Assert.Greater(rimWidth, faceWidth * 1.05f,
                    $"Token {token} rim should be wider than face (rim={rimWidth:F3} face={faceWidth:F3}).");
                Assert.Greater(tile.LabelWorldY, tile.ChipTopWorldY + 0.001f,
                    $"Label should sit above chip top for token {token}.");
                Assert.IsFalse(string.IsNullOrEmpty(tile.TokenLabelText),
                    $"Token {token} should show a number label.");
                Assert.AreEqual(
                    NumberTokenPipVisuals.GetExpectedPipCount(token),
                    tile.PipChildCount,
                    $"Token {token} should show the correct pip count.");
                checkedTiles++;
            }

            Assert.Greater(checkedTiles, 6, "Board should show multiple numbered tokens after setup.");
        }
    }
}
