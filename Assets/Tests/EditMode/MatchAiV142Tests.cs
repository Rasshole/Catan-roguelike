using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Civilizations;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Match;
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
        public void LuckyDice_And_Prospector_OnlyOnYourOwnRoll()
        {
            int luckyRolls = 0;
            for (int seed = 1; seed < 80 && luckyRolls < 3; seed++)
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

                a.State.Seats[me].Trait = MatchTraitId.LuckyDice;
                a.State.Seats[other].Trait = MatchTraitId.LuckyDice;
                Assert.IsTrue(a.Apply(MatchCommand.Simple(MatchCommandType.RollDice, me)).Ok);
                Assert.IsTrue(b.Apply(MatchCommand.Simple(MatchCommandType.RollDice, me)).Ok);
                int roll = a.State.LastRoll;
                Assert.AreEqual(roll, b.State.LastRoll);
                Assert.AreEqual(b.State.Seats[other].Hand.Total, a.State.Seats[other].Hand.Total,
                    "never on someone else's roll");
                if (roll == 7)
                    continue;
                bool lucky = roll <= 4 || roll >= 10;
                if (lucky)
                    luckyRolls++;
                Assert.AreEqual(b.State.Seats[me].Hand.Total + (lucky ? 1 : 0), a.State.Seats[me].Hand.Total, $"roll {roll}");
            }

            Assert.Greater(luckyRolls, 0, "at least one lucky roll in the sample");
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
    }
}
