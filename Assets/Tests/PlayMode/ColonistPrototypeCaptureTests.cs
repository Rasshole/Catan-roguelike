using System.Collections;
using System.IO;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CatanRoguelike.Tests.PlayMode
{
    public class ColonistPrototypeCaptureTests
    {
        private const string ColonistScenePath = "Assets/_Project/Scenes/ColonistStylePrototype.unity";
        private const string ColonistSceneName = "ColonistStylePrototype";
        private const int HudReadyMaxFrames = 120;

        // Matches ColonistKenneyHudArt wood bar tint (hand row panel).
        private static readonly Color HandBarReference = new(0.84f, 0.72f, 0.54f);

        // Matches ColonistKenneyHudArt.ApplyPanelImage parchment (right rail reaches the bottom band).
        private static readonly Color ParchmentPanelReference = new(0.94f, 0.88f, 0.74f);

        [Timeout(90000)]
        [UnityTest]
        public IEnumerator ColonistPrototype_CaptureWithHud_WritesPng()
        {
            if (!File.Exists(ColonistScenePath))
            {
                Assert.Inconclusive(
                    $"Colonist prototype not found at {ColonistScenePath}. " +
                    "Run Catan Roguelike → Build Colonist Style Prototype Scene.");
            }

            Screen.SetResolution(GameSceneCapture.CaptureWidth, GameSceneCapture.CaptureHeight, false);
            yield return null;

            var loadOp = SceneManager.LoadSceneAsync(ColonistSceneName, LoadSceneMode.Single);
            Assert.IsNotNull(loadOp, $"Failed to load '{ColonistSceneName}'. Add it to Build Settings.");
            yield return loadOp;

            for (int i = 0; i < 4; i++)
                yield return null;

            var gameManager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            Assert.IsNotNull(gameManager, "GameManager should exist after scene load.");
            Assert.IsNotNull(gameManager.Controller, "Controller should be initialized after Start().");
            Assert.IsNotNull(gameManager.Meta, "Meta should be loaded after Start().");

            if (gameManager.Controller.State.Phase == GamePhase.RunSelectMap)
                GameScenePlayHarness.CompleteRunSelectAndSetup(
                    gameManager.Controller,
                    gameManager.Meta,
                    fishermenOfCatan: true);

            GameScenePlayHarness.AdvanceToDayPlayerActions(gameManager.Controller);
            Assert.AreEqual(GamePhase.DayPlayerActions, gameManager.Controller.State.Phase);

            ColonistCaptureCardSeeding.ForceDemoHandForCapture(gameManager.Controller.State);

            gameManager.EnsureRunHudReady();

            Assert.IsFalse(gameManager.Controller.State.IsSetupPhase,
                "Harness should finish setup before capture.");

            RunHudController hud = null;
            for (int i = 0; i < HudReadyMaxFrames; i++)
            {
                hud = gameManager.GetComponent<RunHudController>();
                if (hud != null && hud.IsHudCaptureReady())
                    break;
                gameManager.EnsureRunHudReady();
                yield return null;
            }

            Assert.IsNotNull(hud, "RunHudController should be present on the colonist prototype.");
            int graphicCount = hud != null ? hud.CountHudGraphics() : 0;
            Debug.Log($"Colonist capture HUD graphics count: {graphicCount}");
            Assert.IsTrue(hud.IsHudCaptureReady(),
                $"HUD canvas should be built with graphics before capture (found {graphicCount}).");

            hud.Refresh(gameManager.Controller.State);
            Canvas.ForceUpdateCanvases();
            if (hud.HudCanvas != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(hud.HudCanvas.GetComponent<RectTransform>());

            var actionBar = hud.GetColonistActionBarLayoutForTests();
            actionBar.LogToConsole();
            AssertColonistActionBarVisible(actionBar);

            for (int i = 0; i < 4; i++)
                yield return null;

            var boardViewForDiag = UnityEngine.Object.FindFirstObjectByType<BoardView>();
            ColonistRobberCaptureDiagnostics.LogRobberHexSceneRenderers(boardViewForDiag);

            Texture2D boardOnly = GameSceneCapture.CaptureBoardOnlyTexture(
                GameSceneCapture.CaptureWidth,
                GameSceneCapture.CaptureHeight);
            Texture2D withHud = GameSceneCapture.CaptureMainCameraWithHudComposite(hud);
            GameSceneCapture.LogCaptureTextureFingerprint(boardOnly, "test-board-before-write");
            GameSceneCapture.LogCaptureTextureFingerprint(withHud, "test-hud-before-write");
            GameSceneCapture.WriteColonistDebugCapturePngs(boardOnly, withHud);

            try
            {
                Assert.IsNotNull(boardOnly, "Board-only capture texture should be created.");
                Assert.IsNotNull(withHud, "HUD-composited capture texture should be created.");
                Assert.AreNotSame(boardOnly, withHud, "Captures should be distinct texture instances.");

                var mainCamera = Camera.main;
                var tableCamera = UnityEngine.Object.FindFirstObjectByType<TableCamera>();
                var boardView = UnityEngine.Object.FindFirstObjectByType<BoardView>();
                Assert.IsNotNull(mainCamera, "Main camera should exist for board capture assertions.");
                Assert.IsNotNull(tableCamera, "TableCamera should exist for colonist capture assertions.");
                Assert.IsNotNull(boardView, "BoardView should exist for colonist capture assertions.");

                var playCenter = ColonistPlayAreaLayout.GetCenterTexturePixels(
                    GameSceneCapture.CaptureWidth,
                    GameSceneCapture.CaptureHeight);
                var boardPixel = ColonistCaptureProjection.WorldToTexturePixel(
                    boardView.GetCaptureReferenceWorldPoint(),
                    mainCamera,
                    tableCamera,
                    boardOnly.width,
                    boardOnly.height);
                Debug.Log(
                    $"Colonist capture framing: playAreaCenterTexture=({playCenter.x},{playCenter.y}) " +
                    $"boardReferenceTexture=({boardPixel.x},{boardPixel.y})");

                ColonistBoardFraming.AssertSeaFishGroundCornersInsidePlayArea(
                    boardView,
                    mainCamera,
                    GameSceneCapture.CaptureWidth,
                    GameSceneCapture.CaptureHeight);

                ColonistBoardCaptureAssertions.AssertColonistLandHexViewCount(19);
                ColonistBoardCaptureAssertions.AssertRenderedPortBadgeDiscCount(9);

                ColonistBoardCaptureAssertions.AssertColonistBoardCaptureShowsHexTiles(
                    boardOnly,
                    mainCamera,
                    tableCamera,
                    boardView,
                    GameSceneCapture.CaptureWidth,
                    GameSceneCapture.CaptureHeight);
                ColonistBoardCaptureAssertions.AssertLandHexCenterPixelsNotSandColor(
                    boardOnly,
                    mainCamera,
                    tableCamera);
                ColonistBoardCaptureAssertions.AssertNumberTokenLabelsUprightForCamera(mainCamera);
                ColonistBoardCaptureAssertions.AssertNumberTokenLabelsMatchBoardDiceValues(mainCamera);

                var tokenTile = UnityEngine.Object.FindFirstObjectByType<HexTileView>();
                foreach (var tile in UnityEngine.Object.FindObjectsByType<HexTileView>(FindObjectsSortMode.None))
                {
                    if (!tile.Data.IsDesert && tile.Data.NumberToken.HasValue
                        && !BoardState.IsSeaFishGround(tile.Data))
                    {
                        tokenTile = tile;
                        break;
                    }
                }

                if (tokenTile != null && tokenTile.Data.NumberToken.HasValue)
                    ColonistBoardCaptureAssertions.AssertTokenRimRingVisibleOnBoardCapture(
                        boardOnly, mainCamera, tableCamera, tokenTile);

                ColonistBoardCaptureAssertions.AssertRobberPawnReadableOnBoardCapture(
                    mainCamera,
                    tableCamera,
                    GameSceneCapture.CaptureWidth,
                    GameSceneCapture.CaptureHeight);
                ColonistBoardCaptureAssertions.AssertPortRateLabelVisibleOnBoardCapture(
                    boardOnly,
                    mainCamera,
                    tableCamera,
                    GameSceneCapture.CaptureWidth,
                    GameSceneCapture.CaptureHeight);
                ColonistBoardCaptureAssertions.AssertCoastalFishShoalsVisibleOnBoardCapture(
                    boardOnly,
                    mainCamera,
                    tableCamera,
                    gameManager.Controller.State.Board,
                    boardView.HexScale,
                    GameSceneCapture.CaptureWidth,
                    GameSceneCapture.CaptureHeight);
                ColonistBoardCaptureAssertions.AssertFishermenHarnessBoardAndHud(gameManager.Controller.State);

                AssertTextureHasVisibleContent(withHud);
                AssertBottomBandDiffersBoardOnlyVsHud(boardOnly, withHud);
                AssertBottomBandShowsHudBarColor(withHud, boardOnly);
            }
            finally
            {
                if (boardOnly != null)
                    UnityEngine.Object.Destroy(boardOnly);
                if (withHud != null)
                    UnityEngine.Object.Destroy(withHud);
            }
        }

        private static void AssertTextureHasVisibleContent(Texture2D texture)
        {
            Assert.IsNotNull(texture, "Capture texture should be created.");
            Assert.AreEqual(GameSceneCapture.CaptureWidth, texture.width);
            Assert.AreEqual(GameSceneCapture.CaptureHeight, texture.height);

            float luminance = 0f;
            int samples = 0;
            int w = texture.width;
            int h = texture.height;
            int[] xs = { w / 4, w / 2, (3 * w) / 4 };
            int[] ys = { h / 4, h / 2, (3 * h) / 4 };
            foreach (int x in xs)
            {
                foreach (int y in ys)
                {
                    var c = texture.GetPixel(x, y);
                    luminance += c.r + c.g + c.b;
                    samples++;
                }
            }

            Assert.Greater(luminance, 0.05f * samples,
                "Capture should contain non-black pixels (HUD + board).");
        }

        private static void AssertBottomBandDiffersBoardOnlyVsHud(Texture2D boardOnly, Texture2D withHud)
        {
            Assert.AreEqual(boardOnly.width, withHud.width);
            Assert.AreEqual(boardOnly.height, withHud.height);

            int w = withHud.width;
            int h = withHud.height;
            int bandTopY = Mathf.Max(1, (int)(h * 0.12f));
            int differing = 0;
            int samples = 0;

            for (int x = 0; x < w; x += 24)
            {
                for (int y = 0; y < bandTopY; y += 6)
                {
                    var boardPixel = boardOnly.GetPixel(x, y);
                    var hudPixel = withHud.GetPixel(x, y);
                    if (ColorDistance(boardPixel, hudPixel) > 0.04f)
                        differing++;
                    samples++;
                }
            }

            Assert.Greater(differing, samples / 8,
                "Bottom band of HUD capture must differ from board-only capture (hand/action bar).");
        }

        private static void AssertBottomBandShowsHudBarColor(Texture2D withHud, Texture2D boardOnly)
        {
            int w = withHud.width;
            int h = withHud.height;
            int bandTopY = Mathf.Max(1, (int)(h * 0.12f));
            int differingFromBoard = 0;
            int matchingHudBar = 0;
            int samples = 0;

            for (int x = w / 8; x < w; x += w / 8)
            {
                for (int y = 0; y < bandTopY; y += 12)
                {
                    var hudPixel = withHud.GetPixel(x, y);
                    var boardPixel = boardOnly.GetPixel(x, y);

                    if (ColorDistance(hudPixel, boardPixel) > 0.04f)
                        differingFromBoard++;

                    if (ColorDistance(hudPixel, HandBarReference) < 0.35f
                        || ColorDistance(hudPixel, ParchmentPanelReference) < 0.25f)
                        matchingHudBar++;
                    samples++;
                }
            }

            Assert.Greater(differingFromBoard, samples / 8,
                "Bottom band (texture y near 0) should differ between HUD and board-only captures.");
            // Kenney HUD: the hand bar spans only the left ~quarter (action bar sits above it), so
            // require a smaller share of the full-width bottom band than the old edge-to-edge bar.
            Assert.Greater(matchingHudBar, samples / 8,
                $"Bottom band should include warm HUD chrome (wood bar / parchment panel) color from the colonist HUD (matched={matchingHudBar}/{samples}).");
        }

        private static float ColorDistance(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
        }

        private static void AssertColonistActionBarVisible(RunHudController.ColonistActionBarLayoutProbe probe)
        {
            Assert.IsTrue(probe.EndTurnActiveInHierarchy, "End Turn should be active in hierarchy during DayPlayerActions.");
            Assert.Greater(probe.EndTurnSize.x, 8f, "End Turn button width should be non-zero.");
            Assert.Greater(probe.EndTurnSize.y, 8f, "End Turn button height should be non-zero.");

            Assert.IsTrue(probe.RoadActiveInHierarchy, "Road build button should be active.");
            Assert.Greater(probe.RoadSize.x, 8f, "Road button width should be non-zero.");
            Assert.IsTrue(probe.SettlementActiveInHierarchy, "Settlement build button should be active.");
            Assert.Greater(probe.SettlementSize.x, 8f, "Settlement button width should be non-zero.");
            Assert.IsTrue(probe.CityActiveInHierarchy, "City build button should be active.");
            Assert.Greater(probe.CitySize.x, 8f, "City button width should be non-zero.");
            Assert.IsTrue(probe.DevActiveInHierarchy, "Dev button should be active.");
            Assert.Greater(probe.DevSize.x, 8f, "Dev button width should be non-zero.");
        }
    }
}
