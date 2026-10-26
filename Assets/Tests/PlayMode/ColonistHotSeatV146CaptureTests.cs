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
    /// v146: random board with the Fishermen lake (2·3·11·12), the fish spend menu and the old boot, public traits
    /// on the seat strip (chip + tooltip), your quest in view while AIs play, the 15 s trade countdown, the thrown
    /// dice + number-token pop, piece drop-in and button feedback. Set COLONIST_HOTSEAT_SHOT_DIR for v146-*.png.
    /// </summary>
    public class ColonistHotSeatV146CaptureTests
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
            Debug.Log($"v146 capture written: {file}");
        }

        private static Button ActionButton(Transform root, string label)
        {
            var b = root.GetComponentsInChildren<Button>(true).FirstOrDefault(x =>
                x.name.StartsWith("Action_") && x.GetComponentInChildren<Text>(true)?.text == label);
            Assert.IsNotNull(b, "action button " + label);
            return b;
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
                        s.EndTurn();
                        break;
                    default:
                        s.ApplySuggested();
                        break;
                }
            }

            return false;
        }

        private static Button Named(Transform root, string name)
        {
            var b = root.GetComponentsInChildren<Button>(true).FirstOrDefault(x => x.name == name);
            Assert.IsNotNull(b, "button " + name);
            return b;
        }

        private static MatchConfig Config(int seed, bool party, params (SeatKind kind, string name)[] seats)
        {
            var config = new MatchConfig { Seed = seed, MapSize = MapSize.Large, PartyRules = party, Fishermen = true };
            foreach (var (kind, name) in seats)
                config.Seats.Add(new SeatConfig(kind, name));
            return config;
        }

        /// <summary>Party matches reveal a ballot each round; click it away so captures show the board.</summary>
        private static void DismissBallot(RunHudController hud)
        {
            var b = hud.HotSeatRootForTests.GetComponentsInChildren<Button>(true).FirstOrDefault(x => x.name == "RevealContinue");
            if (b == null)
                return;
            b.onClick.Invoke();
            hud.RefreshHotSeatNow();
        }

        /// <summary>v147: every settlement/city in the match is on the board, visible and house-sized.</summary>
        private static void AssertSettlementsDrawn(MatchState state)
        {
            var view = Object.FindFirstObjectByType<BoardView>();
            var root = view.transform.Find("Buildings");
            Assert.IsNotNull(root, "buildings root");
            var houses = root.Cast<Transform>()
                .Where(t => t.name == PlayerPieceVisuals.SettlementName || t.name == PlayerPieceVisuals.CityName).ToList();
            Assert.AreEqual(state.Board.VertexBuildings.Count, houses.Count, "one house per settlement/city");
            Assert.Greater(houses.Count, 0);
            foreach (var h in houses)
            {
                Assert.IsTrue(h.gameObject.activeInHierarchy, h.name + " active");
                var body = h.Find(PlayerPieceVisuals.BodyPartName).GetComponent<Renderer>();
                Assert.IsTrue(body.enabled, h.name + " body rendered");
                Assert.Greater(body.bounds.size.x, 0.5f, h.name + " house-sized");
            }
        }

        [Timeout(240000)]
        [UnityTest]
        public IEnumerator RandomLakeBoard_FishSpendMenu_PublicTraits_QuestWhileWaiting()
        {
            yield return LoadScene();
            var gm = Object.FindFirstObjectByType<GameManager>();
            RunHudController hud = null;
            yield return ReadyHud(gm, h => hud = h);

            // Two lobby starts never share a seed (a fresh board, offers and decks every match).
            Assert.AreNotEqual(hud.NextMatchSeed(), hud.NextMatchSeed(), "per-match seeds");

            hud.HotSeatClockFrozenForTests = true;
            hud.StartHotSeat(Config(1461, true, (SeatKind.Human, "Rasmus"), (SeatKind.Ai, "Astrid"), (SeatKind.Ai, "Bjorn")));
            var s = hud.HotSeatSessionForTests;
            Assert.IsTrue(Drive(s, () => s.State.Phase == MatchPhase.Main && s.State.CurrentSeat == 0), "reach my turn");
            var board = s.State.Board;
            Assert.IsTrue(board.LakeHex.HasValue, "the lake replaces the desert");
            Assert.AreNotEqual(board.LakeHex, board.RobberTile, "the robber never sits on the lake");
            hud.RefreshHotSeatNow();
            yield return Settle(hud, 6);
            var lake = Object.FindObjectsByType<HexTileView>(FindObjectsSortMode.None).FirstOrDefault(v => v.IsLakeToken);
            Assert.IsNotNull(lake, "the lake tile view");
            Assert.IsTrue(lake.HasVisibleToken, "the lake shows its numbers");
            Assert.AreEqual(HexTileView.LakeTokenText, lake.TokenLabelText);
            Assert.IsTrue(lake.LakeArtApplied, "v147: the lake hex is painted as water, not sand");
            AssertSettlementsDrawn(s.State);
            MaybeWrite(hud, "v147-random-board-lake.png");
            // v148: coastal fishing grounds show 4–10 only; houses without any outline.
            Assert.IsTrue(s.State.Board.SeaFishGrounds.Values.All(g => g.NumberToken is >= 4 and <= 10 and not 7));
            MaybeWrite(hud, "v148-board.png");

            // Traits are public: a chip on every seat row, a hover tooltip for another seat's trait.
            var root = hud.HotSeatRootForTests;
            for (int i = 0; i < 3; i++)
            {
                var chip = root.Find($"SeatStrip/Seat_{i}/{RunHudController.TraitChipName}");
                Assert.IsNotNull(chip, "trait chip on seat " + i);
                var name = MatchPartyLibrary.Trait(s.State.Seats[i].Trait).Name;
                Assert.AreEqual(name, chip.GetComponentInChildren<Text>(true).text);
            }

            hud.ShowTraitTooltipForTests(1);
            yield return Settle(hud, 2);
            var tip = hud.TraitTooltipForTests;
            Assert.IsNotNull(tip, "hover tooltip");
            StringAssert.Contains(MatchPartyLibrary.Trait(s.State.Seats[1].Trait).Description,
                string.Join("|", tip.GetComponentsInChildren<Text>(true).Select(t => t.text)));
            Assert.IsNotNull(hud.OwnQuestCardForTests, "my quest on my own turn");
            var myQuest = MatchPartyLibrary.Quest(s.State.Seats[0].Quest);
            Assert.IsTrue(hud.OwnQuestCardForTests.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains(myQuest.Name.ToUpperInvariant())));
            MaybeWrite(hud, "v147-traits.png"); // with Astrid's trait tooltip open
            hud.HideTraitTooltipForTests();

            // Fish: the spend menu (2/3/4/5/7) and the old boot.
            var me = s.State.Seats[0];
            me.Fish = 9;
            var bj = s.State.Seats[2].Hand;
            bj.Add(ResourceType.Stone, 2);
            s.State.Seats[2].Hand = bj;
            if (!board.RobberTile.HasValue)
                board.PlaceRobber(board.Tiles.Keys.First(k => !k.Equals(board.LakeHex.Value)));
            s.State.OldBootSeat = 0;
            hud.RefreshHotSeatNow();
            yield return Settle(hud, 3);
            root = hud.HotSeatRootForTests;
            Assert.IsNotNull(root.Find($"SeatStrip/Seat_0/{RunHudController.FishBootBadgeName}"), "boot on my seat row");
            Named(root, RunHudController.FishChipName).onClick.Invoke();
            yield return Settle(hud, 3);
            root = hud.HotSeatRootForTests;
            Assert.IsNotNull(root.Find(RunHudController.FishPickerName), "spend menu open");
            foreach (int cost in MatchController.FishSpendCosts)
                Assert.IsNotNull(root.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "FishSpend_" + cost), "spend " + cost);
            Assert.IsTrue(Named(root, "FishSpend_2").interactable, "robber off is live");
            Assert.IsTrue(root.GetComponentsInChildren<Button>(true).Any(b => b.name == "BootTo_1"), "pass the boot to Astrid");
            MaybeWrite(hud, "v147-fish-spend.png");

            int ore = me.Hand[ResourceType.Stone];
            Named(root, "FishSpend_4").onClick.Invoke();
            yield return Settle(hud, 3);
            Named(hud.HotSeatRootForTests, "FishFor_Stone").onClick.Invoke();
            yield return Settle(hud, 3);
            Assert.AreEqual(5, me.Fish, "4 fish for a card");
            Assert.AreEqual(ore + 1, me.Hand[ResourceType.Stone]);

            Named(hud.HotSeatRootForTests, RunHudController.FishChipName).onClick.Invoke();
            yield return Settle(hud, 3);
            Named(hud.HotSeatRootForTests, "FishSpend_3").onClick.Invoke();
            yield return Settle(hud, 3);
            int before = me.Hand.Total;
            Named(hud.HotSeatRootForTests, "FishSteal_2").onClick.Invoke();
            yield return Settle(hud, 3);
            Assert.AreEqual(2, me.Fish, "3 fish to steal");
            Assert.AreEqual(before + 1, me.Hand.Total, "stole one card");

            Named(hud.HotSeatRootForTests, RunHudController.FishChipName).onClick.Invoke();
            yield return Settle(hud, 3);
            Named(hud.HotSeatRootForTests, "BootTo_1").onClick.Invoke();
            yield return Settle(hud, 3);
            Assert.AreEqual(1, s.State.OldBootSeat, "the boot moved on");
            Assert.AreEqual(s.State.Config.VictoryPoints + 1, s.Match.VictoryTarget(1));

            // My quest stays in view while the AIs play.
            Assert.IsTrue(s.EndTurn().Ok);
            hud.RefreshHotSeatNow();
            yield return Settle(hud, 3);
            Assert.IsNotNull(hud.OwnQuestCardForTests, "quest visible while waiting");
            StringAssert.Contains("QUEST", string.Join("|", hud.OwnQuestCardForTests.GetComponentsInChildren<Text>(true).Select(t => t.text)));
            MaybeWrite(hud, "v147-quest-while-waiting.png");
            hud.ExitHotSeat();
        }

        [Timeout(240000)]
        [UnityTest]
        public IEnumerator PassScreen_HidesTheQuest_ButTraitsStayPublic()
        {
            yield return LoadScene();
            var gm = Object.FindFirstObjectByType<GameManager>();
            RunHudController hud = null;
            yield return ReadyHud(gm, h => hud = h);
            hud.HotSeatClockFrozenForTests = true;
            hud.StartHotSeat(Config(1462, true, (SeatKind.Human, "Rasmus"), (SeatKind.Human, "Freja"), (SeatKind.Ai, "Bjorn")));
            var s = hud.HotSeatSessionForTests;
            Assert.IsTrue(Drive(s, () => s.State.Phase == MatchPhase.Main && s.State.CurrentSeat == 0));
            Assert.IsTrue(s.EndTurn().Ok);
            int guard = 0;
            while (!s.PassScreenPending && guard++ < 400)
                s.Tick(0f);
            Assert.IsTrue(s.PassScreenPending, "Freja's pass screen");
            hud.RefreshHotSeatNow();
            yield return Settle(hud, 3);
            Assert.IsNull(hud.OwnQuestCardForTests, "no quest on a pass screen");
            s.ConfirmPass();
            hud.RefreshHotSeatNow();
            yield return Settle(hud, 3);
            Assert.IsNotNull(hud.OwnQuestCardForTests, "Freja sees her own quest");
            var freja = MatchPartyLibrary.Quest(s.State.Seats[1].Quest).Name.ToUpperInvariant();
            Assert.IsTrue(hud.OwnQuestCardForTests.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains(freja)));
            Assert.IsNotNull(hud.HotSeatRootForTests.Find($"SeatStrip/Seat_0/{RunHudController.TraitChipName}"));
            hud.ExitHotSeat();
        }

        [Timeout(240000)]
        [UnityTest]
        public IEnumerator TradeCountdown_DiceThrow_NumberPop_PieceDrop_ButtonFeedback()
        {
            yield return LoadScene();
            var gm = Object.FindFirstObjectByType<GameManager>();
            RunHudController hud = null;
            yield return ReadyHud(gm, h => hud = h);
            hud.HotSeatClockFrozenForTests = true;
            hud.StartHotSeat(Config(1463, true, (SeatKind.Human, "Rasmus"), (SeatKind.Ai, "Astrid"), (SeatKind.Ai, "Bjorn")));
            var s = hud.HotSeatSessionForTests;
            var view = Object.FindFirstObjectByType<BoardView>();
            Assert.IsNotNull(view);

            // A placed piece drops in.
            Assert.IsTrue(Drive(s, () => s.State.Phase == MatchPhase.SetupSettlement && s.State.ActingSeat == 0));
            hud.RefreshHotSeatNow();
            yield return Settle(hud, 2);
            Assert.IsTrue(s.ApplySuggested().Ok);
            hud.RefreshHotSeatNow();
            Assert.Greater(view.ActiveDropCountForTests, 0, "the new settlement drops in");
            yield return new WaitForSeconds(BoardView.PieceDropSeconds + 0.1f);
            Assert.AreEqual(0, view.ActiveDropCountForTests, "and lands");
            AssertSettlementsDrawn(s.State);

            // Buttons lean in on hover.
            var anyButton = hud.HotSeatRootForTests.GetComponentsInChildren<ButtonJuice>(true)
                .FirstOrDefault(j => j.GetComponent<Button>().IsInteractable());
            Assert.IsNotNull(anyButton, "HUD buttons carry hover/press feedback");
            anyButton.OnPointerEnter(null);
            yield return new WaitForSeconds(0.25f);
            Assert.Greater(anyButton.CurrentScaleForTests, 1.01f, "hover grows the button");
            anyButton.OnPointerExit(null);

            // Dice are thrown on every roll; the rolled numbers pop on the board.
            bool popped = false;
            for (int turn = 0; turn < 8 && !popped; turn++)
            {
                Assert.IsTrue(Drive(s, () => s.State.Phase == MatchPhase.Roll && s.State.CurrentSeat == 0), "my roll");
                hud.RefreshHotSeatNow();
                yield return Settle(hud, 2);
                yield return new WaitForSeconds(2.2f);
                Assert.IsTrue(s.Roll().Ok);
                yield return Settle(hud, 3);
                Assert.IsTrue(hud.DiceThrownForTests, "the dice are thrown out of the tray");
                DismissBallot(hud);
                Assert.IsTrue(hud.DiceRollingForTests);
                if (s.State.LastRoll == 7)
                {
                    yield return new WaitForSeconds(2.2f);
                    continue;
                }

                yield return new WaitForSeconds(RunHudController.DiceRollSeconds + 0.12f);
                Assert.IsFalse(hud.DiceRollingForTests, "landed");
                Assert.AreEqual(s.State.LastRoll.ToString(), hud.DiceSumTextForTests, "the sum badge");
                Assert.Greater(hud.LastPoppedTokensForTests, 0, "number tokens hit by the roll pop");
                Assert.Greater(view.PoppingTokenCountForTests, 0, "popping now");
                HudMotion.SnapOnCapture = false;
                try { MaybeWrite(hud, "v147-number-pop.png"); }
                finally { HudMotion.SnapOnCapture = true; }
                popped = true;
                yield return new WaitForSeconds(BoardView.NumberPopSeconds + 0.1f);
                Assert.AreEqual(0, view.PoppingTokenCountForTests, "the pop settles");
            }

            Assert.IsTrue(popped, "a non-7 roll within 8 turns");

            // A trade offer counts down from 15 s and closes by itself.
            Assert.IsTrue(Drive(s, () => s.State.Phase == MatchPhase.Main && s.State.CurrentSeat == 0 && s.Current.Hand.Total > 0));
            var hand = s.Current.Hand;
            var giveRes = System.Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>().First(r => hand[r] > 0);
            var getRes = System.Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>().First(r => r != giveRes);
            var give = ResourceBundle.Zero;
            give.Add(giveRes, 1);
            var get = ResourceBundle.Zero;
            get.Add(getRes, 1);
            Assert.IsTrue(s.Apply(MatchCommand.Propose(0, give, get)).Ok);
            hud.RefreshHotSeatNow();
            yield return Settle(hud, 3);
            Assert.AreEqual("Closes in 15 s", hud.TradeCountdownTextForTests);
            DismissBallot(hud);
            s.Tick(9.3f);
            yield return Settle(hud, 3);
            Assert.IsNotNull(s.State.ActiveTrade, "still open at 9 s");
            Assert.AreEqual("Closes in 6 s", hud.TradeCountdownTextForTests);
            for (int i = 0; i < 3; i++)
                Assert.IsNotNull(hud.HotSeatRootForTests.Find($"SeatStrip/Seat_{i}/{RunHudController.TraitChipName}"),
                    "trait chips stay on the strip while a trade is open, seat " + i);
            MaybeWrite(hud, "v147-trade-countdown.png");
            s.Tick(6f);
            yield return Settle(hud, 3);
            Assert.IsNull(s.State.ActiveTrade, "closed after 15 s");
            Assert.IsNull(hud.HotSeatRootForTests.Find("TradeBoard"), "the offer card is gone");
            StringAssert.Contains("closed after 15 s", s.LastMessage);
            hud.ExitHotSeat();
        }
    }
}
