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
    public class ColonistHudPolishPlayModeTests
    {
        private const string ColonistScenePath = "Assets/_Project/Scenes/ColonistStylePrototype.unity";
        private const string ColonistSceneName = "ColonistStylePrototype";

        [Timeout(90000)]
        [UnityTest]
        public IEnumerator ColonistHud_ShowsBankCardsAndFullSettlementLabel()
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

            var gameManager = Object.FindFirstObjectByType<GameManager>();
            if (gameManager.Controller.State.Phase == GamePhase.RunSelectMap)
                GameScenePlayHarness.CompleteRunSelectAndSetup(gameManager.Controller, gameManager.Meta);
            GameScenePlayHarness.AdvanceToDayPlayerActions(gameManager.Controller);
            gameManager.EnsureRunHudReady();
            for (int i = 0; i < 6; i++)
                yield return null;

            var hud = gameManager.GetComponent<RunHudController>();
            Assert.IsNotNull(hud);
            Assert.AreEqual(ColonistHudBankStrip.ExpectedCardCount, hud.GetColonistBankCardCountForTests());

            var probe = hud.GetColonistActionBarLayoutForTests();
            Assert.That(probe.SettlementButtonLabel, Does.Contain("Settlement"));
            Assert.That(probe.SettlementButtonLabel, Does.Not.Contain("Settle."));
        }

        [Timeout(90000)]
        [UnityTest]
        public IEnumerator RobberMarker_OffsetFromTokenCenterOnSharedHex()
        {
            if (!File.Exists(ColonistScenePath))
            {
                Assert.Inconclusive($"Colonist prototype not found at {ColonistScenePath}.");
                yield break;
            }

            var loadOp = SceneManager.LoadSceneAsync(ColonistSceneName, LoadSceneMode.Single);
            yield return loadOp;
            for (int i = 0; i < 6; i++)
                yield return null;

            var gameManager = Object.FindFirstObjectByType<GameManager>();
            GameScenePlayHarness.CompleteRunSelectAndSetup(gameManager.Controller, gameManager.Meta);
            for (int i = 0; i < 4; i++)
                yield return null;

            bool checkedRobber = false;
            foreach (var tile in Object.FindObjectsByType<HexTileView>(FindObjectsSortMode.None))
            {
                if (!tile.Data.HasRobber || !tile.Data.NumberToken.HasValue)
                    continue;

                var robber = tile.transform.Find("Robber");
                Assert.IsNotNull(robber, "Robber hex should have a robber marker root.");
                Vector2 tokenCenter = new(tile.transform.position.x, tile.transform.position.z);
                Vector2 robberPos = new(tile.RobberWorldPosition.x, tile.RobberWorldPosition.z);
                float minSeparation = tile.TokenFaceWorldRadius + 0.04f;
                Assert.Greater(
                    Vector2.Distance(tokenCenter, robberPos),
                    minSeparation,
                    $"Robber should not cover number token on {tile.Data.Coord}.");
                checkedRobber = true;
            }

            if (!checkedRobber)
                Assert.Inconclusive("No numbered hex with robber in this seed; skip overlap check.");
        }
    }
}
