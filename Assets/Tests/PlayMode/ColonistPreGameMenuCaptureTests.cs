using System.Collections;
using System.IO;
using System.Linq;
using CatanRoguelike.Core.Leaders;
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
    /// <summary>
    /// v132: the painted run-setup menus (map → leader → draft), the level-up perk cards and the run-end card
    /// render on the HUD canvas in the Colonist scene and drive the real controller.
    /// Set COLONIST_MENU_SHOT_DIR to a folder to write review captures (menu-map/unlocks/leader/draft,
    /// levelup, runend .png).
    /// </summary>
    public class ColonistPreGameMenuCaptureTests
    {
        private const string ColonistScenePath = "Assets/_Project/Scenes/ColonistStylePrototype.unity";
        private const string ColonistSceneName = "ColonistStylePrototype";

        private static IEnumerator LoadScene()
        {
            if (!File.Exists(ColonistScenePath))
                Assert.Inconclusive("Colonist prototype scene missing.");
            Screen.SetResolution(GameSceneCapture.CaptureWidth, GameSceneCapture.CaptureHeight, false);
            yield return null;
            var loadOp = SceneManager.LoadSceneAsync(ColonistSceneName, LoadSceneMode.Single);
            yield return loadOp;
            for (int i = 0; i < 4; i++)
                yield return null;
        }

        private static IEnumerator Settle(GameManager gameManager, System.Action<RunHudController> done)
        {
            RunHudController hud = null;
            for (int i = 0; i < 30; i++)
            {
                gameManager.EnsureRunHudReady();
                hud = gameManager.GetComponent<RunHudController>();
                if (hud != null && hud.HudCanvas != null && hud.HudCanvas.isActiveAndEnabled)
                    break;
                yield return null;
            }

            Assert.IsNotNull(hud);
            hud.Refresh(gameManager.Controller.State);
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < 4; i++)
                yield return null;
            done(hud);
        }

        private static void MaybeWrite(RunHudController hud, string name)
        {
            string dir = System.Environment.GetEnvironmentVariable("COLONIST_MENU_SHOT_DIR");
            if (string.IsNullOrEmpty(dir))
                return;
            var tex = GameSceneCapture.CaptureMainCameraWithHudComposite(hud);
            Assert.IsNotNull(tex);
            string path = Path.Combine(dir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log($"v132 capture written: {path}");
        }

        private static Button FindButton(RunHudController hud, string prefix)
        {
            var root = hud.PreGameMenuRootForTests;
            Assert.IsNotNull(root, "pre-game menu root");
            return root.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name.StartsWith(prefix)
                && b.interactable);
        }

        [Timeout(120000)]
        [UnityTest]
        public IEnumerator PaintedRunSetupMenus_DriveMapLeaderAndDraft()
        {
            yield return LoadScene();
            var gameManager = Object.FindFirstObjectByType<GameManager>();
            var controller = gameManager?.Controller;
            Assert.IsNotNull(controller);
            Assert.AreEqual(GamePhase.RunSelectMap, controller.State.Phase);

            RunHudController hud = null;
            yield return Settle(gameManager, h => hud = h);
            Assert.IsTrue(hud.IsPreGameMenuVisibleForTests, "painted map menu visible");
            Assert.IsTrue(RunHudController.PaintedPreGameMenusVisible, "IMGUI menu should defer to painted menu");
            Assert.IsTrue(hud.HudCanvas.gameObject.activeSelf, "HUD canvas hosts the pre-game menu");
            foreach (Transform child in hud.HudCanvas.transform)
            {
                if (child == hud.PreGameMenuRootForTests)
                    continue;
                var group = child.GetComponent<CanvasGroup>();
                if (child.gameObject.activeInHierarchy && group != null)
                    Assert.AreEqual(0f, group.alpha, child.name + " should be hidden during run setup");
            }

            MaybeWrite(hud, "menu-map");

            if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("COLONIST_MENU_SHOT_DIR")))
            {
                var unlocks = FindButton(hud, "Footer_1");
                if (unlocks != null && gameManager.Meta != null)
                {
                    unlocks.onClick.Invoke();
                    yield return Settle(gameManager, h => hud = h);
                    MaybeWrite(hud, "menu-unlocks");
                    var close = hud.PreGameMenuRootForTests.GetComponentsInChildren<Button>(true)
                        .First(b => b.GetComponentInChildren<Text>()?.text == "Close");
                    close.onClick.Invoke();
                    yield return Settle(gameManager, h => hud = h);
                }
            }

            var map = FindButton(hud, "Map_");
            Assert.IsNotNull(map, "an available map card");
            map.onClick.Invoke();
            Assert.AreEqual(GamePhase.RunSelectLeader, controller.State.Phase);
            yield return Settle(gameManager, h => hud = h);
            Assert.IsTrue(hud.IsPreGameMenuVisibleForTests);
            MaybeWrite(hud, "menu-leader");

            var leader = FindButton(hud, "Leader_");
            Assert.IsNotNull(leader, "an available leader card");
            leader.onClick.Invoke();
            Assert.AreEqual(GamePhase.RunSelectDraft, controller.State.Phase);
            yield return Settle(gameManager, h => hud = h);

            int picks = gameManager.Meta != null ? gameManager.Meta.GetDraftPickCount() : 2;
            for (int i = 0; i < picks; i++)
            {
                var card = hud.PreGameMenuRootForTests.GetComponentsInChildren<Button>(true)
                    .Where(b => b.name.StartsWith("Draft_")).ElementAt(i);
                card.onClick.Invoke();
                yield return null;
            }

            yield return Settle(gameManager, h => hud = h);
            Assert.AreEqual(picks, controller.State.DraftedUniques.Count);
            MaybeWrite(hud, "menu-draft");

            var start = hud.PreGameMenuRootForTests.GetComponentsInChildren<Button>(true)
                .First(b => b.GetComponentInChildren<Text>()?.text == "Start run");
            start.onClick.Invoke();
            Assert.IsFalse(PlaceholderHudLayout.IsPreGamePhase(controller.State.Phase), "run setup confirmed");
            yield return Settle(gameManager, h => hud = h);
            Assert.IsFalse(hud.IsPreGameMenuVisibleForTests, "painted menu hides once the run starts");
            Assert.IsFalse(RunHudController.PaintedPreGameMenusVisible);
            foreach (Transform child in hud.HudCanvas.transform)
            {
                var group = child.GetComponent<CanvasGroup>();
                if (group != null && child != hud.PreGameMenuRootForTests && child.name != "PreGameMenu")
                    Assert.AreEqual(1f, group.alpha, child.name + " should be visible again in the run");
            }
        }

        [Timeout(120000)]
        [UnityTest]
        public IEnumerator LevelUpAndRunEnd_ShowPaintedCards()
        {
            yield return LoadScene();
            var gameManager = Object.FindFirstObjectByType<GameManager>();
            var controller = gameManager.Controller;
            GameScenePlayHarness.CompleteRunSelect(controller, gameManager.Meta, fishermenOfCatan: true);
            GameScenePlayHarness.CompleteSetup(controller);
            ColonistCapturePieceSeeding.ForceDemoBoardPiecesForCapture(controller.State);
            Object.FindFirstObjectByType<BoardView>()?.Refresh(controller.State);

            var state = controller.State;
            var pool = LeaderLibrary.Get(state.Leader).PerkPool;
            state.PendingLevelUpChoices.Clear();
            foreach (var perk in pool.Take(3))
                state.PendingLevelUpChoices.Add(perk);
            state.Phase = GamePhase.LevelUpChoice;

            RunHudController hud = null;
            yield return Settle(gameManager, h => hud = h);
            Assert.IsTrue(hud.IsLevelUpOverlayVisibleForTests, "level-up perk cards visible");
            MaybeWrite(hud, "levelup");

            state.PendingLevelUpChoices.Clear();
            state.Phase = GamePhase.DayPlayerActions;
            state.PlayerVictoryPoints = 10;
            state.Winner = PlayerId.Human;
            yield return Settle(gameManager, h => hud = h);
            Assert.IsFalse(hud.IsLevelUpOverlayVisibleForTests);
            Assert.IsTrue(hud.IsRunEndOverlayVisibleForTests, "run-end card visible");
            MaybeWrite(hud, "runend");
        }

        [Timeout(120000)]
        [UnityTest]
        public IEnumerator TradeDialog_ShowsPaintedResourceCards()
        {
            yield return LoadScene();
            var gameManager = Object.FindFirstObjectByType<GameManager>();
            var controller = gameManager.Controller;
            GameScenePlayHarness.CompleteRunSelect(controller, gameManager.Meta, fishermenOfCatan: true);
            GameScenePlayHarness.CompleteSetup(controller);
            GameScenePlayHarness.AdvanceToDayPlayerActions(controller);
            ColonistCapturePieceSeeding.ForceDemoBoardPiecesForCapture(controller.State);
            Object.FindFirstObjectByType<BoardView>()?.Refresh(controller.State);

            RunHudController hud = null;
            yield return Settle(gameManager, h => hud = h);
            hud.OpenTradeDialogForTests();
            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return null;
            Assert.IsTrue(hud.IsTradeDialogVisibleForTests, "trade dialog visible");
            MaybeWrite(hud, "trade");
        }
    }
}
