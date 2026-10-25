using System.Collections;
using System.IO;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Events;
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
    /// v142 human-like AI on the pass-and-play table: an AI's trade offer waits for the human (who sees it as an
    /// incoming offer), the AIs answer a human's offer, sabotage spots drawn as resource icon + number token, and a
    /// 5-player midgame board. Set COLONIST_HOTSEAT_SHOT_DIR to write review captures.
    /// </summary>
    public class ColonistHotSeatV142CaptureTests
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
            hud.RefreshHotSeatNow();
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
            var tex = GameSceneCapture.CaptureMainCameraWithHudComposite(hud);
            Assert.IsNotNull(tex);
            string path = Path.Combine(dir, file);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log($"v142 hot-seat capture written: {path}");
        }

        private static Button FindButton(Transform root, string name)
        {
            var b = root.GetComponentsInChildren<Button>(true).FirstOrDefault(x => x.name == name);
            Assert.IsNotNull(b, "button " + name);
            return b;
        }

        private static IEnumerator Click(RunHudController hud, string name)
        {
            var b = FindButton(hud.HotSeatRootForTests, name);
            Assert.IsTrue(b.interactable, name + " is enabled");
            b.onClick.Invoke();
            yield return Settle(hud);
        }

        /// <summary>Clicks away any public reveal (ballot, auction, sabotage) so the table underneath is visible.</summary>
        private static IEnumerator DismissReveals(RunHudController hud)
        {
            for (int i = 0; i < 6; i++)
            {
                yield return Settle(hud);
                var b = hud.HotSeatRootForTests.GetComponentsInChildren<Button>(true).FirstOrDefault(x =>
                    x.name == "RevealContinue" || x.name == "AuctionContinue" || x.name == "SabotageContinue");
                if (b == null)
                    yield break;
                b.onClick.Invoke();
            }

            yield return Settle(hud);
        }

        private static MatchConfig PartyConfig(int seed, params (SeatKind kind, string name)[] seats)
        {
            var config = new MatchConfig { Seed = seed, MapSize = MatchConfig.RecommendedMap(seats.Length), PartyRules = true };
            foreach (var (kind, name) in seats)
                config.Seats.Add(new SeatConfig(kind, name));
            return config;
        }

        /// <summary>
        /// Drives human seats with the suggestion policy until <paramref name="stop"/> holds. Checked before anything
        /// else, so it can stop while an AI's offer waits for the human.
        /// </summary>
        private static bool Drive(HotSeatSession s, System.Func<bool> stop, int guardMax = 40000)
        {
            int guard = 0;
            while (guard++ < guardMax && !s.IsOver)
            {
                s.Tick(0f);
                if (s.IsOver)
                    return false;
                if (s.PassScreenPending)
                    s.ConfirmPass();
                if (stop())
                    return true;
                if (MatchAi.AwaitingHumanAnswer(s.Match))
                {
                    s.Apply(MatchRunner.PendingResponse(s.Match));
                    continue;
                }

                if (s.Acting.IsAi)
                    continue;
                switch (s.State.Phase)
                {
                    case MatchPhase.Roll:
                        s.Roll();
                        break;
                    case MatchPhase.Main:
                        if (s.State.ActiveTrade != null)
                            s.Apply(MatchCommand.Simple(MatchCommandType.CancelTrade, s.State.CurrentSeat));
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

        /// <summary>Clears earlier shenanigans so the captured turn starts from a clean slate.</summary>
        private static void ClearShenanigans(MatchState state)
        {
            state.Bounties.Clear();
            state.PendingSabotage.Clear();
            state.LastSabotageReveal = null;
            state.LastBallot = null;
            state.LastAuction = null;
            foreach (var seat in state.Seats)
                seat.Sabotage.Clear();
        }

        private static Transform Find(RunHudController hud, string path) => hud.HotSeatRootForTests.Find(path);

        [Timeout(300000)]
        [UnityTest]
        public IEnumerator V142_AiOffersTheHuman_AiAnswers_And_Midgame5p()
        {
            yield return LoadScene();
            var gameManager = Object.FindFirstObjectByType<GameManager>();
            RunHudController hud = null;
            yield return ReadyHud(gameManager, h => hud = h);

            var config = PartyConfig(1421, (SeatKind.Human, "Rasmus"), (SeatKind.Ai, "Astrid"), (SeatKind.Ai, "Bjorn"),
                (SeatKind.Ai, "Cecilie"), (SeatKind.Ai, "Dag"));
            hud.HotSeatClockFrozenForTests = true;
            hud.StartHotSeat(config);
            var s = hud.HotSeatSessionForTests;
            var state = s.State;
            var match = s.Match;

            // 1) An AI offers Rasmus a trade: the AI waits, Rasmus sees "INCOMING OFFER" with Accept / Decline / Counter.
            Assert.IsTrue(Drive(s, () => state.Round >= 1 && state.ActiveTrade != null && state.Seats[state.ActiveTrade.From].IsAi
                                         && state.ActiveTrade.Responses[0].Kind == TradeResponseKind.Pending),
                "an AI makes an offer within a few rounds");
            int offerer = state.ActiveTrade.From;
            Assert.IsTrue(MatchAi.AwaitingHumanAnswer(match));
            Assert.IsFalse(MatchRunner.HasAiWork(match), "the AI waits for the human");
            yield return DismissReveals(hud);
            StringAssert.Contains("YOUR HAND", Find(hud, "Hand/Title").GetComponent<Text>().text,
                "the only human sees their own cards while answering");
            StringAssert.Contains("Rasmus", Find(hud, "Hand/Title").GetComponent<Text>().text);
            var board = Find(hud, "TradeBoard");
            Assert.IsNotNull(board, "the offer is on the table");
            StringAssert.Contains("INCOMING OFFER", board.Find("Kicker").GetComponent<Text>().text);
            StringAssert.Contains("RASMUS", board.Find("Kicker").GetComponent<Text>().text);
            Assert.IsNotNull(FindButton(hud.HotSeatRootForTests, "Decline_0"));
            Assert.IsNotNull(FindButton(hud.HotSeatRootForTests, "Counter_0"));
            MaybeWrite(hud, "v142-ai-offer.png");
            int logged = match.CommandLog.Count;
            yield return Click(hud, "Decline_0");
            Assert.IsTrue(match.CommandLog.Skip(logged).Any(c => c.Seat == 0 && c.Type == MatchCommandType.RespondTrade));
            Assert.IsTrue(match.CommandLog.Skip(logged).Any(c => c.Seat == offerer
                                                               && (c.Type == MatchCommandType.ConfirmTrade || c.Type == MatchCommandType.CancelTrade)),
                "once Rasmus answered, the AI closes the deal or withdraws");

            // 2) Early on (nobody runs away yet), Rasmus offers; every AI answers at once — accept, decline or counter.
            Assert.IsTrue(Drive(s, () => state.CurrentSeat == 0 && state.Phase == MatchPhase.Main && state.ActiveTrade == null));
            state.Seats[0].Hand = new ResourceBundle { Wood = 3, Brick = 3, Wheat = 1, Sheep = 2, Stone = 0 };
            foreach (var seat in state.Seats.Where(x => x.Index != 0))
                seat.Hand = new ResourceBundle { Wood = 1, Brick = 0, Wheat = 2, Sheep = 1, Stone = 2 };
            Assert.IsTrue(s.Apply(MatchCommand.Propose(0, new ResourceBundle { Wood = 1, Brick = 1 },
                new ResourceBundle { Stone = 1 })).Ok);
            var trade = state.ActiveTrade;
            Assert.IsNotNull(trade);
            for (int i = 1; i < 5; i++)
                Assert.AreNotEqual(TradeResponseKind.Pending, trade.Responses[i].Kind, $"seat {i} answered");
            Assert.IsTrue(Enumerable.Range(1, 4).Any(i => trade.Responses[i].Kind != TradeResponseKind.Declined),
                "two cards for one Ore: someone bites (or counters)");
            hud.SetHotSeatTradeViewerForTests(0);
            yield return DismissReveals(hud);
            StringAssert.Contains("YOUR OFFER", Find(hud, "TradeBoard/Kicker").GetComponent<Text>().text);
            MaybeWrite(hud, "v142-ai-answers.png");
            yield return Click(hud, "CancelOffer");
            Assert.IsNull(state.ActiveTrade);

            // 3) Midgame: a 5-player board a few rounds in (5-player games last ~7–8 rounds).
            Assert.IsTrue(Drive(s, () => state.Round >= 5 && state.CurrentSeat == 0 && state.Phase == MatchPhase.Main
                                         && state.ActiveTrade == null));
            yield return DismissReveals(hud);
            MaybeWrite(hud, "v142-midgame-5p.png");

            hud.ExitHotSeat();
            yield return Settle(hud);
        }

        [Timeout(300000)]
        [UnityTest]
        public IEnumerator V142_SabotageSpots_ShowResourceIconAndNumber()
        {
            yield return LoadScene();
            var gameManager = Object.FindFirstObjectByType<GameManager>();
            RunHudController hud = null;
            yield return ReadyHud(gameManager, h => hud = h);

            var config = PartyConfig(1412, (SeatKind.Human, "Rasmus"), (SeatKind.Human, "Freja"), (SeatKind.Ai, "AI 3"));
            hud.HotSeatClockFrozenForTests = true;
            hud.StartHotSeat(config);
            var s = hud.HotSeatSessionForTests;
            var state = s.State;
            var match = s.Match;
            Assert.IsTrue(Drive(s, () => state.CurrentSeat == 0 && state.Phase == MatchPhase.Main && state.Round >= 1
                                         && state.ActiveTrade == null && match.BlightTiles(1).Count() >= 2));
            ClearShenanigans(state);
            state.Seats[0].Hand = new ResourceBundle { Wood = 2, Brick = 1, Wheat = 2, Sheep = 2, Stone = 2 };
            hud.OpenHotSeatSchemeForTests(RunHudController.HotSeatScheme.Sabotage);
            yield return Settle(hud);
            yield return Click(hud, "BuySabotage");
            state.Seats[0].Sabotage[0] = MatchSabotageKind.Blight;
            yield return Settle(hud);
            yield return Click(hud, "Target_1");
            var hexes = match.BlightTiles(1).ToList();
            for (int i = 0; i < System.Math.Min(6, hexes.Count); i++)
            {
                var spot = FindButton(hud.HotSeatRootForTests, "Spot_" + i).transform;
                var icon = spot.Find("SpotIcon_0");
                Assert.IsNotNull(icon, "a resource icon, not a word");
                Assert.IsNotNull(icon.GetComponent<Image>().sprite);
                var token = spot.Find("SpotToken_0/N");
                Assert.IsNotNull(token, "a number token");
                Assert.IsTrue(state.Board.TryGetTile(hexes[i], out var tile));
                Assert.AreEqual(tile.NumberToken.ToString(), token.GetComponent<Text>().text);
                foreach (var t in spot.GetComponentsInChildren<Text>())
                    StringAssert.DoesNotContain("Lumber", t.text);
            }

            yield return Click(hud, "Spot_1");
            MaybeWrite(hud, "v142-sabotage-icons.png");
            hud.ExitHotSeat();
            yield return Settle(hud);
        }
    }
}
