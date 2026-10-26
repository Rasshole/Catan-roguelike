using System.Collections;
using System.IO;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Match;
using CatanRoguelike.Core.Shop;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CatanRoguelike.Tests.PlayMode
{
    /// <summary>
    /// v143: Leave match returns to the menu with a clean slate (and a new game starts), "Show board" on the pick
    /// overlays, pulsing setup circles instead of ghost houses, and the multi-batch bank trade in the composer.
    /// Set COLONIST_HOTSEAT_SHOT_DIR to write review captures (v143-*.png).
    /// </summary>
    public class ColonistHotSeatV143CaptureTests
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

        private static IEnumerator ReadyHud(GameManager gameManager, System.Action<RunHudController> done)
        {
            RunHudController hud = null;
            for (int i = 0; i < 120; i++)
            {
                gameManager.EnsureRunHudReady();
                hud = gameManager.GetComponent<RunHudController>();
                if (hud != null && hud.IsHudCaptureReady())
                    break;
                yield return null;
            }

            Assert.IsNotNull(hud);
            hud.Refresh(gameManager.Controller.State);
            yield return Settle(hud);
            done(hud);
        }

        private static IEnumerator Settle(RunHudController hud, int frames = 4)
        {
            Canvas.ForceUpdateCanvases();
            if (hud.HudCanvas != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(hud.HudCanvas.GetComponent<RectTransform>());
            for (int i = 0; i < frames; i++)
                yield return null;
        }

        private static void MaybeWrite(RunHudController hud, string file)
        {
            string dir = System.Environment.GetEnvironmentVariable("COLONIST_HOTSEAT_SHOT_DIR");
            if (string.IsNullOrEmpty(dir))
                return;
            Directory.CreateDirectory(dir);
            var tex = GameSceneCapture.CaptureMainCameraWithHudComposite(hud);
            Assert.IsNotNull(tex);
            File.WriteAllBytes(Path.Combine(dir, file), tex.EncodeToPNG());
            Debug.Log($"v143 capture written: {file}");
        }

        private static Button FindButton(Transform root, string name)
        {
            var b = root.GetComponentsInChildren<Button>(true).FirstOrDefault(x => x.name == name);
            Assert.IsNotNull(b, "button " + name);
            return b;
        }

        private static MatchConfig Config(int seed, bool party, params (SeatKind kind, string name)[] seats)
        {
            var config = new MatchConfig { Seed = seed, MapSize = MatchConfig.RecommendedMap(seats.Length), PartyRules = party };
            foreach (var (kind, name) in seats)
                config.Seats.Add(new SeatConfig(kind, name));
            return config;
        }

        private static bool Drive(HotSeatSession s, System.Func<bool> stop, int guardMax = 20000)
        {
            int guard = 0;
            while (guard++ < guardMax && !s.IsOver)
            {
                s.Tick(0f);
                if (s.PassScreenPending)
                    s.ConfirmPass();
                if (MatchAi.AwaitingHumanAnswer(s.Match))
                {
                    s.Apply(MatchRunner.PendingResponse(s.Match));
                    continue;
                }

                if (s.Acting.IsAi)
                    continue;
                if (stop())
                    return true;
                switch (s.State.Phase)
                {
                    case MatchPhase.Roll:
                        s.Roll();
                        break;
                    case MatchPhase.Main:
                        s.AutoBuild();
                        s.EndTurn();
                        break;
                    default:
                        s.ApplySuggested();
                        break;
                }
            }

            return false;
        }

        private static IEnumerator WaitForMenu(RunHudController hud, GameManager gm)
        {
            for (int i = 0; i < 30 && (hud.IsHotSeatActive || PlaceholderHudLayout.IsPreGamePhase(gm.Controller.State.Phase) == false); i++)
                yield return null;
            yield return Settle(hud, 6);
        }

        [Timeout(180000)]
        [UnityTest]
        public IEnumerator PassAndPlay_ShowBoardOnCivPick_SetupCircles_LeaveToMenu_ThenStartAgain()
        {
            yield return LoadScene();
            var gm = Object.FindFirstObjectByType<GameManager>();
            RunHudController hud = null;
            yield return ReadyHud(gm, h => hud = h);

            hud.HotSeatClockFrozenForTests = true;
            hud.StartHotSeat(Config(1431, true, (SeatKind.Human, "Rasmus"), (SeatKind.Ai, "Astrid"), (SeatKind.Ai, "Bjorn")));
            var s = hud.HotSeatSessionForTests;
            Assert.AreEqual(MatchPhase.CivPick, s.State.Phase);
            yield return Settle(hud);
            if (s.PassScreenPending)
                FindButton(hud.HotSeatRootForTests, "ConfirmPass").onClick.Invoke();
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            Assert.IsNotNull(hud.HotSeatRootForTests.Find("CivPick"));

            FindButton(hud.HotSeatRootForTests, RunHudController.PickHideButtonName).onClick.Invoke();
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            Assert.IsTrue(hud.PickOverlayHiddenForTests);
            Assert.IsNull(hud.HotSeatRootForTests.Find("CivPick"), "cards tucked away");
            Assert.IsNotNull(hud.HotSeatRootForTests.Find("PickPeekBar"));
            MaybeWrite(hud, "v143-civ-hidden.png");

            FindButton(hud.HotSeatRootForTests, RunHudController.PickShowButtonName).onClick.Invoke();
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            Assert.IsFalse(hud.PickOverlayHiddenForTests);
            Assert.IsNotNull(hud.HotSeatRootForTests.Find("CivPick"), "cards back");
            FindButton(hud.HotSeatRootForTests, "PickCiv_0").onClick.Invoke();
            yield return Settle(hud);

            Assert.IsTrue(Drive(s, () => s.Match.IsSetup && s.State.ActingSeat == 0));
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            var input = hud.HotSeatBoardInputForTests;
            Assert.IsNotNull(input);
            Assert.Greater(input.MarkerCountForTests, 0, "setup spots marked");
            var circles = input.HighlightsRootForTests.GetComponentsInChildren<Transform>(true)
                .Count(t => t.name == PlacementMarkerVisuals.SpotCircleName);
            Assert.Greater(circles, 0, "small player-colour circles, not ghost houses");
            Assert.IsNotNull(input.HighlightsRootForTests.GetComponentInChildren<PlacementSpotPulse>(true), "circles pulse");
            MaybeWrite(hud, "v143-setup-circles.png");

            // Leave mid-setup through the settings menu.
            Assert.IsNotNull(gm.SettingsMenu);
            gm.SettingsMenu.Toggle();
            yield return Settle(hud);
            Assert.IsTrue(gm.SettingsMenu.IsOpen);
            FindButton(gm.SettingsMenu.PanelForTests, GameSettingsMenu.LeaveName).onClick.Invoke();
            yield return WaitForMenu(hud, gm);
            Assert.IsFalse(hud.IsHotSeatActive, "left the table");
            Assert.IsFalse(gm.SettingsMenu.IsOpen);
            Assert.IsTrue(PlaceholderHudLayout.IsPreGamePhase(gm.Controller.State.Phase));
            Assert.IsTrue(hud.IsPreGameMenuVisibleForTests, "back at the start menu");
            Assert.IsTrue(hud.IsMainMenuOpenForTests, "Leave lands on the main menu");
            Assert.IsNull(hud.HotSeatRootForTests, "no stale table widgets");
            MaybeWrite(hud, "v143-menu-after-leave.png");

            // ...and a fresh table starts cleanly.
            hud.OpenHotSeatLobbyForTests();
            yield return Settle(hud);
            var menu = hud.PreGameMenuRootForTests;
            FindButton(menu, "FillAi").onClick.Invoke();
            yield return Settle(hud);
            FindButton(hud.PreGameMenuRootForTests, "StartMatch").onClick.Invoke();
            hud.HotSeatClockFrozenForTests = true;
            yield return Settle(hud);
            Assert.IsTrue(hud.IsHotSeatActive, "second game started");
            Assert.AreNotSame(s, hud.HotSeatSessionForTests);
            FindButton(hud.HotSeatRootForTests, "LeaveMatch").onClick.Invoke();
            yield return WaitForMenu(hud, gm);
            Assert.IsFalse(hud.IsHotSeatActive);
            Assert.IsTrue(hud.IsPreGameMenuVisibleForTests);
            Assert.IsTrue(hud.IsMainMenuOpenForTests);
        }

        [Timeout(180000)]
        [UnityTest]
        public IEnumerator PassAndPlay_TwoDiceRollAnimates_LandsOnRoll_AndCardsFlyToTheHand()
        {
            yield return LoadScene();
            var gm = Object.FindFirstObjectByType<GameManager>();
            RunHudController hud = null;
            yield return ReadyHud(gm, h => hud = h);

            hud.HotSeatClockFrozenForTests = true;
            hud.StartHotSeat(Config(77, false, (SeatKind.Human, "Rasmus"), (SeatKind.Ai, "Astrid"), (SeatKind.Ai, "Bjorn")));
            var s = hud.HotSeatSessionForTests;
            Assert.IsTrue(Drive(s, () => s.State.Phase == MatchPhase.Roll && s.State.ActingSeat == 0));
            if (s.PassScreenPending)
                s.ConfirmPass();
            hud.RefreshHotSeatNow();
            yield return Settle(hud, 4);
            yield return new WaitForSeconds(1.5f);
            yield return Settle(hud, 2);
            Assert.AreEqual(0, hud.FlyingCardCountForTests, "nothing in flight before the roll");

            GameAudio.PlayedForTests.Clear();
            Assert.IsTrue(s.Roll().Ok);
            yield return Settle(hud, 3);
            Assert.IsTrue(hud.DiceVisibleForTests, "dice tray shown");
            Assert.IsTrue(hud.DiceRollingForTests, "dice tumble");
            Assert.Contains(Sfx.Dice, GameAudio.PlayedForTests);
            MaybeWrite(hud, "v143-dice-rolling.png");

            yield return new WaitForSeconds(RunHudController.DiceRollSeconds + 0.4f);
            yield return Settle(hud, 2);
            Assert.IsFalse(hud.DiceRollingForTests, "landed after ~1 s");
            Assert.IsTrue(hud.DiceVisibleForTests, "the result stays visible");
            Assert.AreEqual((s.State.LastDieA, s.State.LastDieB), hud.DiceFacesForTests, "faces show the real roll");
            Assert.AreEqual(s.State.LastRoll, s.State.LastDieA + s.State.LastDieB);
            MaybeWrite(hud, "v143-dice-landed.png");

            // Resolve a 7 if it came up, then trade with the bank: the bought card flies into the hand.
            for (int guard = 0; guard < 200 && !(s.State.Phase == MatchPhase.Main && s.State.ActingSeat == 0); guard++)
            {
                if (MatchAi.AwaitingHumanAnswer(s.Match))
                    s.Apply(MatchRunner.PendingResponse(s.Match));
                else if (s.Acting.IsAi)
                    s.Tick(0f);
                else
                    s.ApplySuggested();
            }

            Assert.AreEqual(MatchPhase.Main, s.State.Phase);
            var seat = s.State.Seats[0];
            var hand = seat.Hand;
            hand.Wood = 4;
            seat.Hand = hand;
            yield return Settle(hud, 4);
            yield return new WaitForSeconds(RunHudController.CardFlySeconds + 0.3f);
            yield return Settle(hud, 2);
            GameAudio.PlayedForTests.Clear();
            Assert.IsTrue(s.Apply(MatchCommand.Trade(0, ResourceType.Wood, ResourceType.Stone)).Ok);
            yield return Settle(hud, 3);
            Assert.Greater(hud.FlyingCardCountForTests, 0, "bank → hand card in flight");
            MaybeWrite(hud, "v143-card-flying.png");
            yield return new WaitForSeconds(RunHudController.CardFlySeconds + 0.4f);
            yield return Settle(hud, 2);
            Assert.AreEqual(0, hud.FlyingCardCountForTests, "flies land within ~0.5 s");
        }

        [Timeout(300000)]
        [UnityTest]
        public IEnumerator MainMenu_StartGame_SetupScreen_EveryMapSelectable_GameStarts()
        {
            yield return LoadScene();
            var gm = Object.FindFirstObjectByType<GameManager>();
            RunHudController hud = null;
            yield return ReadyHud(gm, h => hud = h);

            Assert.IsTrue(hud.IsMainMenuOpenForTests, "Play lands on the main menu");
            var menu = hud.PreGameMenuRootForTests;
            Assert.IsTrue(menu.GetComponentsInChildren<Text>(true).Any(t => t.text == "Catan Roguelike"), "title");
            FindButton(menu, RunHudController.MainMenuSettingsName);
            FindButton(menu, RunHudController.MainMenuQuitName);
            FindButton(menu, RunHudController.MainMenuSoloName);
            Assert.IsFalse(menu.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains("Spend stars")));
            MaybeWrite(hud, "v143-main-menu.png");

            FindButton(menu, RunHudController.MainMenuStartName).onClick.Invoke();
            yield return Settle(hud);
            Assert.IsTrue(hud.IsHotSeatLobbyOpenForTests, "Start game opens the setup screen");
            Assert.IsFalse(hud.IsMainMenuOpenForTests);

            // Every map is selectable — no star unlocks needed.
            foreach (MapSize size in System.Enum.GetValues(typeof(MapSize)))
            {
                var tile = FindButton(hud.PreGameMenuRootForTests, RunHudController.SetupMapPrefix + size);
                Assert.IsTrue(tile.interactable, size + " unlocked");
                tile.onClick.Invoke();
                yield return Settle(hud, 2);
                Assert.AreEqual(size, hud.LobbyMap);
            }

            // Rules: Fishermen on, 11 VP, timer off, no shenanigans; Medium map; five seats.
            menu = hud.PreGameMenuRootForTests;
            FindButton(menu, RunHudController.SetupMapPrefix + MapSize.Large).onClick.Invoke();
            yield return Settle(hud, 2);
            FindButton(hud.PreGameMenuRootForTests, "SeatPlus").onClick.Invoke();
            yield return Settle(hud, 2);
            FindButton(hud.PreGameMenuRootForTests, "Fishermen").onClick.Invoke();
            yield return Settle(hud, 2);
            FindButton(hud.PreGameMenuRootForTests, "VictoryPointsPlus").onClick.Invoke();
            yield return Settle(hud, 2);
            FindButton(hud.PreGameMenuRootForTests, "TurnTimerPlus").onClick.Invoke();
            yield return Settle(hud, 2);
            FindButton(hud.PreGameMenuRootForTests, "Shenanigans").onClick.Invoke();
            yield return Settle(hud, 2);
            FindButton(hud.PreGameMenuRootForTests, "FillAi").onClick.Invoke();
            yield return Settle(hud);
            MaybeWrite(hud, "v143-setup.png");
            FindButton(hud.PreGameMenuRootForTests, "TurnTimer").onClick.Invoke();
            yield return Settle(hud, 2);

            var config = hud.BuildLobbyConfig(5);
            Assert.AreEqual(MapSize.Large, config.MapSize);
            Assert.AreEqual(5, config.Seats.Count);
            Assert.IsTrue(config.Fishermen);
            Assert.AreEqual(11, config.VictoryPoints);
            Assert.IsFalse(config.TurnTimer);
            Assert.AreEqual(120, config.TurnSeconds);
            Assert.IsFalse(config.Shenanigans);
            Assert.IsTrue(config.EventCards);

            FindButton(hud.PreGameMenuRootForTests, "StartMatch").onClick.Invoke();
            hud.HotSeatClockFrozenForTests = true;
            yield return Settle(hud);
            Assert.IsTrue(hud.IsHotSeatActive);
            var started = hud.HotSeatSessionForTests.State.Config;
            Assert.AreEqual(MapSize.Large, started.MapSize);
            Assert.AreEqual(11, started.VictoryPoints);
            Assert.IsTrue(started.Fishermen);
            Assert.IsFalse(hud.HotSeatSessionForTests.Clock.Enabled, "untimed table");
            Assert.IsFalse(hud.HotSeatSessionForTests.Match.ShenanigansOn);
            FindButton(hud.HotSeatRootForTests, "LeaveMatch").onClick.Invoke();
            yield return WaitForMenu(hud, gm);
            Assert.IsTrue(hud.IsMainMenuOpenForTests);

            // ...and a game starts on every map.
            foreach (MapSize size in System.Enum.GetValues(typeof(MapSize)))
            {
                FindButton(hud.PreGameMenuRootForTests, RunHudController.MainMenuStartName).onClick.Invoke();
                yield return Settle(hud, 2);
                FindButton(hud.PreGameMenuRootForTests, RunHudController.SetupMapPrefix + size).onClick.Invoke();
                yield return Settle(hud, 2);
                FindButton(hud.PreGameMenuRootForTests, "FillAi").onClick.Invoke();
                yield return Settle(hud, 2);
                FindButton(hud.PreGameMenuRootForTests, "StartMatch").onClick.Invoke();
                hud.HotSeatClockFrozenForTests = true;
                yield return Settle(hud);
                Assert.IsTrue(hud.IsHotSeatActive, size + " table started");
                Assert.AreEqual(size, hud.HotSeatSessionForTests.State.Config.MapSize);
                Assert.AreEqual(MapPresets.GetHexCount(size),
                    hud.HotSeatSessionForTests.State.Board.LandTileCount, size + " land hexes");
                FindButton(hud.HotSeatRootForTests, "LeaveMatch").onClick.Invoke();
                yield return WaitForMenu(hud, gm);
                Assert.IsTrue(hud.IsMainMenuOpenForTests, size + ": back to the main menu");
            }
        }

        [Timeout(180000)]
        [UnityTest]
        public IEnumerator PassAndPlay_BankComposer_TradesSeveralBatchesInOneGo()
        {
            yield return LoadScene();
            var gm = Object.FindFirstObjectByType<GameManager>();
            RunHudController hud = null;
            yield return ReadyHud(gm, h => hud = h);

            hud.HotSeatClockFrozenForTests = true;
            hud.StartHotSeat(Config(1432, false, (SeatKind.Human, "Rasmus"), (SeatKind.Ai, "Astrid"), (SeatKind.Ai, "Bjorn")));
            var s = hud.HotSeatSessionForTests;
            Assert.IsTrue(Drive(s, () => s.State.Phase == MatchPhase.Main && s.State.CurrentSeat == 0));
            var board = s.State.Board;
            var port = s.State.Ports.First(p => p.SpecificResource == ResourceType.Sheep);
            var pier = PortAccess.ServedVertices(board, port).Last();
            board.VertexBuildings[pier] = (BuildingType.Settlement, MatchSeats.ToPlayerId(0));
            Assert.AreEqual(2, s.Match.BankRatio(0, ResourceType.Sheep), "either pier of the wool harbour");
            s.State.Seats[0].Hand = new ResourceBundle { Sheep = 4, Stone = 3 };
            var give = new ResourceBundle { Sheep = 4 };
            var get = new ResourceBundle { Wheat = 1, Brick = 1 };
            hud.SetHotSeatTradeForTests(true, give, get);
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            var bank = FindButton(hud.HotSeatRootForTests, "BankTrade");
            Assert.IsTrue(bank.interactable, "4 wool at 2:1 buys 2 cards");
            StringAssert.Contains("4→2", bank.GetComponentInChildren<Text>(true).text);
            MaybeWrite(hud, "v143-batch-trade.png");
            bank.onClick.Invoke();
            yield return Settle(hud);
            Assert.AreEqual(new ResourceBundle { Wheat = 1, Brick = 1, Stone = 3 }, s.State.Seats[0].Hand);

            hud.SetHotSeatTradeForTests(true, new ResourceBundle { Stone = 3 }, new ResourceBundle { Wood = 1 });
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            Assert.IsFalse(FindButton(hud.HotSeatRootForTests, "BankTrade").interactable, "3 ore at 4:1 is not whole batches");
        }

        [Timeout(180000)]
        [UnityTest]
        public IEnumerator SinglePlayer_CivPickShowBoard_ThenLeaveFromSettings_ReturnsToMenu()
        {
            yield return LoadScene();
            var gm = Object.FindFirstObjectByType<GameManager>();
            GameScenePlayHarness.CompleteRunSelect(gm.Controller, gm.Meta, fishermenOfCatan: true, stopAtCivilizationPick: true);
            RunHudController hud = null;
            yield return ReadyHud(gm, h => hud = h);
            Assert.IsTrue(hud.IsCivilizationPickVisibleForTests);
            var root = hud.CivilizationPickRootForTests;
            FindButton(root, RunHudController.PickHideButtonName).onClick.Invoke();
            yield return Settle(hud);
            Assert.IsTrue(hud.PickOverlayHiddenForTests);
            root = hud.CivilizationPickRootForTests;
            Assert.IsFalse(root.Find(RunHudController.PickHideButtonName).gameObject.activeSelf);
            Assert.IsTrue(root.Find(RunHudController.PickShowButtonName).gameObject.activeSelf);
            MaybeWrite(hud, "v143-sp-civ-hidden.png");
            FindButton(root, RunHudController.PickShowButtonName).onClick.Invoke();
            yield return Settle(hud);
            Assert.IsFalse(hud.PickOverlayHiddenForTests);
            Assert.IsTrue(hud.CivilizationPickRootForTests.Find(RunHudController.PickHideButtonName).gameObject.activeSelf);

            Assert.IsTrue(gm.Controller.ChooseCivilization(gm.Controller.State.CivilizationOffer[0]));
            hud.Refresh(gm.Controller.State);
            yield return Settle(hud);
            var oldController = gm.Controller;
            gm.SettingsMenu.Toggle();
            yield return Settle(hud);
            FindButton(gm.SettingsMenu.PanelForTests, GameSettingsMenu.LeaveName).onClick.Invoke();
            for (int i = 0; i < 10; i++)
                yield return null;
            yield return Settle(hud, 6);
            Assert.AreNotSame(oldController, gm.Controller, "fresh run");
            Assert.IsTrue(PlaceholderHudLayout.IsPreGamePhase(gm.Controller.State.Phase));
            Assert.IsTrue(hud.IsPreGameMenuVisibleForTests);
            Assert.IsFalse(hud.IsCivilizationPickVisibleForTests);

            // Start a new single-player run all the way to the board.
            GameScenePlayHarness.CompleteRunSelect(gm.Controller, gm.Meta, fishermenOfCatan: true, stopAtCivilizationPick: true);
            hud.Refresh(gm.Controller.State);
            yield return Settle(hud);
            Assert.IsTrue(hud.IsCivilizationPickVisibleForTests, "second run reaches the civ pick");
        }
    }
}
