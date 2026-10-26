using System.Collections;
using System.IO;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Match;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CatanRoguelike.Tests.PlayMode
{
    /// <summary>
    /// v134 Phase 2: pass-and-play lobby (any Human/AI mix, open seats filled with AI), the N-seat table HUD,
    /// the pass screen hiding hands, and the turn timer with the manual +90 s charge and last-5 s pulse.
    /// Set COLONIST_HOTSEAT_SHOT_DIR to write review captures.
    /// </summary>
    public class ColonistHotSeatCaptureTests
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

        private static IEnumerator Settle(RunHudController hud)
        {
            Canvas.ForceUpdateCanvases();
            if (hud.HudCanvas != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(hud.HudCanvas.GetComponent<RectTransform>());
            for (int i = 0; i < 4; i++)
                yield return null;
        }

        private static void MaybeWrite(RunHudController hud, string file)
        {
            string dir = System.Environment.GetEnvironmentVariable("COLONIST_HOTSEAT_SHOT_DIR");
            if (string.IsNullOrEmpty(dir))
                return;
            Directory.CreateDirectory(dir);
            var c = hud.HudCanvas;
            Debug.Log($"v134 hot-seat pre-capture {file} frame={Time.frameCount} canvas={(c != null ? c.GetInstanceID() : 0)} " +
                      $"active={(c != null && c.gameObject.activeSelf)} inHier={(c != null && c.gameObject.activeInHierarchy)} " +
                      $"parent={(c != null ? c.transform.parent?.name : "-")} root={(hud.HotSeatRootForTests != null)}");
            var tex = GameSceneCapture.CaptureMainCameraWithHudComposite(hud);
            Assert.IsNotNull(tex);
            string path = Path.Combine(dir, file);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log($"v134 hot-seat capture written: {path}");
        }

        private static Button FindButton(Transform root, string name)
        {
            var b = root.GetComponentsInChildren<Button>(true).FirstOrDefault(x => x.name == name);
            Assert.IsNotNull(b, "button " + name);
            return b;
        }

        [Timeout(120000)]
        [UnityTest]
        public IEnumerator Lobby_ThreeHumansAndOpenSeat_FillWithAi_StartsFourSeatTable_WithPassScreen()
        {
            yield return LoadScene();
            var gameManager = Object.FindFirstObjectByType<GameManager>();
            RunHudController hud = null;
            yield return ReadyHud(gameManager, h => hud = h);

            hud.OpenHotSeatLobbyForTests();
            yield return Settle(hud);
            Assert.IsTrue(hud.IsHotSeatLobbyOpenForTests);
            var menu = hud.PreGameMenuRootForTests;
            Assert.IsNotNull(menu);
            Assert.AreEqual(4, hud.HotSeatLobbySeatsForTests.Count);
            Assert.IsFalse(FindButton(menu, "StartMatch").interactable, "open seat blocks start");
            MaybeWrite(hud, "lobby.png");

            FindButton(menu, "FillAi").onClick.Invoke();
            yield return Settle(hud);
            Assert.IsTrue(hud.HotSeatLobbySeatsForTests.All(s => s.HasValue));
            Assert.AreEqual(SeatKind.Ai, hud.HotSeatLobbySeatsForTests[3]);
            menu = hud.PreGameMenuRootForTests;
            Assert.IsTrue(FindButton(menu, "StartMatch").interactable);
            MaybeWrite(hud, "lobby-filled.png");

            FindButton(menu, "StartMatch").onClick.Invoke();
            hud.HotSeatClockFrozenForTests = true;
            yield return Settle(hud);
            Assert.IsTrue(hud.IsHotSeatActive);
            var session = hud.HotSeatSessionForTests;
            Assert.AreEqual(4, session.State.Seats.Count);
            Assert.AreEqual(3, session.HumanCount);
            Assert.IsTrue(session.PassScreenPending, "first human turn is behind a pass screen");
            Assert.IsFalse(session.IsHandVisible(session.State.CurrentSeat));
            MaybeWrite(hud, "pass.png");

            FindButton(hud.HotSeatRootForTests, "ConfirmPass").onClick.Invoke();
            yield return Settle(hud);
            Assert.IsFalse(session.PassScreenPending);
            Assert.IsTrue(session.IsHandVisible(session.State.CurrentSeat));

            FindButton(hud.HotSeatRootForTests, "LeaveMatch").onClick.Invoke();
            yield return Settle(hud);
            Assert.IsFalse(hud.IsHotSeatActive);
        }

        [Timeout(180000)]
        [UnityTest]
        public IEnumerator FiveSeatTable_ShowsStrip_TimerPulsesInLastFiveSeconds_ChargeAddsNinety()
        {
            yield return LoadScene();
            var gameManager = Object.FindFirstObjectByType<GameManager>();
            RunHudController hud = null;
            yield return ReadyHud(gameManager, h => hud = h);

            var config = new MatchConfig { Seed = 2026, MapSize = MatchConfig.RecommendedMap(5) };
            config.Seats.Add(new SeatConfig(SeatKind.Human, "Rasmus"));
            config.Seats.Add(new SeatConfig(SeatKind.Ai, "AI 2"));
            config.Seats.Add(new SeatConfig(SeatKind.Human, "Freja"));
            config.Seats.Add(new SeatConfig(SeatKind.Ai, "AI 4"));
            config.Seats.Add(new SeatConfig(SeatKind.Ai, "AI 5"));
            hud.HotSeatClockFrozenForTests = true;
            hud.StartHotSeat(config);
            var s = hud.HotSeatSessionForTests;

            int guard = 0;
            while (guard++ < 5000 && !s.IsOver)
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
                if (s.State.Round >= 8 && s.State.Phase == MatchPhase.Main && s.Current.CardCount >= 3)
                    break;
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

            Assert.IsFalse(s.IsOver, "stopped mid-game on a human turn");
            Assert.IsFalse(s.Current.IsAi);
            Assert.IsTrue(s.CanUseExtraTime, "a charge is available");
            int charges = s.Current.ExtraTimeCharges;

            s.Clock.Tick(TurnClock.TurnSeconds - 3.4f);
            Assert.IsTrue(s.Clock.ShouldPulse);
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            var root = hud.HotSeatRootForTests;
            Debug.Log($"v134 hot-seat root frame={Time.frameCount} alive={(root != null)} canvas={(hud.HudCanvas != null ? hud.HudCanvas.GetInstanceID() : 0)}");
            Assert.IsNotNull(root, "table root alive");
            for (int i = 0; i < 5; i++)
                Assert.IsNotNull(root.Find("SeatStrip/Seat_" + i), "seat row " + i);
            Assert.IsTrue(FindButton(root, "ExtraTimeCharge").interactable);
            var seconds = root.Find("Timer/Seconds").GetComponent<Text>();
            Assert.AreEqual("4", seconds.text);
            Assert.Greater(seconds.rectTransform.localScale.x, 1.1f, "number grows in the last 5 s");
            MaybeWrite(hud, "hud-5p-pulse.png");

            FindButton(root, "ExtraTimeCharge").onClick.Invoke();
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            Assert.Greater(s.Clock.Remaining, 90f);
            Assert.AreEqual(charges - 1, s.Current.ExtraTimeCharges);
            Assert.IsFalse(FindButton(hud.HotSeatRootForTests, "ExtraTimeCharge").interactable, "once per turn");
            MaybeWrite(hud, "hud-5p-charged.png");

            hud.ExitHotSeat();
            yield return Settle(hud);
        }

        /// <summary>
        /// v135: a 4-seat table (2 humans, 2 AI) plays until a human must discard on a 7 (pass screen, then the
        /// discard picker), then on Rasmus's turn: the trade board (offer, AI answers, Freja's counter, confirm),
        /// development cards with the Year of Plenty picker, and board-click highlights.
        /// </summary>
        [Timeout(240000)]
        [UnityTest]
        public IEnumerator FourSeatTable_Discard_Trade_DevCards_BoardClicks()
        {
            yield return LoadScene();
            var gameManager = Object.FindFirstObjectByType<GameManager>();
            RunHudController hud = null;
            yield return ReadyHud(gameManager, h => hud = h);

            var config = new MatchConfig { Seed = 135, MapSize = MatchConfig.RecommendedMap(4) };
            config.Seats.Add(new SeatConfig(SeatKind.Human, "Rasmus"));
            config.Seats.Add(new SeatConfig(SeatKind.Human, "Freja"));
            config.Seats.Add(new SeatConfig(SeatKind.Ai, "AI 3"));
            config.Seats.Add(new SeatConfig(SeatKind.Ai, "AI 4"));
            hud.HotSeatClockFrozenForTests = true;
            hud.StartHotSeat(config);
            var s = hud.HotSeatSessionForTests;
            var state = s.State;

            // Play until Freja owes a discard on a 7 (keep her hand over the limit while not discarding).
            bool sawDiscard = false;
            int guard = 0;
            while (guard++ < 20000 && !s.IsOver && !sawDiscard)
            {
                if (!s.Match.IsSetup && state.Phase != MatchPhase.Discard && state.Seats[1].CardCount <= 7)
                    state.Seats[1].Hand = new ResourceBundle { Wood = 3, Brick = 2, Wheat = 2, Sheep = 2, Stone = 1 };
                s.Tick(0f);
                if (state.Phase == MatchPhase.Discard && state.ActingSeat == 1 && s.PassScreenPending)
                {
                    sawDiscard = true;
                    break;
                }

                if (s.PassScreenPending)
                    s.ConfirmPass();
                if (MatchAi.AwaitingHumanAnswer(s.Match))
                {
                    s.Apply(MatchRunner.PendingResponse(s.Match));
                    continue;
                }

                if (s.Acting.IsAi)
                    continue;
                switch (state.Phase)
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

            Assert.IsTrue(sawDiscard, "a 7 with Freja over the limit");
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            var root = hud.HotSeatRootForTests;
            Assert.IsNotNull(root.Find("PassScreen"), "discard is behind Freja's pass screen");
            Assert.IsNull(root.Find("DiscardPicker"), "no picker before she takes the device");
            MaybeWrite(hud, "discard-pass.png");

            FindButton(root, "ConfirmPass").onClick.Invoke();
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            root = hud.HotSeatRootForTests;
            Assert.IsNotNull(root.Find("DiscardPicker"));
            int owed = state.PendingDiscards[1];
            Assert.IsFalse(FindButton(root, "DiscardConfirm").interactable, "pick exactly half first");
            hud.SetHotSeatDiscardForTests(MatchController.SuggestDiscard(state.Seats[1].Hand, owed - 1));
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            var more = hud.HotSeatRootForTests.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(x => x.name.StartsWith("More_") && x.interactable);
            Assert.IsNotNull(more, "one more card can be picked");
            more.onClick.Invoke();
            if (!FindButton(hud.HotSeatRootForTests, "DiscardConfirm").interactable)
                hud.SetHotSeatDiscardForTests(MatchController.SuggestDiscard(state.Seats[1].Hand, owed));
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            MaybeWrite(hud, "discard.png");
            int before = state.Seats[1].CardCount;
            FindButton(hud.HotSeatRootForTests, "DiscardConfirm").onClick.Invoke();
            Assert.AreEqual(before - owed, state.Seats[1].CardCount);
            Assert.IsFalse(state.PendingDiscards.ContainsKey(1));

            // Continue to Rasmus's main phase.
            guard = 0;
            while (guard++ < 20000 && !s.IsOver)
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
                if (state.CurrentSeat == 0 && state.Phase == MatchPhase.Main && state.ActiveTrade == null)
                    break;
                switch (state.Phase)
                {
                    case MatchPhase.Roll:
                        s.Roll();
                        break;
                    case MatchPhase.Main:
                        s.EndTurn();
                        break;
                    default:
                        s.ApplySuggested();
                        break;
                }
            }

            Assert.IsFalse(s.IsOver);
            state.Seats[0].Hand = new ResourceBundle { Wood = 3, Brick = 2, Wheat = 2, Sheep = 1, Stone = 0 };
            state.Seats[1].Hand = new ResourceBundle { Stone = 2, Wheat = 1 };
            state.Seats[2].Hand = new ResourceBundle { Stone = 1, Sheep = 2 };
            state.Seats[3].Hand = new ResourceBundle { Wood = 1 };

            // Board clicks: affordable builds glow on the board.
            hud.RefreshHotSeatNow();
            hud.HotSeatBoardInputForTests.RefreshTargets();
            yield return Settle(hud);
            Assert.Greater(hud.HotSeatBoardInputForTests.MarkerCountForTests, 0, "build spots highlighted");
            Assert.IsNull(hud.HotSeatBoardInputForTests.HighlightsRootForTests.GetComponentInParent<Canvas>(),
                "board highlights live in world space, not under the HUD canvas");

            // Trade board: compose, offer, AI answers, Freja counters, confirm.
            FindButton(hud.HotSeatRootForTests, "Action_0").onClick.Invoke();
            hud.SetHotSeatTradeForTests(true, new ResourceBundle { Wood = 1 }, new ResourceBundle { Stone = 1 });
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            root = hud.HotSeatRootForTests;
            Assert.IsNotNull(root.Find("TradeBoard"));
            Assert.IsTrue(FindButton(root, "ProposeTrade").interactable);
            // v139: "you get" row on top, "you give" below; both rows end in the wildcard card.
            var tradeBoard = root.Find("TradeBoard");
            Assert.Greater(tradeBoard.Find("GetMore_Stone").position.y, tradeBoard.Find("GiveMore_Wood").position.y,
                "get row above the give row");
            Assert.IsNotNull(tradeBoard.Find("GiveMore_Any"));
            Assert.IsNotNull(tradeBoard.Find("GetMore_Any"));
            // Party rules off: no leaders yet, so the seats keep the coloured disc with the number / AI tag.
            Assert.IsNull(tradeBoard.Find("ComposeTo_1/Face"));
            Assert.IsNotNull(tradeBoard.Find("ComposeTo_1/Tag"));
            MaybeWrite(hud, "trade-compose.png");

            FindButton(root, "ProposeTrade").onClick.Invoke();
            Assert.IsNotNull(state.ActiveTrade);
            Assert.AreEqual(TradeResponseKind.Accepted, state.ActiveTrade.Responses[2].Kind, "AI 3 has ore and accepts");
            Assert.AreEqual(TradeResponseKind.Declined, state.ActiveTrade.Responses[3].Kind, "AI 4 has none");
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            // v138: Freja looks at the incoming offer from her side (her give = Rasmus's get, in red).
            hud.SetHotSeatTradeViewerForTests(1);
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            Assert.IsNotNull(hud.HotSeatRootForTests.Find("TradeBoard"));
            Assert.IsNotNull(FindButton(hud.HotSeatRootForTests, "Accept_1"), "responder answers with icon buttons");
            Assert.IsFalse(hud.HotSeatRootForTests.Find("TradeBoard").GetComponentsInChildren<Text>(true)
                .Any(t => t.text.Contains("Wood") || t.text.Contains("Ore") || t.text.Contains("Stone")), "icons, no resource words");
            MaybeWrite(hud, "trade-incoming-freja.png");
            hud.SetHotSeatTradeViewerForTests(0);
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            FindButton(hud.HotSeatRootForTests, "Counter_1").onClick.Invoke();
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            FindButton(hud.HotSeatRootForTests, "CYouGetMore_Wood").onClick.Invoke();
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            FindButton(hud.HotSeatRootForTests, "SendCounter").onClick.Invoke();
            Assert.AreEqual(TradeResponseKind.Countered, state.ActiveTrade.Responses[1].Kind);
            Assert.AreEqual(2, state.ActiveTrade.Responses[1].CounterGive.Wood);
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            MaybeWrite(hud, "trade-board.png");
            FindButton(hud.HotSeatRootForTests, "Confirm_1").onClick.Invoke();
            Assert.IsNull(state.ActiveTrade);
            Assert.AreEqual(1, state.Seats[0].Hand.Wood);
            Assert.AreEqual(1, state.Seats[0].Hand.Stone);
            Assert.AreEqual(2, state.Seats[1].Hand.Wood);

            // Development cards: playable ones lit, a fresh one waits, Year of Plenty picker.
            state.Seats[0].DevCards.Add(MatchDevCard.Knight);
            state.Seats[0].DevCards.Add(MatchDevCard.YearOfPlenty);
            state.Seats[0].NewDevCards.Add(MatchDevCard.Monopoly);
            state.Seats[0].NewDevCards.Add(MatchDevCard.VictoryPoint);
            s.Match.RecomputeScores();
            hud.SetHotSeatTradeForTests(false, default, default);
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            root = hud.HotSeatRootForTests;
            Assert.IsTrue(FindButton(root, "Dev_Knight").interactable);
            Assert.IsFalse(FindButton(root, "Dev_Monopoly").interactable, "bought this turn");
            Assert.IsFalse(FindButton(root, "Dev_VictoryPoint").interactable);
            MaybeWrite(hud, "devcards.png");
            FindButton(root, "Dev_YearOfPlenty").onClick.Invoke();
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            Assert.IsNotNull(hud.HotSeatRootForTests.Find("DevCardPicker"));
            MaybeWrite(hud, "yop.png");
            int wheat = state.Seats[0].Hand.Wheat;
            FindButton(hud.HotSeatRootForTests, "Pick_Wheat").onClick.Invoke();
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            FindButton(hud.HotSeatRootForTests, "Pick_Wheat").onClick.Invoke();
            Assert.AreEqual(wheat + 2, state.Seats[0].Hand.Wheat);
            Assert.IsTrue(state.Seats[0].PlayedDevCardThisTurn);

            hud.ExitHotSeat();
            yield return Settle(hud);
        }
    }
}
