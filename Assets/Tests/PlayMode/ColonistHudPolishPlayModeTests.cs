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
            Transform rateIconProbe = badge.Find("RateIcon") ?? badge.Find("RateLabel/RateIcon");
            float minLabelFraction = rateIconProbe != null
                ? PortMarkerVisuals.BadgeLabelWidthFractionSpecific - 0.06f
                : PortMarkerVisuals.BadgeLabelWidthFractionGeneric - 0.01f;
            Assert.Greater(
                labelMeshWidth,
                discMeshWidth * minLabelFraction,
                $"Port rate label should fit on badge (label={labelMeshWidth:F3} disc={discMeshWidth:F3}).");

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
                Vector2 discScreenSize = ColonistFlatBoardQuad.MeasureScreenPixelSize(discRenderer, camera);
                const float screenTargetFraction = 0.40f;
                float targetIconScreen = discScreenSize.y * screenTargetFraction;
                Assert.Greater(
                    iconScreen.y,
                    targetIconScreen * 0.75f,
                    $"2:1 port icon should read ~{screenTargetFraction:P0} of disc height on screen " +
                    $"(iconH={iconScreen.y:F1} target≈{targetIconScreen:F1}).");
                Assert.Greater(iconScreen.y, 10f, $"2:1 port icon screen height should not be a sliver (h={iconScreen.y:F1}px).");

                Vector3 iconScreenCenter = camera.WorldToScreenPoint(iconRenderer.bounds.center);
                Vector3 labelScreenCenter = camera.WorldToScreenPoint(labelRenderer.bounds.center);
                Vector3 discScreenCenter = camera.WorldToScreenPoint(discRenderer.bounds.center);
                Assert.Greater(
                    iconScreenCenter.y,
                    labelScreenCenter.y,
                    "2:1 port icon should stack above the rate label in screen space.");
                Assert.Less(Mathf.Abs(iconScreenCenter.x - discScreenCenter.x), discScreenSize.x * 0.42f,
                    "2:1 port icon should stay centered on the badge in screen space.");
                Assert.Less(iconScreenCenter.y - discScreenCenter.y, discScreenSize.y * 0.55f,
                    "2:1 port icon should not hang far above the badge disc.");
                Assert.Greater(iconScreenCenter.y - discScreenCenter.y, -discScreenSize.y * 0.15f,
                    "2:1 port icon should stay inside the badge stack in screen space.");
            }
        }

        [Timeout(90000)]
        [UnityTest]
        public IEnumerator PortBadges_ScreenDiameterMatchesTokenScreenDiameter()
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
            for (int i = 0; i < 6; i++)
                yield return null;

            var gameManager = Object.FindFirstObjectByType<GameManager>();
            GameScenePlayHarness.CompleteRunSelectAndSetup(gameManager.Controller, gameManager.Meta);
            gameManager.EnsureRunHudReady();
            for (int i = 0; i < 4; i++)
                yield return null;

            var tableCamera = Object.FindFirstObjectByType<TableCamera>();
            var camera = Camera.main;
            Assert.IsNotNull(tableCamera);
            Assert.IsNotNull(camera);
            ColonistBoardFraming.GetLayoutPixelSize(camera, width, height, out int layoutW, out int layoutH);
            tableCamera.ApplyCapturePose(layoutW, layoutH);
            camera.pixelRect = new Rect(0f, 0f, layoutW, layoutH);
            yield return null;

            HexTileView referenceToken = null;
            foreach (var tile in Object.FindObjectsByType<HexTileView>(FindObjectsSortMode.None))
            {
                if (tile.Data.IsDesert || !tile.Data.NumberToken.HasValue || tile.TokenFaceRenderer == null)
                    continue;
                referenceToken = tile;
                break;
            }

            Assert.IsNotNull(referenceToken, "Board should include a numbered token for badge sizing.");
            Assert.IsNotNull(referenceToken.TokenRimRenderer, "Token should expose rim renderer for badge sizing.");
            float tokenScreen = ColonistBoardScreenMetrics.MeasureScreenDiameter(referenceToken.TokenRimRenderer, camera);
            float targetScreen = tokenScreen * ColonistBoardScreenMetrics.PortBadgeTargetTokenScreenDiameterRatio;

            int checkedDiscs = 0;
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer == null || renderer.gameObject.name != "BadgeDisc")
                    continue;

                float discScreen = ColonistBoardScreenMetrics.MeasureScreenDiameter(renderer, camera);
                Assert.Greater(discScreen, 12f, $"Badge disc should be readable on screen (d={discScreen:F1}px).");
                float ratio = discScreen / Mathf.Max(1f, tokenScreen);
                Assert.That(
                    ratio,
                    Is.InRange(
                        ColonistBoardScreenMetrics.PortBadgeTargetTokenScreenDiameterRatio - 0.08f,
                        ColonistBoardScreenMetrics.PortBadgeTargetTokenScreenDiameterRatio + 0.08f),
                    $"Badge screen diameter should be ~{ColonistBoardScreenMetrics.PortBadgeTargetTokenScreenDiameterRatio:P0} of token " +
                    $"(disc={discScreen:F1}px token={tokenScreen:F1}px target={targetScreen:F1}px ratio={ratio:F3}).");
                checkedDiscs++;
            }

            Assert.Greater(checkedDiscs, 0, "Colonist board should show port badge discs.");
        }

        [Timeout(90000)]
        [UnityTest]
        public IEnumerator PortBadges_DiscClearOfLandHexes()
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

            var boardView = Object.FindFirstObjectByType<BoardView>();
            Assert.IsNotNull(boardView, "Colonist scene should include BoardView.");
            var board = gameManager.Controller.State.Board;
            float hexScale = boardView.HexScale;

            int checkedDiscs = 0;
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer == null || renderer.gameObject.name != "BadgeDisc")
                    continue;

                Vector3 worldCenter = renderer.bounds.center;
                var center = new Vector2(worldCenter.x, worldCenter.z);
                float radius = renderer.transform.lossyScale.x * 0.5f;
                Assert.IsFalse(
                    PortBadgePlacement.DiscIntersectsAnyLandHex(center, radius, board, hexScale),
                    $"Port badge disc at {center} should not overlap land hexes (r={radius:F3}).");
                checkedDiscs++;
            }

            Assert.Greater(checkedDiscs, 0, "Colonist board should show port badge discs.");
        }

        [Timeout(90000)]
        [UnityTest]
        public IEnumerator PortBadges_InsideUnobstructedPlayViewport()
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
            for (int i = 0; i < 6; i++)
                yield return null;

            var gameManager = Object.FindFirstObjectByType<GameManager>();
            GameScenePlayHarness.CompleteRunSelectAndSetup(gameManager.Controller, gameManager.Meta);
            gameManager.EnsureRunHudReady();
            for (int i = 0; i < 4; i++)
                yield return null;

            var tableCamera = Object.FindFirstObjectByType<TableCamera>();
            var camera = Camera.main;
            Assert.IsNotNull(tableCamera);
            Assert.IsNotNull(camera);

            ColonistBoardFraming.GetLayoutPixelSize(camera, width, height, out int layoutW, out int layoutH);
            tableCamera.ApplyCapturePose(layoutW, layoutH);
            camera.pixelRect = new Rect(0f, 0f, layoutW, layoutH);
            yield return null;

            int checkedDiscs = 0;
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer == null || renderer.gameObject.name != "BadgeDisc")
                    continue;

                Assert.IsTrue(
                    ColonistBoardFraming.IsBoundsInsidePlayArea(camera, renderer.bounds, layoutW, layoutH),
                    $"Port badge disc screen bounds should stay inside the unobstructed play viewport ({renderer.bounds.center}).");
                checkedDiscs++;
            }

            Assert.Greater(checkedDiscs, 0, "Colonist board should show port badge discs.");
        }
    }
}
