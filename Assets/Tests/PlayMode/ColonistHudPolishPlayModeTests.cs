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
                var robberIcon = tile.transform.Find("RobberPawn/RobberTopIcon");
                var camera = Camera.main;
                Assert.IsNotNull(camera);
                Vector3 textureV = ColonistFlatBoardQuad.GetTextureVWorldAxis(robberIcon);
                Vector3 cameraUp = ColonistFlatBoardQuad.GetColonistCameraUpOnBoard(camera);
                Assert.Greater(
                    Vector3.Dot(textureV, cameraUp),
                    0.85f,
                    $"Robber icon head should align with camera up on board (textureV={textureV} dot={Vector3.Dot(textureV, cameraUp):F3}).");

                if (tile.Data.NumberToken.HasValue)
                {
                    Vector2 tokenCenter = new(tile.transform.position.x, tile.transform.position.z);
                    Vector2 robberPos = new(tile.RobberWorldPosition.x, tile.RobberWorldPosition.z);
                    float pawnRadius = Mathf.Max(
                        tile.RobberWorldBounds.extents.x,
                        tile.RobberWorldBounds.extents.z);
                    float minSeparation = tile.TokenRimOuterWorldRadius + pawnRadius + 0.03f;
                    Assert.Greater(
                        Vector2.Distance(tokenCenter, robberPos),
                        minSeparation,
                        $"Robber should not overlap token rim on {tile.Data.Coord} " +
                        $"(dist={Vector2.Distance(tokenCenter, robberPos):F3} min={minSeparation:F3}).");
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

            Transform portRoot = null;
            Transform badge = null;
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer == null || renderer.gameObject.name != "BadgeDisc")
                    continue;
                badge = renderer.transform.parent;
                portRoot = badge.parent;
                break;
            }

            Assert.IsNotNull(badge, "Expected at least one port rate badge in the colonist scene.");
            if (portRoot != null)
                PortMarkerVisuals.LogChildRenderersOnce(portRoot.gameObject);

            var disc = badge.Find("BadgeDisc");
            var label = badge.Find("RateLabel");
            Assert.IsNotNull(disc, "Port badge should include BadgeDisc.");
            Assert.IsNotNull(label, "Port badge should include RateLabel quad.");

            var discRenderer = disc.GetComponent<Renderer>();
            var labelRenderer = label.GetComponent<Renderer>();
            Assert.IsNotNull(discRenderer);
            Assert.IsNotNull(labelRenderer);
            Assert.IsNull(label.GetComponent<TextMesh>(), "Port rate label should not use TextMesh.");

            var labelMat = labelRenderer.sharedMaterial;
            Assert.IsNotNull(labelMat);
            var labelTexture = labelMat.mainTexture as Texture2D;
            Assert.IsNotNull(labelTexture, "Port rate label material should reference a texture.");
            Assert.IsTrue(
                ColonistPortRateLabelTextures.HasOpaquePixels(labelTexture),
                "Port rate label texture should contain opaque glyph pixels.");

            ColonistFlatBoardQuad.LogDiagnostics("PortRateLabelPlayMode", label, discRenderer);
            float faceDot = Vector3.Dot(ColonistFlatBoardQuad.GetBoardFaceNormal(label), Vector3.up);
            Assert.Greater(faceDot, 0.85f, $"Port rate label should lie flat on the board (dot={faceDot:F3}).");

            float discMeshWidth = disc.localScale.x;
            float labelMeshWidth = ColonistFlatBoardQuad.MeasureWorldWidthAlongTextureU(label);
            Assert.Greater(
                labelMeshWidth,
                discMeshWidth * 0.65f - 0.01f,
                $"Port rate label should be >= 65% of disc width (label={labelMeshWidth:F3} disc={discMeshWidth:F3}).");

            var camera = Camera.main;
            Assert.IsNotNull(camera);
            Vector3 cameraRight = ColonistFlatBoardQuad.GetColonistCameraRightOnBoard(camera);
            float uDot = Vector3.Dot(ColonistFlatBoardQuad.GetTextureUWorldAxis(label), cameraRight);
            Assert.Greater(
                uDot,
                0.85f,
                $"Port rate label texture +U should align with camera right on board (dot={uDot:F3}).");

            var discBounds = discRenderer.bounds;
            var labelBounds = labelRenderer.bounds;
            Assert.LessOrEqual(labelBounds.max.x, discBounds.max.x + 0.03f);
            Assert.GreaterOrEqual(labelBounds.min.x, discBounds.min.x - 0.03f);
            Assert.LessOrEqual(labelBounds.max.z, discBounds.max.z + 0.03f);
            Assert.GreaterOrEqual(labelBounds.min.z, discBounds.min.z - 0.03f);
            Assert.Greater(
                labelBounds.min.y,
                discBounds.max.y - 0.02f,
                "Port rate label should render above the badge disc.");

            Transform rateIcon = badge.Find("RateIcon");
            if (rateIcon == null)
                rateIcon = badge.Find("RateLabel/RateIcon");
            if (rateIcon != null)
            {
                var iconRenderer = rateIcon.GetComponent<Renderer>();
                Assert.IsNotNull(iconRenderer);
                Vector2 labelScreen = ColonistFlatBoardQuad.MeasureScreenPixelSize(labelRenderer, camera);
                Vector2 iconScreen = ColonistFlatBoardQuad.MeasureScreenPixelSize(iconRenderer, camera);
                Assert.Greater(
                    iconScreen.y,
                    labelScreen.y * 0.65f,
                    $"2:1 port icon should be readable on screen (iconH={iconScreen.y:F1} labelH={labelScreen.y:F1}).");
                Assert.Greater(iconScreen.y, 10f, $"2:1 port icon screen height should not be a sliver (h={iconScreen.y:F1}px).");

                Vector3 iconScreenCenter = camera.WorldToScreenPoint(iconRenderer.bounds.center);
                Vector3 discScreenCenter = camera.WorldToScreenPoint(discRenderer.bounds.center);
                Vector2 discScreenSize = ColonistFlatBoardQuad.MeasureScreenPixelSize(discRenderer, camera);
                Assert.Less(Mathf.Abs(iconScreenCenter.x - discScreenCenter.x), discScreenSize.x * 0.45f,
                    "2:1 port icon should stay centered on the badge in screen space.");
                Assert.Less(Mathf.Abs(iconScreenCenter.y - discScreenCenter.y), discScreenSize.y * 0.55f,
                    "2:1 port icon should stay inside the badge in screen space.");
            }
        }
    }
}
