using System.Collections;
using System.IO;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Civilizations;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
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
    /// v136 step 4: pass-and-play with civilizations (secret pick before placement), secret quests + traits
    /// (after snake placement, last placer first), the match event deck, the robber victim picker and the
    /// game-over quest reveal. Set COLONIST_HOTSEAT_SHOT_DIR to write review captures.
    /// </summary>
    public class ColonistHotSeatPartyCaptureTests
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
            Debug.Log($"v136 hot-seat capture written: {path}");
        }

        private static Button FindButton(Transform root, string name)
        {
            var b = root.GetComponentsInChildren<Button>(true).FirstOrDefault(x => x.name == name);
            Assert.IsNotNull(b, "button " + name);
            return b;
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

        /// <summary>The offered quest the seat is closest to (progress / target), as a sensible player would pick.</summary>
        private static int BestQuest(HotSeatSession s, int seat)
        {
            var offer = s.State.Seats[seat].QuestOffer;
            int best = 0;
            double bestValue = -1;
            for (int i = 0; i < offer.Count; i++)
            {
                var q = MatchPartyLibrary.Quest(offer[i]);
                double v = q.CurrentProgress(s.Match, seat) / (double)q.Target;
                if (v > bestValue + 1e-9)
                {
                    bestValue = v;
                    best = i;
                }
            }

            return best;
        }

        [Timeout(300000)]
        [UnityTest]
        public IEnumerator FiveSeatParty_CivPick_QuestPick_Event_GameOverReveal()
        {
            yield return LoadScene();
            var gameManager = Object.FindFirstObjectByType<GameManager>();
            RunHudController hud = null;
            yield return ReadyHud(gameManager, h => hud = h);

            // The lobby turns party rules on by default.
            hud.OpenHotSeatLobbyForTests();
            Assert.IsTrue(hud.LobbyPartyRules);
            Assert.IsTrue(hud.BuildLobbyConfig(1).PartyRules);

            // v137: seed 1 ends with Freja winning on her secret quest (found with the Core harness, same driving policy).
            var config = PartyConfig(1, (SeatKind.Human, "Rasmus"), (SeatKind.Ai, "AI 2"), (SeatKind.Human, "Freja"),
                (SeatKind.Ai, "AI 4"), (SeatKind.Ai, "AI 5"));
            hud.HotSeatClockFrozenForTests = true;
            hud.StartHotSeat(config);
            var s = hud.HotSeatSessionForTests;
            var state = s.State;
            Assert.AreEqual(MatchPhase.CivPick, state.Phase);
            Assert.AreEqual(0, state.ActingSeat);
            Assert.IsTrue(s.PassScreenPending, "the civ offer is behind Rasmus's pass screen");
            yield return Settle(hud);
            Assert.IsNull(hud.HotSeatRootForTests.Find("CivPick"), "offer hidden on the pass screen");

            // Rasmus: 3 civs with leader portraits.
            FindButton(hud.HotSeatRootForTests, "ConfirmPass").onClick.Invoke();
            yield return Settle(hud);
            var pick = hud.HotSeatRootForTests.Find("CivPick");
            Assert.IsNotNull(pick);
            for (int i = 0; i < 3; i++)
            {
                var portrait = pick.Find($"Civ_{i}/Portrait").GetComponent<Image>();
                Assert.IsNotNull(portrait.sprite, "leader portrait " + i);
            }

            var rasmusOffer = state.Seats[0].CivOffer.ToList();
            MaybeWrite(hud, "civ-pick-rasmus.png");
            FindButton(pick, "PickCiv_0").onClick.Invoke();
            Assert.AreEqual(rasmusOffer[0], state.Seats[0].Civilization);
            yield return Settle(hud);

            // AI 2 picked at once; Freja is next behind her own pass screen.
            Assert.AreEqual(2, state.ActingSeat);
            Assert.IsTrue(s.PassScreenPending);
            Assert.IsNotNull(hud.HotSeatRootForTests.Find("PassScreen"));
            FindButton(hud.HotSeatRootForTests, "ConfirmPass").onClick.Invoke();
            yield return Settle(hud);
            var frejaOffer = state.Seats[2].CivOffer.ToList();
            Assert.IsFalse(frejaOffer.Intersect(rasmusOffer.Take(1)).Any(), "Rasmus's civ is gone from the deck");
            for (int i = 0; i < 5; i++)
                Assert.AreSame(RunHudController.NeutralLeaderSpriteForTests, hud.HotSeatStripPortraitForTests(i),
                    "neutral silhouette until every civ is revealed");
            MaybeWrite(hud, "civ-pick-freja.png");
            FindButton(hud.HotSeatRootForTests, "PickCiv_1").onClick.Invoke();
            yield return Settle(hud);
            Assert.AreEqual(5, state.Seats.Count(x => x.Civilization != CivilizationId.None));
            Assert.AreEqual(5, state.Seats.Select(x => x.Civilization).Distinct().Count(), "unique per match");

            // Snake placement, then the quest pick: AI 5 and AI 4 first, then Freja.
            Assert.IsTrue(Drive(s, () => state.Phase == MatchPhase.QuestPick));
            Assert.AreEqual(2, state.ActingSeat, "last placer first: seats 4 and 3 (AI) already picked");
            Assert.AreNotEqual(MatchQuestId.None, state.Seats[4].Quest);
            Assert.AreNotEqual(MatchQuestId.None, state.Seats[3].Quest);
            yield return Settle(hud);
            var root = hud.HotSeatRootForTests;
            for (int i = 0; i < 5; i++)
            {
                var sprite = hud.HotSeatStripPortraitForTests(i);
                Assert.AreSame(ColonistRoguelikeArt.GetCivilizationPortrait(state.Seats[i].Civilization), sprite,
                    "seat " + i + " shows its own leader");
                Assert.IsTrue(ColonistRoguelikeArt.HasCivilizationPortrait(state.Seats[i].Civilization),
                    "a painted leader for " + state.Seats[i].Civilization);
            }

            Assert.AreEqual(5, Enumerable.Range(0, 5).Select(i => hud.HotSeatStripPortraitForTests(i)).Distinct().Count(),
                "every seat has a different portrait");
            Assert.IsNotNull(root.Find("QuestPick"));
            var offer = state.Seats[2].QuestOffer.ToList();
            Assert.AreEqual(3, offer.Count);
            var points = offer.Select(q => MatchPartyLibrary.Quest(q).Points).ToList();
            Assert.IsTrue(points.Contains(1) && points.Contains(2), "mix of easier 1 VP and harder 2 VP");
            Assert.IsFalse(FindButton(root, "QuestConfirm").interactable);
            FindButton(root, "Quest_" + BestQuest(s, 2)).onClick.Invoke();
            FindButton(root, "Trait_0").onClick.Invoke();
            yield return Settle(hud);
            root = hud.HotSeatRootForTests;
            Assert.IsTrue(FindButton(root, "QuestConfirm").interactable);
            MaybeWrite(hud, "quest-pick.png");
            int frejaQuest = BestQuest(s, 2);
            FindButton(root, "QuestConfirm").onClick.Invoke();
            Assert.AreEqual(offer[frejaQuest], state.Seats[2].Quest);
            yield return Settle(hud);

            // Rasmus picks last.
            Assert.IsTrue(Drive(s, () => state.Phase == MatchPhase.QuestPick && state.ActingSeat == 0, 50));
            yield return Settle(hud);
            FindButton(hud.HotSeatRootForTests, "Quest_" + BestQuest(s, 0)).onClick.Invoke();
            FindButton(hud.HotSeatRootForTests, "Trait_1").onClick.Invoke();
            yield return Settle(hud);
            FindButton(hud.HotSeatRootForTests, "QuestConfirm").onClick.Invoke();
            Assert.AreEqual(MatchPhase.Roll, state.Phase);
            Assert.IsTrue(state.Seats.All(x => x.Quest != MatchQuestId.None && x.Trait != MatchTraitId.None));

            // v140: pass-and-play pieces are larger than single-player so they read on the 30-hex table (city 1.5x).
            var view = Object.FindFirstObjectByType<BoardView>();
            float hexW = PlayerPieceVisuals.FlatHexWidth(view.HexScale) * view.transform.lossyScale.x;
            var settlement = view.GetComponentsInChildren<Transform>().First(t => t.name == PlayerPieceVisuals.SettlementName);
            var sb = settlement.GetComponentsInChildren<MeshFilter>()
                .Select(f => f.sharedMesh.bounds.size.x * f.transform.lossyScale.x).Max();
            Debug.Log($"v136 piece scale: settlement mesh width {sb:F3} / hex width {hexW:F3} = {sb / hexW:F3}");
            Assert.AreEqual(PlayerPieceVisuals.SettlementFootprintOfHex * RunHudController.HotSeatBuildingScale, sb / hexW, 0.015f);

            // An event in the 5-player match, on a human turn.
            Assert.IsTrue(Drive(s, () => state.ActiveEvent != Core.Events.EventId.None && state.Phase == MatchPhase.Main
                                         && state.ActiveTrade == null));
            yield return Settle(hud);
            root = hud.HotSeatRootForTests;
            Assert.IsNotNull(root.Find("EventCard"), "event card shown");
            // v138: no always-visible civ/quest panel — the portraits in the strip open a popup instead.
            Assert.IsNull(root.Find("PartyInfo"), "no always-on civ panel");
            Assert.IsNull(hud.HotSeatInfoPopupForTests, "popup closed by default");
            MaybeWrite(hud, "hud-no-left-panel.png");
            int me = hud.HotSeatViewerSeat;
            Assert.GreaterOrEqual(me, 0, "a human seat holds the device");
            int other = Enumerable.Range(0, 5).First(i => i != me);
            hud.ClickHotSeatPortraitForTests(me);
            yield return Settle(hud);
            var popup = hud.HotSeatInfoPopupForTests;
            Assert.IsNotNull(popup, "own portrait opens the popup");
            var myQuest = MatchPartyLibrary.Quest(state.Seats[me].Quest).Name;
            Assert.IsTrue(popup.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains(myQuest)), "own quest shown");
            Assert.IsTrue(popup.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains("SECRET QUEST")));
            MaybeWrite(hud, "popup-own.png");
            hud.ClickHotSeatPortraitForTests(me);
            yield return Settle(hud);
            Assert.IsNull(hud.HotSeatInfoPopupForTests, "second click on the same portrait closes");

            hud.ClickHotSeatPortraitForTests(other);
            yield return Settle(hud);
            popup = hud.HotSeatInfoPopupForTests;
            Assert.IsNotNull(popup, "another seat's portrait opens its popup");
            Assert.AreEqual(other, hud.HotSeatInfoSeatForTests);
            var otherQuest = MatchPartyLibrary.Quest(state.Seats[other].Quest).Name;
            var otherTrait = MatchPartyLibrary.Trait(state.Seats[other].Trait).Name;
            foreach (var t in popup.GetComponentsInChildren<Text>(true))
            {
                StringAssert.DoesNotContain("SECRET QUEST", t.text, "no quest section for other seats");
                StringAssert.DoesNotContain(otherQuest, t.text, "another seat's quest never leaks");
            }

            // v146: traits are public now; only the quest stays secret.
            Assert.IsTrue(popup.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains(otherTrait.ToUpperInvariant())),
                "another seat's trait is visible");
            Assert.IsTrue(popup.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains("hidden until the game ends")));
            MaybeWrite(hud, "popup-other.png");
            hud.ClickOutsideHotSeatInfoForTests();
            yield return Settle(hud);
            Assert.IsNull(hud.HotSeatInfoPopupForTests, "outside click closes");
            hud.ClickHotSeatPortraitForTests(other);
            yield return Settle(hud);
            FindButton(hud.HotSeatRootForTests, "CivInfoClose").onClick.Invoke();
            yield return Settle(hud);
            Assert.IsNull(hud.HotSeatInfoPopupForTests, "✕ closes");
            root = hud.HotSeatRootForTests;
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(state.Seats[i].PublicVictoryPoints.ToString(),
                    root.Find($"SeatStrip/Seat_{i}/VP").GetComponent<Text>().text, "strip never shows quest VP");
            Debug.Log($"v136 event capture: {state.ActiveEvent} round {state.Round + 1}, seat {state.ActingSeat}");
            MaybeWrite(hud, "event-5p.png");

            // Play to the end: the game-over card reveals every quest.
            Drive(s, () => false, 60000);
            Assert.IsTrue(s.IsOver, "match finished");
            yield return Settle(hud);
            root = hud.HotSeatRootForTests;
            Assert.IsNotNull(root.Find("GameOver/GameOverCard/Reveal"));
            int ticks = 0;
            for (int i = 0; i < 5; i++)
            {
                var line = root.Find($"GameOver/GameOverCard/Quest_{i}").GetComponent<Text>().text;
                StringAssert.StartsWith("Quest: ", line);
                if (line.Contains("✓"))
                    ticks++;
            }

            Assert.AreEqual(state.Seats.Count(x => x.QuestDone), ticks, "one ✓ per completed quest");
            Assert.GreaterOrEqual(ticks, 1, "the reveal shows at least one completed quest");

            Debug.Log("v136 reveal: " + string.Join(" | ", state.Seats.Select(x =>
                $"{x.Name} {x.Civilization} {x.Quest} done={x.QuestDone} vp={x.VictoryPoints} public={x.PublicVictoryPoints}")));
            MaybeWrite(hud, "gameover-reveal.png");
            hud.ExitHotSeat();
            yield return Settle(hud);
        }

        [Timeout(240000)]
        [UnityTest]
        public IEnumerator Robber_TwoOpponentsOnTheTile_OpensVictimPicker()
        {
            yield return LoadScene();
            var gameManager = Object.FindFirstObjectByType<GameManager>();
            RunHudController hud = null;
            yield return ReadyHud(gameManager, h => hud = h);

            var config = PartyConfig(1362, (SeatKind.Human, "Rasmus"), (SeatKind.Human, "Freja"), (SeatKind.Ai, "AI 3"),
                (SeatKind.Ai, "AI 4"));
            hud.HotSeatClockFrozenForTests = true;
            hud.StartHotSeat(config);
            var s = hud.HotSeatSessionForTests;
            var state = s.State;

            HexCoord target = default;
            bool found = false;
            System.Func<bool> robberWithTwo = () =>
            {
                if (state.Phase != MatchPhase.MoveRobber)
                    return false;
                int seat = state.ActingSeat;
                foreach (var hex in state.Board.Tiles.Keys)
                {
                    if (state.Board.RobberTile == hex)
                        continue;
                    if (s.Match.RobberVictims(seat, hex).Count() >= 2)
                    {
                        target = hex;
                        found = true;
                        return true;
                    }
                }

                return false;
            };
            // Keep opponents holding cards so they can be robbed.
            int guard = 0;
            while (!found && guard++ < 200 && !s.IsOver)
            {
                if (Drive(s, robberWithTwo, 400))
                    break;
                if (!state.IsPicking && !s.Match.IsSetup)
                    foreach (var seat in state.Seats.Where(x => x.CardCount == 0))
                        seat.Hand = new ResourceBundle { Wood = 1, Sheep = 1 };
            }

            Assert.IsTrue(found, "a human robber move with two opponents on one tile");
            int robber = state.ActingSeat;
            var victims = s.Match.RobberVictims(robber, target).ToList();
            yield return Settle(hud);
            var hint = hud.HotSeatRootForTests.Find("BoardHint")?.GetComponent<Text>();
            Assert.IsNotNull(hint);
            StringAssert.Contains("pick whom to rob", hint.text);

            hud.ClickHotSeatRobberHexForTests(target);
            Assert.AreEqual(MatchPhase.MoveRobber, state.Phase, "nothing applied yet");
            Assert.IsTrue(hud.HotSeatVictimPickerOpenForTests);
            yield return Settle(hud);
            var picker = hud.HotSeatRootForTests.Find("VictimPicker");
            Assert.IsNotNull(picker);
            foreach (int v in victims)
            {
                Assert.IsNotNull(picker.Find("Rob_" + v), "button for seat " + v);
                // v140: leader portraits, as on the trade screens.
                var face = picker.Find("VictimDisc_" + v + "/Face")?.GetComponent<Image>();
                Assert.IsNotNull(face, "leader portrait for seat " + v);
                Assert.AreSame(hud.HotSeatStripPortraitForTests(v), face.sprite);
            }

            MaybeWrite(hud, "victim-picker.png");

            int chosen = victims[victims.Count - 1];
            int before = state.Seats[chosen].CardCount;
            FindButton(picker, "Rob_" + chosen).onClick.Invoke();
            Assert.AreEqual(target, state.Board.RobberTile);
            Assert.AreEqual(before - 1, state.Seats[chosen].CardCount, "robbed the picked seat");
            Assert.IsFalse(hud.HotSeatVictimPickerOpenForTests);
            yield return Settle(hud);

            // The robber pawn stays clear of the tile's number token.
            var view = Object.FindFirstObjectByType<BoardView>();
            var pawn = view.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "RobberPawn" && t.gameObject.activeInHierarchy);
            Assert.IsNotNull(pawn);
            var chip = pawn.parent.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "NumberTokenChip");
            if (chip != null)
            {
                var pb = Bounds(pawn);
                var cb = Bounds(chip);
                float dist = Vector2.Distance(new Vector2(pb.center.x, pb.center.z), new Vector2(cb.center.x, cb.center.z));
                float rp = Mathf.Max(pb.extents.x, pb.extents.z);
                float rc = Mathf.Max(cb.extents.x, cb.extents.z);
                Debug.Log($"v136 robber clearance: dist {dist:F3} pawn r {rp:F3} chip r {rc:F3}");
                Assert.GreaterOrEqual(dist, rp + rc - 0.02f, "robber does not cover the number token");
            }

            MaybeWrite(hud, "robber-moved.png");
            hud.ExitHotSeat();
            yield return Settle(hud);
        }

        [Timeout(240000)]
        [UnityTest]
        public IEnumerator Trade_LeaderPortraits_SwappedRows_AndWildcardPicker()
        {
            yield return LoadScene();
            var gameManager = Object.FindFirstObjectByType<GameManager>();
            RunHudController hud = null;
            yield return ReadyHud(gameManager, h => hud = h);

            var config = PartyConfig(1362, (SeatKind.Human, "Rasmus"), (SeatKind.Human, "Freja"), (SeatKind.Ai, "AI 3"),
                (SeatKind.Ai, "AI 4"));
            hud.HotSeatClockFrozenForTests = true;
            hud.StartHotSeat(config);
            var s = hud.HotSeatSessionForTests;
            var state = s.State;
            Assert.IsTrue(Drive(s, () => state.CurrentSeat == 0 && state.Phase == MatchPhase.Main && state.ActiveTrade == null),
                "Rasmus's main phase");
            state.Seats[0].Hand = new ResourceBundle { Wood = 1, Brick = 2, Wheat = 2, Sheep = 1 };
            state.Seats[1].Hand = new ResourceBundle { Wheat = 1, Stone = 2 };
            state.Seats[2].Hand = new ResourceBundle { Sheep = 2, Stone = 1 };
            state.Seats[3].Hand = new ResourceBundle { Wood = 1 };

            // Compose: leaders on the card, "you get" on top, "you give" below.
            hud.SetHotSeatTradeForTests(true, new ResourceBundle { Wood = 1 }, new ResourceBundle { Stone = 1 });
            yield return Settle(hud);
            var board = hud.HotSeatRootForTests.Find("TradeBoard");
            Assert.IsNotNull(board);
            Assert.Greater(board.Find("GetMore_Stone").position.y, board.Find("GiveMore_Wood").position.y, "get row on top");
            var me = board.Find("ComposeFrom/Face")?.GetComponent<Image>();
            Assert.IsNotNull(me, "own leader portrait");
            Assert.AreSame(hud.HotSeatStripPortraitForTests(0), me.sprite, "same leader as the seat strip");
            for (int j = 1; j < 4; j++)
                Assert.AreSame(hud.HotSeatStripPortraitForTests(j), board.Find($"ComposeTo_{j}/Face")?.GetComponent<Image>()?.sprite,
                    "leader of seat " + j);
            MaybeWrite(hud, "trade-compose-portraits.png");

            // Wildcard in the give row: "I want stone, you pick 1 of anything else I hold".
            hud.SetHotSeatTradeForTests(true, ResourceBundle.Zero, new ResourceBundle { Stone = 1 });
            yield return Settle(hud);
            FindButton(hud.HotSeatRootForTests, "GiveMore_Any").onClick.Invoke();
            yield return Settle(hud);
            Assert.IsFalse(FindButton(hud.HotSeatRootForTests, "GetMore_Any").interactable, "wildcards on one side only");
            Assert.IsTrue(FindButton(hud.HotSeatRootForTests, "ProposeTrade").interactable);
            MaybeWrite(hud, "trade-compose-wildcard.png");
            FindButton(hud.HotSeatRootForTests, "ProposeTrade").onClick.Invoke();
            Assert.IsNotNull(state.ActiveTrade);
            Assert.AreEqual(1, state.ActiveTrade.GiveAny);
            Assert.AreEqual(TradeResponseKind.Accepted, state.ActiveTrade.Responses[2].Kind, "AI 3 takes the wildcard");
            Assert.AreEqual(1, state.ActiveTrade.Responses[2].AnyChoice.Total);
            Assert.AreEqual(TradeResponseKind.Declined, state.ActiveTrade.Responses[3].Kind, "AI 4 has no stone");

            // Freja looks at the incoming offer: leaders for the offerer and every answer row.
            hud.SetHotSeatTradeViewerForTests(1);
            yield return Settle(hud);
            board = hud.HotSeatRootForTests.Find("TradeBoard");
            Assert.AreSame(hud.HotSeatStripPortraitForTests(0), board.Find("FromDisc/Face")?.GetComponent<Image>()?.sprite);
            for (int j = 1; j < 4; j++)
                Assert.AreSame(hud.HotSeatStripPortraitForTests(j), board.Find($"TradeDisc_{j}/Face")?.GetComponent<Image>()?.sprite);
            Assert.Greater(board.Find("HeadGetArrow").position.y, board.Find("HeadGiveArrow").position.y,
                "Freja's view: what she gets on top");
            Assert.IsNotNull(board.Find("HeadGetIcon_Any"), "she gets the wildcard");
            MaybeWrite(hud, "trade-incoming-portraits.png");

            FindButton(hud.HotSeatRootForTests, "Accept_1").onClick.Invoke();
            Assert.AreEqual(TradeResponseKind.Pending, state.ActiveTrade.Responses[1].Kind, "she picks first");
            Assert.IsTrue(hud.HotSeatWildPickerOpenForTests);
            yield return Settle(hud);
            var picker = hud.HotSeatRootForTests.Find("TradeBoard/WildPicker");
            Assert.IsNotNull(picker);
            Assert.IsFalse(FindButton(picker, "WildPickMore_Stone").interactable, "never what Rasmus asks for");
            Assert.IsFalse(FindButton(picker, "WildPickConfirm").interactable);
            FindButton(picker, "WildPickMore_Brick").onClick.Invoke();
            yield return Settle(hud);
            picker = hud.HotSeatRootForTests.Find("TradeBoard/WildPicker");
            Assert.IsTrue(FindButton(picker, "WildPickConfirm").interactable);
            Assert.IsFalse(FindButton(picker, "WildPickMore_Wheat").interactable, "exactly one card");
            MaybeWrite(hud, "trade-wildcard-picker.png");
            FindButton(picker, "WildPickConfirm").onClick.Invoke();
            Assert.AreEqual(TradeResponseKind.Accepted, state.ActiveTrade.Responses[1].Kind);
            Assert.AreEqual(1, state.ActiveTrade.Responses[1].AnyChoice.Brick);
            Assert.IsFalse(hud.HotSeatWildPickerOpenForTests);

            hud.SetHotSeatTradeViewerForTests(0);
            yield return Settle(hud);
            MaybeWrite(hud, "trade-wildcard-answers.png");
            FindButton(hud.HotSeatRootForTests, "Confirm_1").onClick.Invoke();
            Assert.IsNull(state.ActiveTrade, "traded");
            Assert.AreEqual(1, state.Seats[0].Hand.Stone);
            Assert.AreEqual(1, state.Seats[0].Hand.Brick);
            Assert.AreEqual(1, state.Seats[1].Hand.Brick);
            Assert.AreEqual(1, state.Seats[1].Hand.Stone);
            yield return Settle(hud);

            // Wildcard in the get row: Rasmus picks from what the partner can spare when confirming.
            hud.SetHotSeatTradeForTests(true, new ResourceBundle { Wheat = 1 }, ResourceBundle.Zero);
            hud.SetHotSeatTradeWildcardsForTests(0, 1);
            yield return Settle(hud);
            FindButton(hud.HotSeatRootForTests, "ProposeTrade").onClick.Invoke();
            Assert.AreEqual(1, state.ActiveTrade.GetAny);
            Assert.AreEqual(TradeResponseKind.Accepted, state.ActiveTrade.Responses[2].Kind);
            yield return Settle(hud);
            FindButton(hud.HotSeatRootForTests, "Confirm_2").onClick.Invoke();
            Assert.IsNotNull(state.ActiveTrade, "the pick comes before the swap");
            Assert.IsTrue(hud.HotSeatWildPickerOpenForTests);
            yield return Settle(hud);
            picker = hud.HotSeatRootForTests.Find("TradeBoard/WildPicker");
            Assert.IsFalse(FindButton(picker, "WildPickMore_Wood").interactable, "AI 3 has no wood");
            FindButton(picker, "WildPickMore_Sheep").onClick.Invoke();
            yield return Settle(hud);
            FindButton(hud.HotSeatRootForTests.Find("TradeBoard/WildPicker"), "WildPickConfirm").onClick.Invoke();
            Assert.IsNull(state.ActiveTrade);
            Assert.AreEqual(2, state.Seats[0].Hand.Sheep);
            Assert.AreEqual(1, state.Seats[0].Hand.Wheat);
            Assert.AreEqual(1, state.Seats[2].Hand.Wheat);
            yield return Settle(hud);
            hud.ExitHotSeat();
            yield return Settle(hud);
        }

        private static Rect ScreenRect(Transform t)
        {
            var c = new Vector3[4];
            ((RectTransform)t).GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        private static void AssertClearOfPanels(RunHudController hud, Transform card, string what)
        {
            var root = hud.HotSeatRootForTests;
            Assert.IsNull(root.Find("DevCards"), what + ": the dev-card panel steps aside while trading");
            var hand = root.Find("Hand");
            Assert.IsNotNull(hand);
            Assert.Greater(ScreenRect(card).yMin, ScreenRect(hand).yMax, what + " ends above the hand panel");
        }

        [Timeout(300000)]
        [UnityTest]
        public IEnumerator V140_FiveSeatMidgame_PiecesReadable()
        {
            yield return LoadScene();
            var gameManager = Object.FindFirstObjectByType<GameManager>();
            RunHudController hud = null;
            yield return ReadyHud(gameManager, h => hud = h);

            // v144 traits/quests: seed 1406 now ends in round 5 before a human Roll with 3 cities; 1408 reaches it.
            var config = PartyConfig(1408, (SeatKind.Human, "Rasmus"), (SeatKind.Ai, "AI 2"), (SeatKind.Human, "Freja"),
                (SeatKind.Ai, "AI 4"), (SeatKind.Ai, "AI 5"));
            hud.HotSeatClockFrozenForTests = true;
            hud.StartHotSeat(config);
            var s = hud.HotSeatSessionForTests;
            var state = s.State;
            Assert.AreEqual(RunHudController.HotSeatBuildingScale, PlayerPieceVisuals.BuildingScaleMultiplier);
            Assert.IsTrue(Drive(s, () => state.Round >= 3 && state.Phase == MatchPhase.Roll && state.CurrentSeat == 0
                                         && Enumerable.Range(0, state.Seats.Count).Sum(i => s.Match.CountCities(i)) >= 3), "a lively mid-game");
            yield return Settle(hud);

            var view = Object.FindFirstObjectByType<BoardView>();
            float hexW = PlayerPieceVisuals.FlatHexWidth(view.HexScale) * view.transform.lossyScale.x;
            float Width(Transform t) => t.GetComponentsInChildren<MeshFilter>()
                .Select(f => f.sharedMesh.bounds.size.x * f.transform.lossyScale.x).Max();
            var all = view.GetComponentsInChildren<Transform>();
            var settlement = all.First(t => t.name == PlayerPieceVisuals.SettlementName);
            var city = all.First(t => t.name == PlayerPieceVisuals.CityName);
            float sw = Width(settlement) / hexW, cw = Width(city) / hexW;
            Debug.Log($"v140 piece scale: settlement {sw:F3}, city {cw:F3} of the hex width");
            Assert.AreEqual(PlayerPieceVisuals.SettlementFootprintOfHex * RunHudController.HotSeatBuildingScale, sw, 0.015f);
            Assert.Greater(cw, sw * 1.3f, "a city reads clearly bigger than a settlement");
            MaybeWrite(hud, "v140-midgame-5p.png");

            hud.ExitHotSeat();
            yield return Settle(hud);
            Assert.AreEqual(1f, PlayerPieceVisuals.BuildingScaleMultiplier, "single-player scale restored");
            Assert.AreEqual(1f, PlayerPieceVisuals.RoadScaleMultiplier);
        }

        [Timeout(300000)]
        [UnityTest]
        public IEnumerator V140_FiveSeatTrade_ClearOfPanels_PickerLocks_StatusClears()
        {
            yield return LoadScene();
            var gameManager = Object.FindFirstObjectByType<GameManager>();
            RunHudController hud = null;
            yield return ReadyHud(gameManager, h => hud = h);

            var config = PartyConfig(1405, (SeatKind.Human, "Rasmus"), (SeatKind.Ai, "AI 2"), (SeatKind.Human, "Freja"),
                (SeatKind.Ai, "AI 4"), (SeatKind.Ai, "AI 5"));
            hud.HotSeatClockFrozenForTests = true;
            hud.StartHotSeat(config);
            var s = hud.HotSeatSessionForTests;
            var state = s.State;
            Assert.IsTrue(Drive(s, () => state.CurrentSeat == 0 && state.Phase == MatchPhase.Main && state.ActiveTrade == null
                                         && state.Round >= 1));
            state.Seats[0].Hand = new ResourceBundle { Wood = 1, Brick = 2, Wheat = 2, Sheep = 1 };
            state.Seats[1].Hand = new ResourceBundle { Wood = 1 };
            state.Seats[2].Hand = new ResourceBundle { Wheat = 1, Stone = 2 };
            state.Seats[3].Hand = new ResourceBundle { Sheep = 1 };
            state.Seats[4].Hand = new ResourceBundle { Brick = 1 };

            hud.SetHotSeatTradeForTests(true, ResourceBundle.Zero, new ResourceBundle { Stone = 1 });
            yield return Settle(hud);
            FindButton(hud.HotSeatRootForTests, "GiveMore_Any").onClick.Invoke();
            yield return Settle(hud);
            AssertClearOfPanels(hud, hud.HotSeatRootForTests.Find("TradeBoard"), "the composer");
            MaybeWrite(hud, "v140-trade-compose.png");
            FindButton(hud.HotSeatRootForTests, "ProposeTrade").onClick.Invoke();
            Assert.IsNotNull(state.ActiveTrade);
            yield return Settle(hud);
            AssertClearOfPanels(hud, hud.HotSeatRootForTests.Find("TradeBoard"), "the offer (four answer rows)");
            StringAssert.Contains("offers a trade", hud.HotSeatRootForTests.Find("Banner/Log").GetComponent<Text>().text);

            hud.SetHotSeatTradeViewerForTests(2);
            yield return Settle(hud);
            AssertClearOfPanels(hud, hud.HotSeatRootForTests.Find("TradeBoard"), "Freja's view");
            FindButton(hud.HotSeatRootForTests, "Accept_2").onClick.Invoke();
            Assert.IsTrue(hud.HotSeatWildPickerOpenForTests);
            yield return Settle(hud);
            var board = hud.HotSeatRootForTests.Find("TradeBoard");
            AssertClearOfPanels(hud, board, "the wildcard picker");
            var picker = board.Find("WildPicker");
            Assert.IsNotNull(picker);
            // Disallowed (Rasmus has none to spare / asks for it): struck through. Allowed: full colour.
            foreach (var r in new[] { ResourceType.Stone })
                Assert.IsNotNull(picker.Find("WildPickMore_" + r + "/Locked"), r + " is locked");
            foreach (var r in new[] { ResourceType.Wood, ResourceType.Brick, ResourceType.Wheat, ResourceType.Sheep })
            {
                Assert.IsNull(picker.Find("WildPickMore_" + r + "/Locked"), r + " is allowed");
                Assert.AreEqual(1f, picker.Find("WildPickMore_" + r + "/Icon").GetComponent<Image>().color.a, 0.01f,
                    r + " in full colour");
            }

            MaybeWrite(hud, "v140-wildcard-picker.png");
            FindButton(picker, "WildPickCancel").onClick.Invoke();
            hud.SetHotSeatTradeViewerForTests(0);
            yield return Settle(hud);
            FindButton(hud.HotSeatRootForTests, "CancelOffer").onClick.Invoke();
            Assert.IsNull(state.ActiveTrade);
            yield return Settle(hud);
            var status = hud.HotSeatRootForTests.Find("Banner/Log").GetComponent<Text>().text;
            StringAssert.DoesNotContain("offers a trade", status);
            StringAssert.Contains("withdrew", status);
            MaybeWrite(hud, "v140-status-after-withdraw.png");
            FindButton(hud.HotSeatRootForTests, "TradeClose").onClick.Invoke();
            yield return Settle(hud);
            Assert.IsNotNull(hud.HotSeatRootForTests.Find("DevCards"), "the dev-card panel is back once the trade card closes");
            hud.ExitHotSeat();
            yield return Settle(hud);
        }

        private static Bounds Bounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs.Skip(1))
                b.Encapsulate(r.bounds);
            return b;
        }
    }
}
