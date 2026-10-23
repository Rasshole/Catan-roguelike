using System.Collections;
using System.IO;
using System.Linq;
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
                if (s.Current.IsAi)
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
    }
}
