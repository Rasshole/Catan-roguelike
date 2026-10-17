using System.Collections;
using System.IO;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CatanRoguelike.Tests.PlayMode
{
    public class ColonistRoguelikeCardHandPlayModeTests
    {
        private const string ColonistScenePath = "Assets/_Project/Scenes/ColonistStylePrototype.unity";
        private const string ColonistSceneName = "ColonistStylePrototype";

        [Timeout(90000)]
        [UnityTest]
        public IEnumerator RoguelikeHand_FanDoesNotOverlapActionBar()
        {
            if (!File.Exists(ColonistScenePath))
            {
                Assert.Inconclusive($"Colonist prototype not found at {ColonistScenePath}.");
                yield break;
            }

            yield return SceneManager.LoadSceneAsync(ColonistSceneName, LoadSceneMode.Single);
            for (int i = 0; i < 4; i++)
                yield return null;

            var gameManager = Object.FindFirstObjectByType<GameManager>();
            GameScenePlayHarness.CompleteRunSelectAndSetup(gameManager.Controller, gameManager.Meta);
            GameScenePlayHarness.AdvanceToDayPlayerActions(gameManager.Controller);
            ColonistCaptureCardSeeding.ForceDemoHandForCapture(gameManager.Controller.State);
            gameManager.EnsureRunHudReady();
            for (int i = 0; i < 4; i++)
                yield return null;

            var hud = gameManager.GetComponent<RunHudController>();
            Assert.IsNotNull(hud);
            hud.Refresh(gameManager.Controller.State);
            Canvas.ForceUpdateCanvases();

            var probe = hud.GetRoguelikeCardHandLayoutForTests();
            Assert.GreaterOrEqual(probe.CardCount, 5, "Capture demo hand should show five cards.");
            Assert.Greater(
                probe.MinCardBottomY,
                probe.ActionBarTopScreen.y + 2f,
                "Fanned cards should sit above the build/action bar.");

            int width = GameSceneCapture.CaptureWidth;
            int height = GameSceneCapture.CaptureHeight;
            Screen.SetResolution(width, height, false);
            var tableCamera = Object.FindFirstObjectByType<TableCamera>();
            var camera = Camera.main;
            Assert.IsNotNull(tableCamera);
            Assert.IsNotNull(camera);
            ColonistBoardFraming.GetLayoutPixelSize(camera, width, height, out int layoutW, out int layoutH);
            tableCamera.ApplyCapturePose(layoutW, layoutH);
            camera.pixelRect = new Rect(0f, 0f, layoutW, layoutH);
            Canvas.ForceUpdateCanvases();
            yield return null;

            var handView = hud.GetRoguelikeCardHandViewForTests();
            Assert.IsNotNull(handView);
            Assert.IsTrue(
                handView.TryGetRestingFanScreenRect(out float fanMinX, out float fanMaxX, out float fanMinY, out float fanMaxY),
                "Resting fan should have a screen bounds.");

            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer == null || renderer.gameObject.name != "BadgeDisc")
                    continue;
                Assert.IsFalse(
                    ScreenBoundsOverlap(renderer.bounds, camera, fanMinX, fanMaxX, fanMinY, fanMaxY, padding: 4f),
                    $"Resting card fan should not cover port badge at {renderer.bounds.center}.");
            }

            float tokenCutoffY = fanMaxY + 6f;
            foreach (var chip in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (chip == null || chip.gameObject.name != "NumberTokenChip")
                    continue;
                var center = camera.WorldToScreenPoint(chip.bounds.center);
                if (center.y > tokenCutoffY)
                    continue;
                Assert.IsFalse(
                    ScreenBoundsOverlap(chip.bounds, camera, fanMinX, fanMaxX, fanMinY, fanMaxY, padding: 4f),
                    $"Resting card fan should not cover bottom-row token chip at {chip.bounds.center}.");
            }

            var handRoot = GameObject.Find("RoguelikeCardHand");
            Assert.IsNotNull(handRoot, "Roguelike card hand panel should exist.");
            var resourceHand = GameObject.Find("ResourceHand");
            Assert.IsNotNull(resourceHand);
            Assert.IsFalse(
                RectTransformsOverlap(handRoot.GetComponent<RectTransform>(), resourceHand.GetComponent<RectTransform>()),
                "Roguelike hand should not overlap the resource hand strip.");

            // Hover the middle card: it must lift, scale and come to the front.
            var middle = GameObject.Find("RoguelikeCard_2");
            Assert.IsNotNull(middle, "Middle roguelike card should exist.");
            float restY = middle.GetComponent<RectTransform>().anchoredPosition.y;
            ExecuteEvents.Execute(middle, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            Canvas.ForceUpdateCanvases();
            yield return null;
            var middleRect = middle.GetComponent<RectTransform>();
            Assert.Greater(middleRect.anchoredPosition.y, restY + 10f, "Hovered card should lift.");
            float restScale = ColonistRoguelikeCardHandLayout.RestScale;
            Assert.Greater(
                middleRect.localScale.x,
                restScale * 1.05f,
                "Hovered card should scale up from the resting fan size.");
            Assert.AreEqual(middleRect.parent.childCount - 1, middleRect.GetSiblingIndex(), "Hovered card should draw on top.");

            // Optional review capture of the hovered fan (COLONIST_CARD_HOVER_SHOT=/path.png).
            string hoverShot = System.Environment.GetEnvironmentVariable("COLONIST_CARD_HOVER_SHOT");
            if (!string.IsNullOrEmpty(hoverShot))
            {
                var tex = GameSceneCapture.CaptureMainCameraWithHudComposite(
                    hud, GameSceneCapture.CaptureWidth, GameSceneCapture.CaptureHeight);
                File.WriteAllBytes(hoverShot, tex.EncodeToPNG());
            }
        }

        private static bool ScreenBoundsOverlap(
            Bounds worldBounds,
            Camera camera,
            float fanMinX,
            float fanMaxX,
            float fanMinY,
            float fanMaxY,
            float padding)
        {
            var center = worldBounds.center;
            var extents = worldBounds.extents;
            float bMinX = float.MaxValue;
            float bMaxX = float.MinValue;
            float bMinY = float.MaxValue;
            float bMaxY = float.MinValue;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        var p = camera.WorldToScreenPoint(center + new Vector3(
                            extents.x * sx,
                            extents.y * sy,
                            extents.z * sz));
                        bMinX = Mathf.Min(bMinX, p.x);
                        bMaxX = Mathf.Max(bMaxX, p.x);
                        bMinY = Mathf.Min(bMinY, p.y);
                        bMaxY = Mathf.Max(bMaxY, p.y);
                    }
                }
              }

            fanMinX -= padding;
            fanMaxX += padding;
            fanMinY -= padding;
            fanMaxY += padding;
            bool overlapX = fanMinX < bMaxX && fanMaxX > bMinX;
            bool overlapY = fanMinY < bMaxY && fanMaxY > bMinY;
            return overlapX && overlapY;
        }

        private static bool RectTransformsOverlap(RectTransform a, RectTransform b)
        {
            if (a == null || b == null)
                return false;

            var ac = new Vector3[4];
            var bc = new Vector3[4];
            a.GetWorldCorners(ac);
            b.GetWorldCorners(bc);
            float aMinX = ac[0].x;
            float aMaxX = ac[2].x;
            float aMinY = ac[0].y;
            float aMaxY = ac[2].y;
            float bMinX = bc[0].x;
            float bMaxX = bc[2].x;
            float bMinY = bc[0].y;
            float bMaxY = bc[2].y;
            bool overlapX = aMinX < bMaxX && aMaxX > bMinX;
            bool overlapY = aMinY < bMaxY && aMaxY > bMinY;
            return overlapX && overlapY;
        }
    }
}
