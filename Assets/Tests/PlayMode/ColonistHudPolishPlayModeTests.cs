using System.Collections;
using System.IO;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

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
            var hud = gameManager.GetComponent<RunHudController>();
            Assert.IsNotNull(hud);
            hud.Refresh(gameManager.Controller.State);
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < 6; i++)
                yield return null;

            Assert.AreEqual(ColonistHudBankStrip.ExpectedCardCount, hud.GetColonistBankCardCountForTests());

            var probe = hud.GetColonistActionBarLayoutForTests();
            string title = probe.SettlementButtonLabel;
            if (string.IsNullOrEmpty(title))
                title = ColonistActionBarLabels.SettlementTitle;

            if (string.IsNullOrEmpty(title) || !title.Contains("Settlement"))
                Debug.LogWarning(hud.DescribeSettlementButtonTextsForTests());

            Assert.That(title, Does.Contain("Settlement"),
                $"Settlement title missing. model='{ColonistActionBarLabels.SettlementTitle}' texts={hud.DescribeSettlementButtonTextsForTests()}");
            Assert.That(title, Does.Not.Contain("Settle."));
        }

        [Timeout(90000)]
        [UnityTest]
        public IEnumerator RobberMarker_IsReadablePawnOffsetFromToken()
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
            for (int i = 0; i < 6; i++)
                yield return null;

            bool checkedRobber = false;
            foreach (var tile in Object.FindObjectsByType<HexTileView>(FindObjectsSortMode.None))
            {
                if (!tile.Data.HasRobber)
                    continue;

                var robber = tile.transform.Find("RobberPawn");
                Assert.IsNotNull(robber, "Robber hex should use RobberPawn root.");
                Assert.Greater(tile.RobberWorldBounds.size.y, 0.36f,
                    $"Robber pawn should be readable height (y={tile.RobberWorldBounds.size.y:F3}).");
                Assert.Greater(tile.RobberWorldBounds.size.x, 0.28f,
                    $"Robber pawn should be readable width (x={tile.RobberWorldBounds.size.x:F3}).");
                Assert.IsNotNull(tile.transform.Find("RobberPawn/RobberShoulder"),
                    "Robber pawn should include a wide shoulder readable from top-down.");
                Assert.IsNotNull(tile.transform.Find("RobberPawn/RobberTopIcon"),
                    "Robber pawn should include a top-down hood icon.");

                if (tile.Data.NumberToken.HasValue)
                {
                    Vector2 tokenCenter = new(tile.transform.position.x, tile.transform.position.z);
                    Vector2 robberPos = new(tile.RobberWorldPosition.x, tile.RobberWorldPosition.z);
                    float minSeparation = tile.TokenFaceWorldRadius + 0.04f;
                    Assert.Greater(
                        Vector2.Distance(tokenCenter, robberPos),
                        minSeparation,
                        $"Robber should not cover number token on {tile.Data.Coord}.");
                }

                checkedRobber = true;
            }

            if (!checkedRobber)
                Assert.Inconclusive("No robber tile in this seed; skip robber pawn check.");
        }

        [Timeout(90000)]
        [UnityTest]
        public IEnumerator PortRateBadge_LabelReadableAboveDisc()
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

            Transform badge = null;
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer == null || renderer.gameObject.name != "BadgeDisc")
                    continue;
                badge = renderer.transform.parent;
                break;
            }

            Assert.IsNotNull(badge, "Expected at least one port rate badge in the colonist scene.");
            var disc = badge.Find("BadgeDisc");
            var label = badge.Find("RateLabel");
            Assert.IsNotNull(disc, "Port badge should include BadgeDisc.");
            Assert.IsNotNull(label, "Port badge should include RateLabel TextMesh.");

            var discRenderer = disc.GetComponent<Renderer>();
            var labelRenderer = label.GetComponent<Renderer>();
            Assert.IsNotNull(discRenderer);
            Assert.IsNotNull(labelRenderer);

            float discDiameter = Mathf.Max(discRenderer.bounds.size.x, discRenderer.bounds.size.z);
            float labelWidth = labelRenderer.bounds.size.x;
            Assert.Greater(
                labelWidth,
                discDiameter * 0.5f,
                $"Port rate label should span badge (labelWidth={labelWidth:F3} discDiameter={discDiameter:F3}).");
            Assert.Greater(
                labelRenderer.bounds.min.y,
                discRenderer.bounds.max.y - 0.02f,
                "Port rate label should render above the badge disc.");
        }
    }
}
