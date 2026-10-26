using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Civilizations;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Match;
using CatanRoguelike.Core.Shop;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    /// <summary>
    /// v142: human-like AI — trade offers and answers (counters, no deals with a near-winner, at most two offers a
    /// turn), an AI's offer waiting for the humans, knights before the roll and for Largest Army, a robber that
    /// spares its own hexes — plus the v142 own-roll trait rules.
    /// </summary>
    public class MatchAiV142Tests
    {
        private static MatchController AfterSetup(int seats, int humans, int seed, bool party = false)
        {
            var match = new MatchController(MatchConfig.Create(seats, humans, seed, partyRules: party));
            while (match.IsSetup || match.State.IsPicking)
                Assert.IsTrue(match.Apply(MatchAi.ChooseCommand(match)).Ok);
            return match;
        }

        private static MatchController AtMain(int seats, int humans, int seed, int current)
        {
            var match = AfterSetup(seats, humans, seed);
            match.State.CurrentSeat = current;
            match.State.Phase = MatchPhase.Main;
            return match;
        }

        /// <summary>A hand one <paramref name="short"/> card short of the AI's next build, with two spare cards.</summary>
        private static (ResourceBundle hand, ResourceType need, ResourceType spare) OneShort(MatchController match, int seat)
        {
            var goal = MatchAi.PrimaryGoal(match, seat);
            var need = MatchController.AllResources.First(r => goal[r] > 0);
            var spare = MatchController.AllResources.First(r => goal[r] == 0);
            var hand = goal;
            hand.Add(need, -1);
            hand.Add(spare, 2);
            return (hand, need, spare);
        }

        [Test]
        public void Ai_ProposesOneForOne_ThenSweetens_AndStopsAfterTwoOffers()
        {
            var match = AtMain(4, 0, 142, 0);
            var (hand, need, spare) = OneShort(match, 0);
            match.State.Seats[0].Hand = hand;
            for (int i = 1; i < 4; i++)
                match.State.Seats[i].Hand = new ResourceBundle { Wood = 2, Brick = 2, Wheat = 2, Sheep = 2, Stone = 2 };

            var first = MatchAi.ChooseProposal(match, 0);
            Assert.IsNotNull(first, "one card short of the next build: ask the table");
            Assert.AreEqual(MatchCommandType.ProposeTrade, first.Type);
            Assert.AreEqual(1, first.Request[need]);
            Assert.AreEqual(1, first.Request.Total);
            Assert.AreEqual(1, first.Offer.Total, "a fair 1-for-1 first");
            Assert.AreEqual(1, first.Offer[spare]);
            Assert.IsTrue(match.Apply(first).Ok);
            Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.CancelTrade, 0)).Ok);

            var second = MatchAi.ChooseProposal(match, 0);
            Assert.IsNotNull(second);
            Assert.AreEqual(2, second.Offer.Total, "nobody bit: the same ask, sweetened");
            Assert.IsTrue(match.Apply(second).Ok);
            Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.CancelTrade, 0)).Ok);

            Assert.AreEqual(2, MatchAi.ProposalsThisTurn(match, 0));
            Assert.IsNull(MatchAi.ChooseProposal(match, 0), "two offers a turn at most");
        }

        [Test]
        public void Ai_AcceptsAGoodDeal_ButNeverHelpsANearWinner()
        {
            var match = AtMain(3, 1, 7, 0);
            var (hand, need, spare) = OneShort(match, 1);
            match.State.Seats[1].Hand = hand;
            var give = ResourceBundle.Zero;
            give.Add(need, 1);
            var get = ResourceBundle.Zero;
            get.Add(spare, 1);
            match.State.Seats[0].Hand = give;
            Assert.IsTrue(match.Apply(MatchCommand.Propose(0, give, get)).Ok);
            Assert.AreEqual(TradeResponseKind.Accepted, MatchAi.ChooseTradeResponse(match, 1).Response,
                "the card it is missing for a spare: yes");

            match.State.Config.VictoryPoints = match.State.Seats[0].PublicVictoryPoints + MatchAi.NearWinMargin;
            Assert.AreEqual(TradeResponseKind.Declined, MatchAi.ChooseTradeResponse(match, 1).Response,
                "no trades with a player about to win");
        }

        [Test]
        public void Ai_DeclinesOrCounters_ABadDeal()
        {
            var match = AtMain(3, 1, 9, 0);
            var (hand, need, _) = OneShort(match, 1);
            match.State.Seats[1].Hand = hand;
            // They ask for the very card we are short of and give back one we have plenty of.
            var get = ResourceBundle.Zero;
            get.Add(MatchController.AllResources.First(r => hand[r] > 0 && r != need), 1);
            var give = ResourceBundle.Zero;
            give.Add(MatchController.AllResources.First(r => r != need && get[r] == 0), 1);
            match.State.Seats[0].Hand = give;
            Assert.IsTrue(match.Apply(MatchCommand.Propose(0, give, get)).Ok);
            var answer = MatchAi.ChooseTradeResponse(match, 1);
            Assert.AreNotEqual(TradeResponseKind.Accepted, answer.Response);
            if (answer.Response == TradeResponseKind.Countered)
                Assert.AreEqual(1, answer.Offer[need], "a counter asks for what it needs");
        }

        [Test]
        public void Ai_ResolvesItsOffer_WithTheBestPartner_OrWithdraws()
        {
            var match = AtMain(3, 0, 11, 0);
            var (hand, need, spare) = OneShort(match, 0);
            match.State.Seats[0].Hand = hand;
            var get = ResourceBundle.Zero;
            get.Add(need, 1);
            var give = ResourceBundle.Zero;
            give.Add(spare, 1);
            match.State.Seats[1].Hand = get;
            match.State.Seats[2].Hand = get;
            Assert.IsTrue(match.Apply(MatchCommand.Propose(0, give, get)).Ok);
            Assert.IsTrue(match.Apply(MatchCommand.Respond(1, TradeResponseKind.Declined)).Ok);
            Assert.IsTrue(match.Apply(MatchCommand.Respond(2, TradeResponseKind.Accepted)).Ok);
            var close = MatchAi.ResolveOwnOffer(match, 0);
            Assert.AreEqual(MatchCommandType.ConfirmTrade, close.Type);
            Assert.AreEqual(2, close.Victim);

            match.State.Config.VictoryPoints = match.State.Seats[2].PublicVictoryPoints + MatchAi.NearWinMargin;
            Assert.AreEqual(MatchCommandType.CancelTrade, MatchAi.ResolveOwnOffer(match, 0).Type,
                "the only taker is about to win: withdraw");
        }

        [Test]
        public void AiOffer_WaitsForTheHuman_AndTheTimerDeclinesForThem()
        {
            var match = AtMain(3, 1, 13, 1); // seat 0 is the human, seat 1 the AI on turn
            var (hand, need, spare) = OneShort(match, 1);
            match.State.Seats[1].Hand = hand;
            var get = ResourceBundle.Zero;
            get.Add(need, 1);
            var give = ResourceBundle.Zero;
            give.Add(spare, 1);
            Assert.IsTrue(match.Apply(MatchCommand.Propose(1, give, get)).Ok);
            Assert.IsTrue(MatchAi.AwaitingHumanAnswer(match));

            MatchRunner.AdvanceAi(match);
            Assert.IsNotNull(match.State.ActiveTrade, "the AI keeps its offer open for the human");
            Assert.AreNotEqual(TradeResponseKind.Pending, match.State.ActiveTrade.Responses[2].Kind, "the other AI answered");
            Assert.AreEqual(TradeResponseKind.Pending, match.State.ActiveTrade.Responses[0].Kind);
            Assert.IsFalse(MatchRunner.HasAiWork(match));

            MatchRunner.ResolveTimeout(match);
            Assert.IsTrue(match.State.ActiveTrade == null
                          || match.State.ActiveTrade.Responses[0].Kind == TradeResponseKind.Declined,
                "silence is a no");
            Assert.IsFalse(MatchAi.AwaitingHumanAnswer(match));
            int logged = match.CommandLog.Count;
            MatchRunner.AdvanceAi(match);
            Assert.IsTrue(match.CommandLog.Skip(logged).Any(c => c.Seat == 1 && (c.Type == MatchCommandType.ConfirmTrade
                                                                                 || c.Type == MatchCommandType.CancelTrade)),
                "the AI closes or withdraws once everyone answered");
            Assert.LessOrEqual(MatchAi.ProposalsThisTurn(match, 1), MatchAi.MaxProposalsPerTurn);
        }

        private static Vertex OwnSettlement(MatchController match, int seat) =>
            match.State.Board.VertexBuildings.First(kv => MatchSeats.ToSeat(kv.Value.owner) == seat).Key;

        [Test]
        public void Knight_BeforeTheRoll_WhenTheRobberSitsOnOurTile()
        {
            var match = AfterSetup(4, 0, 21);
            int seat = match.State.CurrentSeat;
            Assert.AreEqual(MatchPhase.Roll, match.State.Phase);
            match.State.Seats[seat].DevCards.Add(MatchDevCard.Knight);
            var own = OwnSettlement(match, seat);
            var hex = VertexGraph.GetHexesForVertex(own)
                .First(h => match.State.Board.TryGetTile(h, out var t) && !t.IsDesert);
            var away = match.State.Board.Tiles.Values.First(t => t.IsDesert).Coord;
            match.State.Board.PlaceRobber(away);
            Assert.IsNull(MatchAi.ChooseKnight(match, seat, preRoll: true), "nothing to fix: just roll");

            match.State.Board.PlaceRobber(hex);
            var cmd = MatchAi.ChooseKnight(match, seat, preRoll: true);
            Assert.IsNotNull(cmd, "the robber blocks our tile: knight first");
            Assert.AreEqual(MatchCommandType.PlayKnight, cmd.Type);
            Assert.AreEqual(MatchCommandType.PlayKnight, MatchAi.ChooseCommand(match).Type);
        }

        [Test]
        public void Knight_TakesLargestArmy()
        {
            var match = AfterSetup(4, 0, 23);
            int seat = match.State.CurrentSeat;
            var me = match.State.Seats[seat];
            me.DevCards.Add(MatchDevCard.Knight);
            var away = match.State.Board.Tiles.Values.First(t => t.IsDesert).Coord;
            match.State.Board.PlaceRobber(away);
            Assert.IsNull(MatchAi.ChooseKnight(match, seat, preRoll: true));
            me.KnightsPlayed = MatchController.LargestArmyMinimum - 1;
            Assert.IsNotNull(MatchAi.ChooseKnight(match, seat, preRoll: true), "one more knight takes the army");

            int rival = (seat + 1) % 4;
            match.State.Seats[rival].KnightsPlayed = MatchController.LargestArmyMinimum + 1;
            match.State.LargestArmySeat = rival;
            Assert.IsNull(MatchAi.ChooseKnight(match, seat, preRoll: true), "can't catch them this turn");
        }

        [Test]
        public void Robber_NeverLandsOnItsOwnHexes_WhenThereIsAChoice()
        {
            for (int seed = 30; seed < 40; seed++)
            {
                var match = AtMain(4, 0, seed, 0);
                int seat = match.State.CurrentSeat;
                match.State.Phase = MatchPhase.MoveRobber;
                var cmd = MatchAi.ChooseRobber(match, seat);
                Assert.IsTrue(match.State.Board.TryGetTile(cmd.Hex, out _));
                for (int c = 0; c < 6; c++)
                {
                    var v = VertexGraph.Canonicalize(new Vertex(cmd.Hex, c));
                    if (match.State.Board.VertexBuildings.TryGetValue(v, out var b))
                        Assert.AreNotEqual(seat, MatchSeats.ToSeat(b.owner), $"seed {seed}: robber on own hex");
                }

                Assert.IsTrue(match.Apply(cmd).Ok, $"seed {seed}");
            }
        }

        [Test]
        public void LuckyDice_PaysOnAnyonesTwoOrTwelve_NotThreeOrEleven_ProspectorOwnRollOnly()
        {
            int luckyRolls = 0, nearMiss = 0;
            for (int seed = 1; seed < 600 && (luckyRolls < 2 || nearMiss < 2); seed++)
            {
                var a = AfterSetup(3, 0, seed, party: true);
                var b = AfterSetup(3, 0, seed, party: true);
                Assert.AreEqual(MatchPhase.Roll, a.State.Phase);
                int me = a.State.CurrentSeat;
                int other = (me + 1) % 3;
                foreach (var m in new[] { a, b })
                    foreach (var s in m.State.Seats)
                    {
                        s.Trait = MatchTraitId.None;
                        s.Civilization = CivilizationId.SunKingdom;
                        s.Quest = MatchQuestId.None;
                    }

                a.State.Seats[other].Trait = MatchTraitId.LuckyDice;
                Assert.IsTrue(a.Apply(MatchCommand.Simple(MatchCommandType.RollDice, me)).Ok);
                Assert.IsTrue(b.Apply(MatchCommand.Simple(MatchCommandType.RollDice, me)).Ok);
                int roll = a.State.LastRoll;
                Assert.AreEqual(roll, b.State.LastRoll);
                if (roll == 7)
                    continue;
                bool lucky = roll == 2 || roll == 12;
                if (lucky)
                    luckyRolls++;
                if (roll == 3 || roll == 11)
                    nearMiss++;
                Assert.AreEqual(lucky, MatchController.LuckyDiceTriggers(roll));
                Assert.AreEqual(b.State.Seats[other].Hand.Total + (lucky ? 1 : 0), a.State.Seats[other].Hand.Total,
                    $"roll {roll} by another player");
                Assert.AreEqual(b.State.Seats[me].Hand.Total, a.State.Seats[me].Hand.Total, "roller has no trait");
            }

            Assert.Greater(luckyRolls, 0, "at least one 2/12 by another player in the sample");
            Assert.Greater(nearMiss, 0, "at least one 3/11 in the sample");
            for (int roll = 2; roll <= 12; roll++)
                Assert.AreEqual(roll == 2 || roll == 12, MatchController.LuckyDiceTriggers(roll), $"roll {roll}");
        }

        [Test]
        public void LuckyDice_TiesForLeastHeld_AreRandomButDeterministic()
        {
            var hand = new ResourceBundle { Wood = 2, Brick = 2, Sheep = 2, Wheat = 2, Stone = 2 };
            var seen = new HashSet<ResourceType>();
            for (int seed = 0; seed < 200; seed++)
            {
                var r1 = new MatchRng(seed, 7);
                var r2 = new MatchRng(seed, 7);
                var pick = MatchController.LeastHeldRandom(hand, r1);
                Assert.AreEqual(pick, MatchController.LeastHeldRandom(hand, r2), "same RNG state, same pick");
                Assert.AreEqual(r1.State, r2.State);
                seen.Add(pick);
            }

            Assert.AreEqual(5, seen.Count, "every tied resource can come up (not fixed enum order)");

            var partial = new ResourceBundle { Wood = 3, Brick = 1, Sheep = 1, Wheat = 4, Stone = 2 };
            var tiedSeen = new HashSet<ResourceType>();
            for (int seed = 0; seed < 100; seed++)
                tiedSeen.Add(MatchController.LeastHeldRandom(partial, new MatchRng(seed)));
            CollectionAssert.AreEquivalent(new[] { ResourceType.Brick, ResourceType.Sheep }, tiedSeen);

            var unique = new ResourceBundle { Wood = 3, Brick = 1, Sheep = 2, Wheat = 4, Stone = 0 };
            var rng = new MatchRng(5);
            ulong before = rng.State;
            Assert.AreEqual(ResourceType.Stone, MatchController.LeastHeldRandom(unique, rng));
            Assert.AreEqual(before, rng.State, "no draw when there is no tie");
        }

        [Test]
        public void Granary_EveryCardSeven_GivesGrain_AndRaidersOnlyRaidHoarders()
        {
            for (int seed = 1; seed < 200; seed++)
            {
                var match = AfterSetup(3, 0, seed, party: true);
                int me = match.State.CurrentSeat;
                int granary = (me + 1) % 3;
                int raider = (me + 2) % 3;
                foreach (var s in match.State.Seats)
                {
                    s.Trait = MatchTraitId.None;
                    s.Civilization = CivilizationId.SunKingdom;
                    s.Hand = new ResourceBundle { Wood = 1, Brick = 1, Sheep = 1 };
                }

                match.State.Seats[granary].Trait = MatchTraitId.Granary;
                match.State.Seats[raider].Civilization = CivilizationId.NorthernRaiders;
                Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.RollDice, me)).Ok);
                if (match.State.LastRoll != 7)
                    continue;
                Assert.AreEqual(1, match.State.Seats[granary].Hand.Wheat, "Granary: 1 Grain on every 7");
                Assert.AreEqual(3, match.State.Seats[raider].Hand.Total, "small hands are never raided on a rival's 7");
                return;
            }

            Assert.Fail("no 7 in the sample");
        }
        /// <summary>A party match after setup whose first roll (by the current seat) is a 7, with plain civs/traits.</summary>
        private static MatchController FirstRollSeven(out int seed, int from = 1)
        {
            for (seed = from; seed < 2000; seed++)
            {
                var probe = AfterSetup(3, 0, seed, party: true);
                if (probe.State.Rng.RollDie() + probe.State.Rng.RollDie() != 7)
                    continue;
                var match = Plain(AfterSetup(3, 0, seed, party: true));
                return match;
            }

            Assert.Fail("no opening 7 in the sample");
            return null;
        }

        private static MatchController Plain(MatchController match)
        {
            foreach (var s in match.State.Seats)
            {
                s.Trait = MatchTraitId.None;
                s.Civilization = CivilizationId.SunKingdom;
                s.Quest = MatchQuestId.None;
                s.Hand = new ResourceBundle { Wood = 1 };
            }

            return match;
        }

        [Test]
        public void ToughHide_EverySecondSeven_GivesOneRandomResource()
        {
            FirstRollSeven(out int seed);
            var expected = new[] { 0, 1, 0, 1 };
            for (int nth = 1; nth <= 4; nth++)
            {
                var match = Plain(AfterSetup(3, 0, seed, party: true));
                int me = match.State.CurrentSeat;
                int hide = (me + 1) % 3;
                match.State.Seats[hide].Trait = MatchTraitId.ToughHide;
                match.State.Seats[hide].Hand = new ResourceBundle { Wood = 9 };
                match.State.SevensRolled = nth - 1;
                Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.RollDice, me)).Ok);
                Assert.AreEqual(7, match.State.LastRoll);
                Assert.AreEqual(nth, match.State.SevensRolled);
                Assert.IsFalse(match.State.PendingDiscards.ContainsKey(hide), "Tough Hide still never discards");
                Assert.AreEqual(9 + expected[nth - 1], match.State.Seats[hide].Hand.Total, $"7 number {nth}");

                var again = Plain(AfterSetup(3, 0, seed, party: true));
                again.State.Seats[hide].Trait = MatchTraitId.ToughHide;
                again.State.Seats[hide].Hand = new ResourceBundle { Wood = 9 };
                again.State.SevensRolled = nth - 1;
                Assert.IsTrue(again.Apply(MatchCommand.Simple(MatchCommandType.RollDice, me)).Ok);
                Assert.AreEqual(match.State.Seats[hide].Hand, again.State.Seats[hide].Hand, "deterministic pick");
            }

            for (int n = 1; n <= 8; n++)
                Assert.AreEqual(n % 2 == 0, MatchController.ToughHidePaysOn(n), $"7 number {n}");
        }

        [Test]
        public void HeadStart_NoDiscardBeforeFirstTurnEnds_NormalDiscardAfter()
        {
            FirstRollSeven(out int seed);
            foreach (bool done in new[] { false, true })
            {
                var match = Plain(AfterSetup(3, 0, seed, party: true));
                int me = match.State.CurrentSeat;
                int hs = (me + 1) % 3;
                match.State.Seats[hs].Trait = MatchTraitId.HeadStart;
                match.State.Seats[hs].HasTakenFirstTurn = done;
                match.State.Seats[hs].Hand = new ResourceBundle { Wood = 5, Brick = 5 };
                Assert.AreEqual(done ? 5 : 0, match.DiscardOwed(hs));
                Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.RollDice, me)).Ok);
                Assert.AreEqual(7, match.State.LastRoll);
                Assert.AreEqual(done, match.State.PendingDiscards.ContainsKey(hs),
                    done ? "discards normally after the first turn" : "protected before the first turn ends");
            }

            // The flag flips when that seat ends its first normal turn, and only for that seat.
            var m = Plain(AfterSetup(3, 0, 5, party: true));
            int cur = m.State.CurrentSeat;
            Assert.IsTrue(m.State.Seats.All(s => !s.HasTakenFirstTurn), "setup turns don't count");
            m.State.Phase = MatchPhase.Main;
            Assert.IsTrue(m.Apply(MatchCommand.Simple(MatchCommandType.EndTurn, cur)).Ok);
            Assert.IsTrue(m.State.Seats[cur].HasTakenFirstTurn);
            Assert.AreEqual(1, m.State.Seats.Count(s => s.HasTakenFirstTurn));
        }

        [Test]
        public void HeadStart_IsSkippedAsStealTarget_UntilFirstTurnEnds()
        {
            int checkedHexes = 0;
            for (int seed = 1; seed < 40 && checkedHexes == 0; seed++)
            {
                var match = Plain(AfterSetup(3, 0, seed, party: true));
                int me = match.State.CurrentSeat;
                int hs = (me + 1) % 3;
                var target = match.State.Seats[hs];
                target.Trait = MatchTraitId.HeadStart;
                target.Civilization = CivilizationId.ForestFolk; // not Sun Kingdom (small hands are unrobbable)
                target.Hand = new ResourceBundle { Wood = 2, Brick = 2 };
                foreach (var hex in match.State.Board.Tiles.Keys.Where(h => h != match.State.Board.RobberTile).ToList())
                {
                    target.HasTakenFirstTurn = true;
                    if (!match.RobberVictims(me, hex).Contains(hs))
                        continue;
                    target.HasTakenFirstTurn = false;
                    Assert.IsFalse(match.RobberVictims(me, hex).Contains(hs), "robber can't pick a protected Head Start");
                    Assert.IsTrue(match.IsHeadStartProtected(hs));
                    match.State.Phase = MatchPhase.MoveRobber;
                    Assert.IsFalse(match.Apply(MatchCommand.Robber(me, hex, hs)).Ok, "explicit steal is rejected");
                    var aiPick = MatchAi.ChooseCommand(match);
                    if (aiPick.Type == MatchCommandType.MoveRobber)
                        Assert.AreNotEqual(hs, aiPick.Victim, "AI skips the protected seat");
                    target.HasTakenFirstTurn = true;
                    Assert.IsTrue(match.Apply(MatchCommand.Robber(me, hex, hs)).Ok, "fair game after the first turn");
                    Assert.AreEqual(3, target.Hand.Total);
                    checkedHexes++;
                    break;
                }
            }

            Assert.Greater(checkedHexes, 0);
        }
        private static MatchController PilotAtMain(int seed, out int me)
        {
            var match = Plain(AfterSetup(3, 0, seed, party: true));
            me = match.State.CurrentSeat;
            match.State.Seats[me].Trait = MatchTraitId.HarbourPilot;
            match.State.Phase = MatchPhase.Main;
            return match;
        }

        [Test]
        public void HarbourPilot_SettlementOnPort_OwesOnePick_ThatBlocksUntilChosen()
        {
            for (int seed = 1; seed < 60; seed++)
            {
                var match = PilotAtMain(seed, out int me);
                var owner = match.State.Seats[me].Owner;
                var board = match.State.Board;
                Vertex? spot = null;
                foreach (var port in match.State.Ports)
                foreach (var v in PortAccess.ServedVertices(board, port))
                {
                    if (spot.HasValue)
                        break;
                    foreach (var adj in VertexGraph.GetAdjacentVertices(v))
                    {
                        var e = MatchController.NormalizeEdge(new HexMath.Edge(v, adj));
                        if (board.Roads.ContainsKey(e))
                            continue;
                        board.Roads[e] = owner;
                        if (match.ValidSettlements(me).Contains(VertexGraph.Canonicalize(v)))
                        {
                            spot = VertexGraph.Canonicalize(v);
                            break;
                        }

                        board.Roads.Remove(e);
                    }
                }

                if (!spot.HasValue)
                    continue;
                Assert.IsTrue(match.IsPortVertex(spot.Value));
                match.State.Seats[me].Hand = match.SettlementCostFor(me);
                Assert.IsTrue(match.Apply(MatchCommand.AtVertex(MatchCommandType.BuildSettlement, me, spot.Value)).Ok);
                Assert.AreEqual(1, match.State.Seats[me].HarbourPilotPicks);
                Assert.AreEqual(me, match.State.HarbourPickSeat);
                Assert.AreEqual(me, match.State.ActingSeat);
                Assert.IsFalse(match.Apply(MatchCommand.Simple(MatchCommandType.EndTurn, me)).Ok, "the pick comes first");
                Assert.IsTrue(match.Apply(MatchCommand.HarbourPick(me, ResourceType.Stone)).Ok);
                Assert.AreEqual(1, match.State.Seats[me].Hand.Stone);
                Assert.AreEqual(0, match.State.Seats[me].HarbourPilotPicks);
                Assert.IsFalse(match.Apply(MatchCommand.HarbourPick(me, ResourceType.Stone)).Ok, "only one pick");
                Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.EndTurn, me)).Ok);
                return;
            }

            Assert.Fail("no buildable harbour corner in the sample");
        }

        [Test]
        public void HarbourPilot_CityOnPort_OwesAPick_NonPortBuildsGiveNothing()
        {
            int port = 0, inland = 0;
            for (int seed = 1; seed < 40 && (port == 0 || inland == 0); seed++)
            {
                var match = PilotAtMain(seed, out int me);
                var owner = match.State.Seats[me].Owner;
                var mine = match.State.Board.VertexBuildings
                    .Where(kv => kv.Value.owner == owner && kv.Value.type == BuildingType.Settlement)
                    .Select(kv => kv.Key).ToList();
                foreach (var v in mine)
                {
                    bool onPort = match.IsPortVertex(v);
                    var m = PilotAtMain(seed, out _);
                    m.State.Seats[me].Hand = m.CityCostFor(me);
                    Assert.IsTrue(m.Apply(MatchCommand.AtVertex(MatchCommandType.BuildCity, me, v)).Ok);
                    Assert.AreEqual(onPort ? 1 : 0, m.State.Seats[me].HarbourPilotPicks, onPort ? "port city" : "inland city");
                    if (onPort)
                        port++;
                    else
                        inland++;
                }

                // Force a harbour settlement to test the city upgrade even when setup avoided ports.
                if (port == 0)
                {
                    var pv = match.State.Ports.SelectMany(p => PortAccess.ServedVertices(match.State.Board, p))
                        .Select(VertexGraph.Canonicalize)
                        .FirstOrDefault(v => !match.State.Board.VertexBuildings.ContainsKey(v));
                    match.State.Board.VertexBuildings[pv] = (BuildingType.Settlement, owner);
                    match.State.Seats[me].Hand = match.CityCostFor(me);
                    Assert.IsTrue(match.Apply(MatchCommand.AtVertex(MatchCommandType.BuildCity, me, pv)).Ok);
                    Assert.AreEqual(1, match.State.Seats[me].HarbourPilotPicks, "forced port city");
                    port++;
                }

                // Without the trait a port city owes nothing.
                var plain = Plain(AfterSetup(3, 0, seed, party: true));
                plain.State.Phase = MatchPhase.Main;
                foreach (var v in plain.State.Board.VertexBuildings.Where(kv => kv.Value.owner == owner).Select(kv => kv.Key).ToList())
                {
                    plain.State.Seats[me].Hand = plain.CityCostFor(me);
                    Assert.IsTrue(plain.Apply(MatchCommand.AtVertex(MatchCommandType.BuildCity, me, v)).Ok);
                    Assert.AreEqual(0, plain.State.Seats[me].HarbourPilotPicks);
                }
            }

            Assert.Greater(port, 0);
            Assert.Greater(inland, 0);
        }

        [Test]
        public void HarbourPilot_SetupPortSettlements_CountWhenTheTraitIsPicked()
        {
            int found = 0;
            for (int seed = 1; seed < 80 && found < 2; seed++)
            {
                var match = new MatchController(MatchConfig.Create(3, 0, seed, partyRules: true));
                while (match.IsSetup || match.State.IsPicking)
                {
                    if (match.State.Phase == MatchPhase.QuestPick && match.State.HarbourPickSeat < 0)
                    {
                        int seat = match.State.ActingSeat;
                        var me = match.State.Seats[seat];
                        me.TraitOffer[0] = MatchTraitId.HarbourPilot;
                        int ports = match.State.Board.VertexBuildings.Count(kv => kv.Value.owner == me.Owner && match.IsPortVertex(kv.Key));
                        var before = me.Hand.Total;
                        Assert.IsTrue(match.Apply(MatchAi.ChooseCommand(match)).Ok);
                        Assert.AreEqual(MatchTraitId.HarbourPilot, me.Trait);
                        Assert.AreEqual(ports, me.HarbourPilotPicks, $"seed {seed}: one pick per setup harbour settlement");
                        if (ports > 0)
                        {
                            found++;
                            Assert.AreEqual(seat, match.State.ActingSeat, "the picker resolves before the next seat");
                            while (me.HarbourPilotPicks > 0)
                                Assert.IsTrue(match.Apply(MatchAi.ChooseCommand(match)).Ok);
                            Assert.AreEqual(before + ports + (seat == 0 && !match.State.IsPicking ? 1 : 0), me.Hand.Total);
                        }

                        continue;
                    }

                    Assert.IsTrue(match.Apply(MatchAi.ChooseCommand(match)).Ok);
                }
            }

            Assert.Greater(found, 0, "some setup settled on a harbour");
        }

        [Test]
        public void HarbourPilot_AiPicksWhatItsNextBuildLacksMost_AndTimeoutAutoPicks()
        {
            var match = PilotAtMain(3, out int me);
            match.State.Seats[me].HarbourPilotPicks = 1;
            var goal = MatchAi.PrimaryGoal(match, me);
            var hand = match.State.Seats[me].Hand;
            int bestShort = MatchController.AllResources.Max(r => goal[r] - hand[r]);
            var pick = MatchAi.HarbourPilotPick(match, me);
            if (bestShort > 0)
                Assert.AreEqual(bestShort, goal[pick] - hand[pick], "the largest shortfall");
            else
                Assert.AreEqual(MatchController.LeastHeld(hand), pick);
            var cmd = MatchAi.ChooseCommand(match);
            Assert.AreEqual(MatchCommandType.ChooseHarbourPilotResource, cmd.Type);
            Assert.AreEqual(pick, cmd.Receive);

            // Empty hand: the pick is the resource the next build needs most of.
            match.State.Seats[me].Hand = default;
            goal = MatchAi.PrimaryGoal(match, me);
            pick = MatchAi.HarbourPilotPick(match, me);
            Assert.AreEqual(MatchController.AllResources.Max(r => goal[r]), goal[pick]);

            // Timer runs out with the pick owed: auto-picked, then the turn ends as usual.
            int total = match.State.Seats[me].Hand.Total;
            MatchRunner.ResolveTimeout(match);
            Assert.AreEqual(0, match.State.Seats[me].HarbourPilotPicks);
            Assert.AreEqual(total + 1, match.State.Seats[me].Hand.Total);
            Assert.AreNotEqual(me, match.State.CurrentSeat, "turn ended after the auto-pick");
        }
        // ---------- v144 quests ----------

        private static MatchController QuestBoard(MatchQuestId quest, out int seat)
        {
            var match = Plain(AfterSetup(3, 0, 11, party: true));
            seat = 0;
            match.State.Seats[0].Quest = quest;
            match.State.Board.VertexBuildings.Clear();
            match.State.Board.Roads.Clear();
            return match;
        }

        private static HashSet<ResourceType> Touches(MatchController m, Vertex v) =>
            new(VertexGraph.GetHexesForVertex(v).Where(h => m.State.Board.TryGetTile(h, out var t) && !t.IsDesert)
                .Select(h => m.State.Board.Tiles[h].Resource));

        /// <summary>Puts a building of seat on a free corner touching a new <paramref name="want"/> hex and none of <paramref name="avoid"/>.</summary>
        private static Vertex Settle(MatchController m, int seat, ResourceType want, BuildingType type = BuildingType.Settlement,
            params ResourceType[] avoid)
        {
            var board = m.State.Board;
            var owner = m.State.Seats[seat].Owner;
            int had = MatchPartyLibrary.Bordered(m, seat, want);
            foreach (var tile in board.Tiles.Values.Where(t => !t.IsDesert && t.Resource == want).OrderBy(t => t.Coord.Q).ThenBy(t => t.Coord.R))
                for (int c = 0; c < 6; c++)
                {
                    var v = VertexGraph.Canonicalize(new Vertex(tile.Coord, c));
                    if (board.VertexBuildings.ContainsKey(v) || Touches(m, v).Overlaps(avoid))
                        continue;
                    board.VertexBuildings[v] = (type, owner);
                    if (MatchPartyLibrary.Bordered(m, seat, want) > had)
                        return v;
                    board.VertexBuildings.Remove(v);
                }

            Assert.Inconclusive($"no corner for {want} avoiding {string.Join(",", avoid)}");
            return default;
        }

        private static bool Done(MatchController m, int seat) =>
            MatchPartyLibrary.Quest(m.State.Seats[seat].Quest).IsComplete(m, seat);

        [Test]
        public void Breadbasket_NeedsTwoGrainAndABrick()
        {
            var m = QuestBoard(MatchQuestId.Breadbasket, out int me);
            Settle(m, me, ResourceType.Wheat, BuildingType.Settlement, ResourceType.Brick);
            Settle(m, me, ResourceType.Wheat, BuildingType.Settlement, ResourceType.Brick);
            Assert.AreEqual(0, MatchPartyLibrary.Bordered(m, me, ResourceType.Brick));
            Assert.IsFalse(Done(m, me), "two grain without a brick hex is not enough");
            var v = Settle(m, me, ResourceType.Brick);
            Assert.IsTrue(Done(m, me));
            Assert.Greater(MatchAi.QuestBuildGain(QuestBoardUndo(m, v), me, v, BuildingType.Settlement), 0,
                "the AI values the missing brick corner");
        }

        /// <summary>Removes the building at v again (to probe the AI's gain for building there).</summary>
        private static MatchController QuestBoardUndo(MatchController m, Vertex v)
        {
            m.State.Board.VertexBuildings.Remove(v);
            return m;
        }

        [Test]
        public void OreBaron_NeedsTwoOreAndAWool()
        {
            var m = QuestBoard(MatchQuestId.OreBaron, out int me);
            Settle(m, me, ResourceType.Stone, BuildingType.Settlement, ResourceType.Sheep);
            Settle(m, me, ResourceType.Stone, BuildingType.Settlement, ResourceType.Sheep);
            Assert.IsFalse(Done(m, me), "two ore without a wool hex is not enough");
            Settle(m, me, ResourceType.Sheep);
            Assert.IsTrue(Done(m, me));
        }

        [Test]
        public void OreEmpire_NeedsTwoOre_AWoolOrGrain_AndACityOnOre()
        {
            var m = QuestBoard(MatchQuestId.OreEmpire, out int me);
            var a = Settle(m, me, ResourceType.Stone, BuildingType.City, ResourceType.Sheep, ResourceType.Wheat);
            Settle(m, me, ResourceType.Stone, BuildingType.Settlement, ResourceType.Sheep, ResourceType.Wheat);
            Assert.AreEqual(1, MatchPartyLibrary.CitiesBordering(m, me, ResourceType.Stone));
            Assert.IsFalse(Done(m, me), "no wool/grain hex yet");
            var w = Settle(m, me, ResourceType.Wheat);
            Assert.IsTrue(Done(m, me), "grain counts");
            m.State.Board.VertexBuildings.Remove(w);
            Settle(m, me, ResourceType.Sheep);
            Assert.IsTrue(Done(m, me), "wool counts");
            m.State.Board.VertexBuildings[a] = (BuildingType.Settlement, m.State.Seats[me].Owner);
            Assert.IsFalse(Done(m, me), "needs the city on ore");
            Assert.Greater(MatchAi.QuestBuildGain(m, me, a, BuildingType.City), 0, "the AI values the ore city");
        }

        [Test]
        public void FiveHarvests_NeedsOneConnectedRoadNetwork()
        {
            var m = QuestBoard(MatchQuestId.Diverse, out int me);
            var board = m.State.Board;
            var owner = m.State.Seats[me].Owner;
            var spots = new List<Vertex>();
            foreach (var r in MatchController.AllResources)
                if (MatchPartyLibrary.Bordered(m, me, r) == 0)
                    spots.Add(Settle(m, me, r));
            Assert.AreEqual(5, MatchPartyLibrary.DistinctResources(m, me));
            Assert.Less(MatchPartyLibrary.ConnectedDistinctResources(m, me), 5);
            Assert.IsFalse(Done(m, me), "all five bordered but the buildings aren't joined");
            Assert.IsTrue(MatchAi.WantsQuestRoads(m, me), "the AI goes for connecting roads");

            // Join every building to the first one with own roads (shortest paths over land corners).
            var path = new List<Vertex>();
            for (int i = 1; i < spots.Count; i++)
            {
                var p = ShortestPath(m, spots[0], spots[i]);
                Assert.IsNotNull(p);
                for (int k = 0; k + 1 < p.Count; k++)
                    board.Roads[MatchController.NormalizeEdge(new HexMath.Edge(p[k], p[k + 1]))] = owner;
                path.AddRange(p.Skip(1).Take(p.Count - 2));
            }

            Assert.IsTrue(Done(m, me), "one connected network covers all five");

            // A rival settlement on a corner the network runs through cuts it.
            var cut = path.FirstOrDefault(v => !board.VertexBuildings.ContainsKey(v));
            if (!cut.Equals(default(Vertex)))
            {
                board.VertexBuildings[cut] = (BuildingType.Settlement, m.State.Seats[1].Owner);
                Assert.IsFalse(Done(m, me), "a rival building cuts the network");
            }
        }

        private static List<Vertex> ShortestPath(MatchController m, Vertex from, Vertex to)
        {
            var board = m.State.Board;
            var prev = new Dictionary<Vertex, Vertex>();
            var seen = new HashSet<Vertex> { from };
            var queue = new Queue<Vertex>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var v = queue.Dequeue();
                if (v.Equals(to))
                {
                    var path = new List<Vertex> { to };
                    while (!path[0].Equals(from))
                        path.Insert(0, prev[path[0]]);
                    return path;
                }

                foreach (var n0 in VertexGraph.GetAdjacentVertices(v))
                {
                    var n = VertexGraph.Canonicalize(n0);
                    if (!seen.Add(n) || !VertexGraph.GetHexesForVertex(n).Any(h => board.TryGetTile(h, out _)))
                        continue;
                    prev[n] = v;
                    queue.Enqueue(n);
                }
            }

            return null;
        }
    }
}
