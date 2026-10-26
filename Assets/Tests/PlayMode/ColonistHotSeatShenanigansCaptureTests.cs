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
    /// v141 Phase 4 shenanigans on the pass-and-play table: the secret ballot (a private screen per voter, the
    /// totals revealed afterwards), the Night Market (sealed bids, then the result), bounties, sabotage cards,
    /// the saboteur guess and the reveal. Set COLONIST_HOTSEAT_SHOT_DIR to write review captures.
    /// </summary>
    public class ColonistHotSeatShenanigansCaptureTests
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
            Debug.Log($"v141 hot-seat capture written: {path}");
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

        private static MatchConfig PartyConfig(int seed, params (SeatKind kind, string name)[] seats)
        {
            var config = new MatchConfig { Seed = seed, MapSize = MatchConfig.RecommendedMap(seats.Length), PartyRules = true };
            foreach (var (kind, name) in seats)
                config.Seats.Add(new SeatConfig(kind, name));
            return config;
        }

        /// <summary>Drives human seats with the suggestion policy until <paramref name="stop"/> holds on a human's visible turn.</summary>
        private static bool Drive(HotSeatSession s, System.Func<bool> stop, int guardMax = 20000)
        {
            int guard = 0;
            while (guard++ < guardMax && !s.IsOver)
            {
                s.Tick(0f);
                if (s.IsOver)
                    return false;
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
        public IEnumerator V141_SecretBallot_PrivateScreens_ThenReveal()
        {
            yield return LoadScene();
            var gameManager = Object.FindFirstObjectByType<GameManager>();
            RunHudController hud = null;
            yield return ReadyHud(gameManager, h => hud = h);

            var config = PartyConfig(1411, (SeatKind.Human, "Rasmus"), (SeatKind.Human, "Freja"), (SeatKind.Ai, "AI 3"),
                (SeatKind.Human, "Jonas"));
            hud.HotSeatClockFrozenForTests = true;
            hud.StartHotSeat(config);
            var s = hud.HotSeatSessionForTests;
            var state = s.State;
            Assert.IsTrue(Drive(s, () => state.CurrentSeat == 3 && state.Phase == MatchPhase.Main && state.Round >= 1
                                         && state.ActiveTrade == null));
            ClearShenanigans(state);
            state.EventDeck.Add(EventId.Decree);
            Assert.IsTrue(s.EndTurn().Ok);
            Assert.AreEqual(MatchPhase.Vote, state.Phase);
            Assert.AreEqual(0, state.ActingSeat, "the first voter");
            Assert.IsTrue(s.PassScreenPending, "each voter gets a private screen");
            yield return Settle(hud);
            StringAssert.Contains("vote in secret", Find(hud, "PassScreen/PassCard/ConfirmPass").GetComponentInChildren<Text>().text);
            s.ConfirmPass();
            yield return Settle(hud);
            Assert.IsNotNull(Find(hud, "Ballot"));
            yield return Click(hud, "VoteAye");

            Assert.AreEqual(1, state.ActingSeat);
            Assert.IsTrue(s.PassScreenPending, "Rasmus's vote is hidden before Freja looks");
            Assert.IsNull(Find(hud, "Ballot"));
            s.ConfirmPass();
            yield return Settle(hud);
            var ballot = Find(hud, "Ballot");
            Assert.IsNotNull(ballot);
            StringAssert.Contains("FREJA", ballot.Find("Kicker").GetComponent<Text>().text);
            StringAssert.Contains("sealed", ballot.Find("VoterCap_0").GetComponent<Text>().text, "Rasmus has voted");
            StringAssert.Contains("waiting", ballot.Find("VoterCap_3").GetComponent<Text>().text);
            Assert.IsNotNull(ballot.Find("Voter_1/Face"), "voters show their leader portraits");
            MaybeWrite(hud, "v141-vote.png");
            yield return Click(hud, "VoteNay");

            // The AI votes at once; then Jonas, privately.
            Assert.AreEqual(3, state.ActingSeat);
            Assert.IsTrue(s.PassScreenPending);
            s.ConfirmPass();
            yield return Settle(hud);
            yield return Click(hud, "VoteAye");

            Assert.AreEqual(MatchPhase.Roll, state.Phase);
            var result = state.LastBallot;
            Assert.IsNotNull(result);
            Assert.AreEqual(4, result.Yes + result.No);
            if (s.PassScreenPending)
                s.ConfirmPass();
            yield return Settle(hud);
            var reveal = Find(hud, "BallotReveal");
            Assert.IsNotNull(reveal, "the totals are revealed to the table");
            Assert.IsTrue(hud.HotSeatRevealShowingForTests);
            Assert.AreEqual(result.Yes.ToString(), reveal.Find("Ayes").GetComponent<Text>().text);
            Assert.AreEqual(result.No.ToString(), reveal.Find("Nays").GetComponent<Text>().text);
            StringAssert.Contains(result.Passed ? "passes" : "fails", reveal.Find("Title").GetComponent<Text>().text);
            MaybeWrite(hud, "v141-reveal.png");
            yield return Click(hud, "RevealContinue");
            Assert.IsNull(Find(hud, "BallotReveal"));
            Assert.IsFalse(hud.HotSeatRevealShowingForTests);
            StringAssert.Contains(result.Passed ? "passed" : "failed", Find(hud, "EventCard/Desc").GetComponent<Text>().text);
            hud.ExitHotSeat();
            yield return Settle(hud);
        }

        [Timeout(300000)]
        [UnityTest]
        public IEnumerator V141_Bounty_Sabotage_Guess_And_NightMarket()
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
                                         && state.ActiveTrade == null && match.BlightTiles(1).Any()));
            ClearShenanigans(state);
            state.Seats[0].Hand = new ResourceBundle { Wood = 2, Brick = 1, Wheat = 2, Sheep = 2, Stone = 2 };
            state.Seats[1].Hand = new ResourceBundle { Wood = 1, Wheat = 1, Sheep = 2 };
            state.Seats[2].Hand = new ResourceBundle { Brick = 2, Stone = 1 };
            yield return Settle(hud);

            // Bounty: pick the mark (leader portraits), put up 2 cards.
            Assert.AreEqual("Schemes", FindButton(hud.HotSeatRootForTests, "Action_2").GetComponentInChildren<Text>().text,
                "a Schemes button on the action bar");
            yield return Click(hud, "Action_2");
            Assert.AreEqual(RunHudController.HotSeatScheme.Bounty, hud.HotSeatSchemeForTests);
            Assert.IsNull(Find(hud, "DevCards"), "the scheme panel takes the dev-card space");
            yield return Click(hud, "Mark_1");
            yield return Click(hud, "BountyMore_Sheep");
            yield return Click(hud, "BountyMore_Wood");
            StringAssert.Contains("Post 2 on Freja", FindButton(hud.HotSeatRootForTests, "PostBounty").GetComponentInChildren<Text>().text);
            MaybeWrite(hud, "v141-bounty.png");
            yield return Click(hud, "PostBounty");
            Assert.AreEqual(1, state.Bounties.Count);
            Assert.AreEqual(1, state.Bounties[0].Target);
            Assert.AreEqual(2, state.Bounties[0].Reward.Total);
            Assert.IsNotNull(Find(hud, "SeatStrip/Seat_1/Bounty"), "the strip marks the bounty holder");

            // Sabotage: buy one with the button, then play a Blight face-down on Freja.
            hud.OpenHotSeatSchemeForTests(RunHudController.HotSeatScheme.Sabotage);
            yield return Settle(hud);
            yield return Click(hud, "BuySabotage");
            Assert.AreEqual(1, state.Seats[0].Sabotage.Count);
            state.Seats[0].Sabotage[0] = MatchSabotageKind.Blight; // the deck is shuffled: pin the card for the capture
            yield return Settle(hud);
            yield return Click(hud, "Target_1");
            yield return Click(hud, "Spot_0");
            StringAssert.Contains("face-down", FindButton(hud.HotSeatRootForTests, "PlaySabotage").GetComponentInChildren<Text>().text);
            MaybeWrite(hud, "v141-sabotage-play.png");
            yield return Click(hud, "PlaySabotage");
            var pending = match.PendingSabotageOn(1);
            Assert.IsNotNull(pending);
            Assert.AreEqual(MatchSabotageKind.Blight, pending.Kind);
            Assert.IsNotNull(Find(hud, "SeatStrip/Seat_1/FaceDown"), "a face-down card on Freja's row");
            StringAssert.DoesNotContain("Rasmus", state.Log[state.Log.Count - 1], "the play is anonymous");

            // Freja's turn: she may guess before rolling.
            Assert.IsTrue(s.EndTurn().Ok);
            Assert.AreEqual(1, state.CurrentSeat);
            Assert.IsTrue(s.PassScreenPending);
            s.ConfirmPass();
            yield return Settle(hud);
            Assert.IsNotNull(Find(hud, "GuessPanel"), "Freja can name the saboteur");
            MaybeWrite(hud, "v141-guess.png");
            yield return Click(hud, "GuessLater");
            Assert.IsNull(Find(hud, "GuessPanel"));
            Assert.IsTrue(s.Roll().Ok);
            Assert.IsNull(match.PendingSabotageOn(1), "the Blight bit at dawn");
            yield return Settle(hud);
            var sabReveal = Find(hud, "SabotageReveal");
            Assert.IsNotNull(sabReveal, "the card is revealed when it bites");
            StringAssert.Contains("Rasmus", sabReveal.Find("Line").GetComponent<Text>().text);
            MaybeWrite(hud, "v141-sabotage-reveal.png");
            yield return Click(hud, "SabotageContinue");

            // Night Market at the next night: sealed bids on private screens, then the result.
            state.EventDeck.Add(EventId.NightMarket);
            Assert.IsTrue(Drive(s, () => state.Phase == MatchPhase.Auction && state.ActingSeat == 0));
            ClearShenanigans(state);
            state.Seats[0].Hand = new ResourceBundle { Wood = 2, Brick = 1, Wheat = 1, Stone = 1 };
            state.Seats[1].Hand = new ResourceBundle { Sheep = 2, Wheat = 1 };
            state.Seats[2].Hand = new ResourceBundle { Brick = 1 };
            yield return Settle(hud);
            Assert.IsNotNull(Find(hud, "NightMarket"));
            yield return Click(hud, "BidMore_Wood");
            yield return Click(hud, "BidMore_Wood");
            yield return Click(hud, "BidMore_Stone");
            StringAssert.Contains("3 cards", FindButton(hud.HotSeatRootForTests, "SealBid").GetComponentInChildren<Text>().text);
            MaybeWrite(hud, "v141-auction-bid.png");
            yield return Click(hud, "SealBid");
            Assert.AreEqual(1, state.ActingSeat);
            Assert.IsTrue(s.PassScreenPending, "Rasmus's bid is hidden before Freja bids");
            s.ConfirmPass();
            yield return Settle(hud);
            yield return Click(hud, "PassBid"); // v142: one Wool is below every lot's minimum

            Assert.AreEqual(MatchPhase.Roll, state.Phase, "the AI bid at once and the market closed");
            var auction = state.LastAuction;
            Assert.IsNotNull(auction);
            Assert.AreEqual(0, auction.Winner, "Rasmus bid the most");
            if (s.PassScreenPending)
                s.ConfirmPass();
            yield return Settle(hud);
            var res = Find(hud, "AuctionResult");
            Assert.IsNotNull(res, "the result is shown to the table");
            Assert.IsNotNull(res.Find("Winner/Face"), "the winner's leader portrait");
            StringAssert.Contains("Rasmus wins", res.Find("Title").GetComponent<Text>().text);
            MaybeWrite(hud, "v141-auction-result.png");
            yield return Click(hud, "AuctionContinue");
            Assert.IsNull(Find(hud, "AuctionResult"));
            hud.ExitHotSeat();
            yield return Settle(hud);
        }
    }
}
