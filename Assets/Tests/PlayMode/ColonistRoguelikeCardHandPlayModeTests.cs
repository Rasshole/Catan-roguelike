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

            var handRoot = GameObject.Find("RoguelikeCardHand");
            Assert.IsNotNull(handRoot, "Roguelike card hand panel should exist.");
            var resourceHand = GameObject.Find("ResourceHand");
            Assert.IsNotNull(resourceHand);
            Assert.IsFalse(
                RectTransformsOverlap(handRoot.GetComponent<RectTransform>(), resourceHand.GetComponent<RectTransform>()),
                "Roguelike hand should not overlap the resource hand strip.");
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
