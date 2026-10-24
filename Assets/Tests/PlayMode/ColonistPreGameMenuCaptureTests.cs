using System.Collections;
using System.IO;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Civilizations;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Trade;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
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
            // v134: civs and leaders are merged — the civ pick (with leader portraits) is step 2.
            Assert.AreEqual(GamePhase.RunSelectCivilization, controller.State.Phase);
            yield return Settle(gameManager, h => hud = h);
            Assert.IsTrue(hud.IsCivilizationPickVisibleForTests);
            MaybeWrite(hud, "menu-civ");

            var civ = hud.CivilizationPickRootForTests.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(b => b.name.StartsWith("Civ_"));
            Assert.IsNotNull(civ, "a civilization card");
            Assert.IsNotNull(civ.transform.Find("Portrait"), "civ card shows the leader portrait");
            Assert.IsNotNull(civ.transform.Find("LedBy"), "civ card names its leader");
            civ.onClick.Invoke();
            Assert.AreEqual(GamePhase.RunSelectDraft, controller.State.Phase);
            Assert.AreNotEqual(CatanRoguelike.Core.Civilizations.CivilizationId.None, controller.State.Civilization);
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

            // v138: icon + number trade card. Bank/port: 4 wheat -> 1 stone (or the harbour rate).
            var state = controller.State;
            // Harbours for the capture: a generic 3:1 and, when the map has one, the wheat 2:1.
            foreach (var port in state.Ports.Where(p => p.IsGeneric).Take(1)
                         .Concat(state.Ports.Where(p => p.SpecificResource == ResourceType.Wheat).Take(1)))
                state.Board.VertexBuildings[port.Vertex] = (BuildingType.Settlement, PlayerId.Human);
            state.PlayerInventory = new ResourceBundle { Wood = 2, Brick = 1, Wheat = 6, Sheep = 3, Stone = 0 };
            int rate = BankTradeCalculator.GetGiveRateForResource(state, PlayerId.Human, ResourceType.Wheat);
            hud.SetTradeTermsForTests(ResourceBundle.Zero, ResourceBundle.Zero);
            var dialog = hud.TradeDialogRootForTests;
            // Click the wheat slot `rate` times (adds), right-click once and add again (removes/adds).
            var wheat = dialog.GetComponentsInChildren<Button>(true).First(b => b.name == "GiveMore_Wheat");
            for (int i = 0; i < rate; i++)
            {
                wheat = dialog.GetComponentsInChildren<Button>(true).First(b => b.name == "GiveMore_Wheat");
                wheat.onClick.Invoke();
            }

            dialog.GetComponentsInChildren<HudRightClickRelay>(true).First(b => b.name == "GiveMore_Wheat").RightClickForTests();
            dialog.GetComponentsInChildren<Button>(true).First(b => b.name == "GiveMore_Wheat").onClick.Invoke();
            dialog.GetComponentsInChildren<Button>(true).First(b => b.name == "GetMore_Stone").onClick.Invoke();
            yield return null;
            var bank = dialog.GetComponentsInChildren<Button>(true).First(b => b.name == "BankTrade");
            Assert.IsTrue(bank.interactable, "bank trade enabled at " + rate + ":1");
            Assert.Greater(dialog.GetComponentsInChildren<Button>(true).First(b => b.name == "GetMore_Stone").transform.position.y,
                dialog.GetComponentsInChildren<Button>(true).First(b => b.name == "GiveMore_Wheat").transform.position.y,
                "v139: the get row is above the give row");
            Assert.IsTrue(dialog.GetComponentsInChildren<Button>(true).Any(b => b.name == "GiveMore_Any"), "wildcard slot present");
            Assert.IsTrue(dialog.GetComponentsInChildren<Image>(true).Any(i => i.name == "Rate_Wheat"), "rate badges");
            Assert.IsFalse(dialog.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains("Wheat") || t.text.Contains("Stone")),
                "no resource words on the trade card");
            Canvas.ForceUpdateCanvases();
            yield return null;
            MaybeWrite(hud, "trade-bank");
            bank.onClick.Invoke();
            yield return null;
            Assert.AreEqual(1, controller.State.PlayerInventory.Stone, "bank trade executed");
            Assert.AreEqual(6 - rate, controller.State.PlayerInventory.Wheat);

            // Offer the opponent 2 sheep for 1 brick, then show its answer.
            controller.State.AiInventory = new ResourceBundle { Wood = 2, Brick = 2, Stone = 1 };
            hud.SetTradeTermsForTests(new ResourceBundle { Sheep = 2 }, new ResourceBundle { Brick = 1 });
            yield return null;
            dialog.GetComponentsInChildren<Button>(true).First(b => b.name == "OfferAi").onClick.Invoke();
            yield return null;
            yield return null;
            Assert.IsTrue(controller.State.PendingPlayerTrade.HasValue, "offer sent");
            Assert.IsTrue(dialog.GetComponentsInChildren<Image>(true).Any(i => i.name == "OpponentDisc"), "opponent answer row");
            Canvas.ForceUpdateCanvases();
            yield return null;
            var opponentFace = dialog.GetComponentsInChildren<Image>(true).First(i => i.name == "OpponentDisc")
                .transform.Find("Face");
            Assert.IsNotNull(opponentFace, "v139: the opponent shows as a leader portrait");
            MaybeWrite(hud, "trade-offer");

            // v139 wildcard: 1 sheep for "1 of anything" — on Trade you pick from what the opponent can spare.
            hud.SetTradeTermsForTests(new ResourceBundle { Sheep = 1 }, ResourceBundle.Zero, 0, 1);
            yield return null;
            dialog.GetComponentsInChildren<Button>(true).First(b => b.name == "OfferAi").onClick.Invoke();
            yield return null;
            Assert.AreEqual(1, controller.State.PendingPlayerTrade.Value.ReceiveAny);
            Assert.IsTrue(controller.State.PendingAiTradeResponses[0].Accept, "the AI can spare a card");
            dialog.GetComponentsInChildren<Button>(true).First(b => b.name == "AcceptAi").onClick.Invoke();
            yield return null;
            Assert.IsTrue(hud.TradeWildPickerOpenForTests);
            Assert.IsTrue(controller.State.PendingPlayerTrade.HasValue, "nothing traded before the pick");
            var stoneBefore = controller.State.PlayerInventory.Stone;
            Assert.IsFalse(dialog.GetComponentsInChildren<Button>(true).First(b => b.name == "WildPickMore_Wheat").interactable,
                "the opponent holds no wheat");
            dialog.GetComponentsInChildren<Button>(true).First(b => b.name == "WildPickMore_Stone").onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            MaybeWrite(hud, "trade-wild-picker-sp");
            dialog.GetComponentsInChildren<Button>(true).First(b => b.name == "WildPickConfirm").onClick.Invoke();
            yield return null;
            Assert.IsFalse(controller.State.PendingPlayerTrade.HasValue, "traded");
            Assert.AreEqual(stoneBefore + 1, controller.State.PlayerInventory.Stone);
            hud.CloseTradeDialogForTests();
            controller.State.PendingPlayerTrade = null;
            controller.State.PendingAiTradeResponses = System.Array.Empty<AiTradeResponse>();

            // Incoming AI offer seen from your side: red = you give, green = you get.
            controller.State.IncomingAiTrade = new AiIncomingTradeOffer(
                new ResourceBundle { Stone = 1 }, new ResourceBundle { Wood = 1, Sheep = 1 });
            yield return Settle(gameManager, h => hud = h);
            var incoming = hud.IncomingTradePanelForTests;
            Assert.IsTrue(incoming.gameObject.activeInHierarchy, "incoming offer visible");
            Assert.IsTrue(incoming.GetComponentsInChildren<Image>(true).Any(i => i.name.StartsWith("YouGiveIcon")), "red row");
            Assert.IsTrue(incoming.GetComponentsInChildren<Image>(true).Any(i => i.name.StartsWith("YouGetIcon")), "green row");
            MaybeWrite(hud, "trade-incoming");
            incoming.GetComponentsInChildren<Button>(true).First(b => b.name == "IncomingAccept").onClick.Invoke();
            yield return null;
            Assert.IsFalse(controller.State.IncomingAiTrade.HasValue, "incoming accepted");
        }

        [Timeout(120000)]
        [UnityTest]
        public IEnumerator CivEmblem_OpensInfoPopup()
        {
            yield return LoadScene();
            var gameManager = Object.FindFirstObjectByType<GameManager>();
            var controller = gameManager.Controller;
            GameScenePlayHarness.CompleteRunSelect(controller, gameManager.Meta, fishermenOfCatan: true);
            GameScenePlayHarness.CompleteSetup(controller);
            GameScenePlayHarness.AdvanceToDayPlayerActions(controller);
            controller.State.Civilization = CivilizationId.ForestFolk;
            ColonistCapturePieceSeeding.ForceDemoBoardPiecesForCapture(controller.State);
            Object.FindFirstObjectByType<BoardView>()?.Refresh(controller.State);

            RunHudController hud = null;
            yield return Settle(gameManager, h => hud = h);
            Assert.IsFalse(hud.IsCivInfoPopupVisibleForTests, "no always-on civ panel");
            MaybeWrite(hud, "sp-hud");
            hud.ClickOwnCivEmblemForTests();
            yield return null;
            Assert.IsTrue(hud.IsCivInfoPopupVisibleForTests, "emblem opens the civ popup");
            var name = CivilizationLibrary.Get(CivilizationId.ForestFolk).Name;
            Assert.IsTrue(hud.CivInfoPopupForTests.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains(name)));
            Canvas.ForceUpdateCanvases();
            yield return null;
            MaybeWrite(hud, "sp-civ-popup");
            hud.ClickOwnCivEmblemForTests();
            yield return null;
            Assert.IsFalse(hud.IsCivInfoPopupVisibleForTests, "second click closes");
        }
    }
}
