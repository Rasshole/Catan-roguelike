using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Match;
using CatanRoguelike.Core.Victory;
using CatanRoguelike.Core.Yield;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    /// <summary>v141 Phase 4 shenanigans: secret ballots, the Night Market, bounties, sabotage cards and Chaos.</summary>
    public class MatchShenanigansV141Tests
    {
        // ---------- helpers ----------

        /// <summary>A party match (all seats human, driven by the policy) stopped at <paramref name="seat"/>'s Main phase.</summary>
        private static MatchController AtMain(int seats, int seed, int seat = 0)
        {
            var match = new MatchController(MatchConfig.Create(seats, seats, seed, partyRules: true));
            StepTo(match, () => match.State.Phase == MatchPhase.Main && match.State.CurrentSeat == seat && !match.IsPregame);
            return match;
        }

        private static void StepTo(MatchController match, System.Func<bool> stop)
        {
            int guard = 0;
            while (!stop() && guard++ < 5000)
            {
                Assert.IsFalse(match.IsOver, "the match ended early");
                var r = match.Apply(MatchAi.ChooseCommand(match));
                Assert.IsTrue(r.Ok, r.Error);
            }

            Assert.IsTrue(stop(), "never reached the wanted state");
        }

        /// <summary>From the last seat's Main phase: end the turn so the night flips <paramref name="ev"/>.</summary>
        private static void NightWith(MatchController match, EventId ev)
        {
            int last = match.State.Seats.Count - 1;
            StepTo(match, () => match.State.Phase == MatchPhase.Main && match.State.CurrentSeat == last);
            match.State.ActiveTrade = null;
            match.State.EventDeck.Add(ev);
            Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.EndTurn, last)).Ok);
            Assert.AreEqual(ev, match.State.ActiveEvent);
        }

        private static ResourceBundle Cards(int wood = 0, int brick = 0, int wheat = 0, int sheep = 0, int stone = 0) =>
            new() { Wood = wood, Brick = brick, Wheat = wheat, Sheep = sheep, Stone = stone };

        private static int OwnedTileSeat(MatchController match, HexCoord hex, int notSeat)
        {
            for (int c = 0; c < 6; c++)
            {
                var v = VertexGraph.Canonicalize(new Vertex(hex, c));
                if (match.State.Board.VertexBuildings.TryGetValue(v, out var b) && MatchSeats.ToSeat(b.owner) != notSeat)
                    return MatchSeats.ToSeat(b.owner);
            }

            return -1;
        }

        // ---------- commands ----------

        [Test]
        public void NewCommands_RoundTripThroughJson()
        {
            var edge = new HexMath.Edge(new Vertex(new HexCoord(1, -1), 2), new Vertex(new HexCoord(1, -1), 3));
            var commands = new[]
            {
                MatchCommand.Vote(2, true), MatchCommand.Vote(1, false), MatchCommand.Bid(3, Cards(wood: 2, stone: 1)),
                MatchCommand.Bounty(0, 4, Cards(sheep: 2)), MatchCommand.Simple(MatchCommandType.BuySabotage, 1),
                MatchCommand.Sabotage(1, MatchSabotageKind.Blight, 2, new HexCoord(-1, 2)),
                MatchCommand.Sabotage(1, MatchSabotageKind.SpoiledGrain, 3), MatchCommand.SabotageRoad(2, 0, edge),
                MatchCommand.Guess(3, 1, Cards(brick: 1)), MatchCommand.Reroll(4, new HexCoord(0, 1))
            };
            var back = MatchCommand.LogFromJson(MatchCommand.LogToJson(commands));
            CollectionAssert.AreEqual(commands, back);
            Assert.AreEqual(22, (int)MatchCommandType.CastVote);
            Assert.AreEqual(28, (int)MatchCommandType.RerollToken);
            Assert.AreEqual(10, (int)MatchPhase.Vote);
            Assert.AreEqual(11, (int)MatchPhase.Auction);
            // Sealed inputs never show their content in the readable command text.
            Assert.That(MatchCommand.Vote(2, true).ToString(), Does.Not.Contain("1"));
            Assert.That(MatchCommand.Bid(3, Cards(wood: 2)).ToString(), Does.Contain("sealed"));
        }

        [Test]
        public void ShenaniganEvents_AreInTheMatchDeck()
        {
            foreach (var id in new[] { EventId.Decree, EventId.NightMarket, EventId.Chaos })
                Assert.IsNotNull(MatchPartyLibrary.Event(id), id.ToString());
        }

        // ---------- secret ballot ----------

        [Test]
        public void Decree_SecretBallot_OneVoterAtATime_ThenOnlyTotalsAndResult()
        {
            var match = AtMain(3, 1411);
            NightWith(match, EventId.Decree);
            Assert.AreEqual(MatchPhase.Vote, match.State.Phase);
            match.State.BallotDecree = MatchDecreeId.PortsThreeToOne;
            Assert.AreEqual(0, match.State.ActingSeat);
            Assert.IsFalse(match.Apply(MatchCommand.Vote(1, true)).Ok, "votes are taken in seat order");
            Assert.IsTrue(match.Apply(MatchCommand.Vote(0, true)).Ok);
            Assert.AreEqual(1, match.State.ActingSeat);
            Assert.IsNull(match.State.LastBallot, "nothing is revealed until everyone has voted");
            Assert.IsTrue(match.Apply(MatchCommand.Vote(1, false)).Ok);
            Assert.IsTrue(match.Apply(MatchCommand.Vote(2, true)).Ok);

            var ballot = match.State.LastBallot;
            Assert.AreEqual(2, ballot.Yes);
            Assert.AreEqual(1, ballot.No);
            Assert.IsTrue(ballot.Passed);
            Assert.AreEqual(-1, ballot.TieBreaker);
            Assert.AreEqual(0, match.State.Votes.Count, "individual votes are discarded after the tally");
            Assert.AreEqual(MatchDecreeId.PortsThreeToOne, match.State.ActiveDecree);
            Assert.AreEqual(MatchPhase.Roll, match.State.Phase);
            Assert.AreEqual(0, match.State.ActingSeat);
            Assert.IsTrue(match.State.Log.Any(l => l.Contains("2 aye, 1 nay")));
        }

        [Test]
        public void Decree_Tie_GoesToThePlayerWithFewestPoints()
        {
            var match = AtMain(4, 1412);
            NightWith(match, EventId.Decree);
            match.State.BallotDecree = MatchDecreeId.NoDesertRobber;
            // Make seat 2 the clear underdog (fewest public VP comes first).
            foreach (var s in match.State.Seats)
                s.VictoryPoints = s.Index == 2 ? 1 : 5;
            Assert.AreEqual(2, match.TieBreakSeat(Enumerable.Range(0, 4)));
            Assert.IsTrue(match.Apply(MatchCommand.Vote(0, false)).Ok);
            Assert.IsTrue(match.Apply(MatchCommand.Vote(1, false)).Ok);
            Assert.IsTrue(match.Apply(MatchCommand.Vote(2, true)).Ok);
            Assert.IsTrue(match.Apply(MatchCommand.Vote(3, true)).Ok);
            Assert.AreEqual(2, match.State.LastBallot.TieBreaker);
            Assert.IsTrue(match.State.LastBallot.Passed, "the underdog voted aye");
            Assert.AreEqual(MatchDecreeId.NoDesertRobber, match.State.ActiveDecree);
        }

        [Test]
        public void NoDesertDecree_RobberCantEnterTheDesert()
        {
            var match = AtMain(3, 1413);
            var desert = match.State.Board.Tiles.Values.First(t => t.IsDesert).Coord;
            var other = match.State.Board.Tiles.Values.First(t => !t.IsDesert && t.Coord != match.State.Board.RobberTile).Coord;
            match.State.Board.PlaceRobber(other);
            match.State.Phase = MatchPhase.MoveRobber;
            match.State.ActiveDecree = MatchDecreeId.NoDesertRobber;
            var r = match.Apply(MatchCommand.Robber(0, desert, -1));
            Assert.IsFalse(r.Ok);
            Assert.That(r.Error, Does.Contain("decree"));
            Assert.AreNotEqual(desert, MatchAi.ChooseCommand(match).Hex, "the AI respects the decree");
        }

        [Test]
        public void PortsDecree_ClosesHarbours()
        {
            var match = AtMain(3, 1414);
            match.State.Seats[0].Civilization = default;
            match.State.Seats[0].Trait = default;
            var port = match.State.Ports.First(p => p.SpecificResource.HasValue);
            match.State.Board.VertexBuildings[VertexGraph.Canonicalize(port.Vertex)] = (BuildingType.Settlement, PlayerId.Human);
            var give = port.SpecificResource.Value;
            int before = match.BankRatio(0, give);
            match.State.ActiveDecree = MatchDecreeId.PortsThreeToOne;
            Assert.LessOrEqual(before, 2);
            Assert.AreEqual(MatchController.BankTradeRatio, match.BankRatio(0, give), "v142: harbours are closed for the day");
        }

        [Test]
        public void HarbourTariff_Passes_SmallHandsWithoutAHarbourTakeACard()
        {
            var match = AtMain(3, 1414);
            NightWith(match, EventId.Decree);
            match.State.BallotDecree = MatchDecreeId.PortsThreeToOne;
            match.State.Seats[0].Hand = new ResourceBundle { Wood = 2 };
            match.State.Seats[1].Hand = new ResourceBundle { Wood = 5 };
            var owners = Enumerable.Range(0, 3).Select(i => !match.HarbourTariffPays(i)).ToList();
            Assert.IsFalse(match.HarbourTariffPays(1), "v142: a big hand gets nothing");
            var before = match.State.Seats.Select(s => s.CardCount).ToList();
            for (int i = 0; i < 3; i++)
                Assert.IsTrue(match.Apply(MatchCommand.Vote(i, true)).Ok);
            Assert.IsTrue(match.State.LastBallot.Passed);
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(before[i] + (owners[i] ? 0 : 1), match.State.Seats[i].CardCount, $"seat {i}");
        }

        [Test]
        public void WealthTax_TheRicherHalfPaysOneCard()
        {
            var match = AtMain(4, 1415);
            NightWith(match, EventId.Decree);
            match.State.BallotDecree = MatchDecreeId.RichestDiscardsToSeven;
            match.State.Seats[0].Hand = Cards(wood: 2);
            match.State.Seats[1].Hand = Cards(wood: 4, brick: 3, stone: 3);
            match.State.Seats[2].Hand = Cards(sheep: 6);
            match.State.Seats[3].Hand = Cards(wheat: 3);
            // Average 5.25: seats 1 (10) and 2 (6) pay; 0 and 3 don't.
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, match.WealthTaxPayers().ToArray());
            for (int i = 0; i < 4; i++)
                Assert.AreEqual(i == 0 || i == 3, MatchAi.ChooseVote(match, i).Pick == 1, $"AI seat {i} votes its interest");
            foreach (var s in match.State.Seats)
                s.VictoryPoints = s.Index == 0 ? 1 : 5;
            for (int i = 0; i < 4; i++)
                Assert.IsTrue(match.Apply(MatchCommand.Vote(i, i == 0 || i == 3)).Ok);
            Assert.AreEqual(0, match.State.LastBallot.TieBreaker, "2–2: the seat with the fewest points decides");
            Assert.IsTrue(match.State.LastBallot.Passed);
            {
                Assert.AreEqual(9, match.State.Seats[1].CardCount);
                Assert.AreEqual(3, match.State.Seats[1].Hand.Wood, "pays from the biggest pile");
                Assert.AreEqual(5, match.State.Seats[2].CardCount);
                Assert.AreEqual(2, match.State.Seats[0].CardCount);
                Assert.AreEqual(3, match.State.Seats[3].CardCount);
            }
        }

        [Test]
        public void WealthTax_SmallHandsNeverPay()
        {
            var match = AtMain(3, 1416);
            foreach (var s in match.State.Seats)
                s.Hand = Cards(wood: 3);
            Assert.AreEqual(0, match.WealthTaxPayers().Count(), $"below {MatchController.WealthTaxFloor} cards nobody pays");
        }

        [Test]
        public void WealthTax_Fails_NobodyPays()
        {
            var match = AtMain(3, 1416);
            NightWith(match, EventId.Decree);
            match.State.BallotDecree = MatchDecreeId.RichestDiscardsToSeven;
            match.State.Seats[1].Hand = Cards(wood: 4, brick: 3, stone: 3);
            match.State.Seats[0].Hand = Cards(wood: 2);
            match.State.Seats[2].Hand = Cards(sheep: 5);
            for (int i = 0; i < 3; i++)
                Assert.IsTrue(match.Apply(MatchCommand.Vote(i, i == 0)).Ok);
            Assert.IsFalse(match.State.LastBallot.Passed);
            Assert.AreEqual(10, match.State.Seats[1].CardCount);
            Assert.AreEqual(MatchDecreeId.None, match.State.ActiveDecree);
        }

        [Test]
        public void Vote_TimeoutResolvesOnlyTheTimedOutVoter()
        {
            var match = AtMain(3, 1417);
            NightWith(match, EventId.Decree);
            MatchRunner.ResolveTimeout(match);
            Assert.AreEqual(MatchPhase.Vote, match.State.Phase);
            Assert.AreEqual(1, match.State.ActingSeat);
        }

        // ---------- Night Market ----------

        [Test]
        public void Auction_SealedSecondPrice_WinnerPaysTheBank()
        {
            var match = AtMain(3, 1421);
            NightWith(match, EventId.NightMarket);
            Assert.AreEqual(MatchPhase.Auction, match.State.Phase);
            match.State.AuctionItem = MatchAuctionItem.VictoryPointCard;
            match.State.Seats[0].Hand = Cards(wood: 4, brick: 1);
            match.State.Seats[1].Hand = Cards(stone: 3);
            match.State.Seats[2].Hand = Cards(sheep: 1);
            int bankBefore = match.State.Seats.Sum(s => s.CardCount);
            int vpBefore = match.State.Seats[0].VictoryPoints;
            Assert.IsFalse(match.Apply(MatchCommand.Bid(0, Cards(stone: 1))).Ok, "can't bid cards you don't hold");
            Assert.IsFalse(match.Apply(MatchCommand.Bid(0, Cards(wood: 2))).Ok, "v142: a VP card has a reserve of 3");
            Assert.IsTrue(match.Apply(MatchCommand.Bid(0, Cards(wood: 4))).Ok);
            Assert.IsNull(match.State.LastAuction, "bids stay sealed until everyone has bid");
            Assert.IsTrue(match.Apply(MatchCommand.Bid(1, Cards(stone: 3))).Ok);
            Assert.IsTrue(match.Apply(MatchCommand.Bid(2, ResourceBundle.Zero)).Ok);

            var result = match.State.LastAuction;
            Assert.AreEqual(0, result.Winner);
            Assert.AreEqual(3, result.Price, "second price");
            Assert.AreEqual(2, result.Bidders);
            Assert.AreEqual(Cards(wood: 1, brick: 1), match.State.Seats[0].Hand);
            Assert.AreEqual(Cards(stone: 3), match.State.Seats[1].Hand, "losers keep their bid");
            Assert.AreEqual(bankBefore - 3, match.State.Seats.Sum(s => s.CardCount), "the price goes to the bank");
            Assert.AreEqual(vpBefore + 1, match.State.Seats[0].VictoryPoints);
            Assert.AreEqual(MatchPhase.Roll, match.State.Phase);
            Assert.AreEqual(0, match.State.Bids.Count);
        }

        [Test]
        public void Auction_TieGoesToFewestPoints_SoleBidderPaysTheReserve()
        {
            var match = AtMain(3, 1422);
            NightWith(match, EventId.NightMarket);
            match.State.AuctionItem = MatchAuctionItem.TokenReroll;
            foreach (var s in match.State.Seats)
            {
                s.Hand = Cards(wood: 2);
                s.VictoryPoints = s.Index == 1 ? 2 : 4;
            }

            Assert.IsTrue(match.Apply(MatchCommand.Bid(0, Cards(wood: 2))).Ok);
            Assert.IsTrue(match.Apply(MatchCommand.Bid(1, Cards(wood: 2))).Ok);
            Assert.IsTrue(match.Apply(MatchCommand.Bid(2, ResourceBundle.Zero)).Ok);
            Assert.AreEqual(1, match.State.LastAuction.Winner);
            Assert.AreEqual(2, match.State.LastAuction.Price);
            Assert.AreEqual(1, match.State.Seats[1].Rerolls);

            var solo = AtMain(3, 1423);
            NightWith(solo, EventId.NightMarket);
            solo.State.AuctionItem = MatchAuctionItem.SabotageCard;
            solo.State.Seats[2].Hand = Cards(stone: 4);
            Assert.IsTrue(solo.Apply(MatchCommand.Bid(0, ResourceBundle.Zero)).Ok);
            Assert.IsTrue(solo.Apply(MatchCommand.Bid(1, ResourceBundle.Zero)).Ok);
            Assert.IsTrue(solo.Apply(MatchCommand.Bid(2, Cards(stone: 4))).Ok);
            Assert.AreEqual(2, solo.State.LastAuction.Price, "v142: a sabotage card costs at least 2");
            Assert.AreEqual(2, solo.State.Seats[2].Hand.Stone);
            Assert.AreEqual(1, solo.State.Seats[2].Sabotage.Count);
        }

        [Test]
        public void TokenReroll_OnlyOwnTiles_NewTokenFromPool_NoAdjacentRed()
        {
            for (int seed = 1430; seed < 1440; seed++)
            {
                var match = AtMain(3, seed);
                match.State.Seats[0].Rerolls = 1;
                var mine = match.RerollableTiles(0).ToList();
                Assert.IsNotEmpty(mine);
                var notMine = match.State.Board.Tiles.Values
                    .First(t => !t.IsDesert && t.NumberToken.HasValue && !mine.Contains(t.Coord)).Coord;
                Assert.IsFalse(match.Apply(MatchCommand.Reroll(0, notMine)).Ok);
                var hex = mine[0];
                int old = match.State.Board.Tiles[hex].NumberToken.Value;
                Assert.IsTrue(match.Apply(MatchCommand.Reroll(0, hex)).Ok);
                int now = match.State.Board.Tiles[hex].NumberToken.Value;
                Assert.AreNotEqual(old, now);
                Assert.Contains(now, NumberTokenLibrary.ClassicPool);
                if (NumberTokenLibrary.IsRedNumber(now))
                    for (int d = 0; d < 6; d++)
                        if (match.State.Board.TryGetTile(HexMath.GetNeighbor(hex, d), out var n) && n.NumberToken.HasValue)
                            Assert.IsFalse(NumberTokenLibrary.IsRedNumber(n.NumberToken.Value), "6/8 next to 6/8");
                Assert.AreEqual(0, match.State.Seats[0].Rerolls);
                Assert.IsFalse(match.Apply(MatchCommand.Reroll(0, hex)).Ok, "the reroll is spent");
            }
        }

        // ---------- bounties ----------

        [Test]
        public void Bounty_Escrow_ClaimedByRobbingTheHolder()
        {
            var match = AtMain(3, 1441);
            match.State.Seats[0].Hand = Cards(sheep: 3);
            Assert.IsFalse(match.Apply(MatchCommand.Bounty(0, 0, Cards(sheep: 1))).Ok, "not on yourself");
            Assert.IsFalse(match.Apply(MatchCommand.Bounty(0, 1, Cards(sheep: 3, wood: 1))).Ok, "max 3 cards");
            Assert.IsTrue(match.Apply(MatchCommand.Bounty(0, 1, Cards(sheep: 2))).Ok);
            Assert.AreEqual(1, match.State.Seats[0].Hand.Sheep, "the reward is paid into escrow");
            Assert.AreEqual(1, match.BountiesOn(1).Count());
            Assert.IsFalse(match.Apply(MatchCommand.Bounty(0, 2, Cards(sheep: 1))).Ok, "one bounty up at a time");

            // Seat 2 rolls a 7 later: put them on the robber step and rob seat 1 on one of seat 1's tiles.
            match.State.CurrentSeat = 2;
            match.State.Phase = MatchPhase.MoveRobber;
            match.State.Seats[1].Hand = Cards(wood: 1);
            var hex = match.State.Board.Tiles.Values.Select(t => t.Coord)
                .First(h => h != match.State.Board.RobberTile && match.RobberVictims(2, h).Contains(1));
            int before = match.State.Seats[2].CardCount;
            Assert.IsTrue(match.Apply(MatchCommand.Robber(2, hex, 1)).Ok);
            Assert.AreEqual(before + 1 + 2, match.State.Seats[2].CardCount, "the stolen card + the bounty");
            Assert.IsEmpty(match.State.Bounties);
            Assert.That(match.State.Log.Last(), Does.Contain("claims"));
        }

        [Test]
        public void Bounty_HolderWithoutCards_RobberMovesBountyStays()
        {
            var match = AtMain(3, 1442);
            match.State.Seats[0].Hand = Cards(sheep: 1);
            Assert.IsTrue(match.Apply(MatchCommand.Bounty(0, 1, Cards(sheep: 1))).Ok);
            match.State.CurrentSeat = 2;
            match.State.Phase = MatchPhase.MoveRobber;
            match.State.Seats[1].Hand = ResourceBundle.Zero;
            var hex = match.State.Board.Tiles.Values.Select(t => t.Coord)
                .First(h => h != match.State.Board.RobberTile && OwnedTileSeat(match, h, 2) == 1
                            && !match.RobberVictims(2, h).Any());
            Assert.IsFalse(match.Apply(MatchCommand.Robber(2, hex, 1)).Ok, "nothing to steal from an empty hand");
            Assert.IsTrue(match.Apply(MatchCommand.Robber(2, hex, -1)).Ok);
            Assert.AreEqual(hex, match.State.Board.RobberTile);
            Assert.AreEqual(1, match.State.Bounties.Count, "the bounty stays up");
        }

        [Test]
        public void Bounty_Unclaimed_ReturnsAtTheEndOfTheAct()
        {
            var match = AtMain(3, 1443);
            match.State.Seats[0].Hand = Cards(brick: 2);
            Assert.IsTrue(match.Apply(MatchCommand.Bounty(0, 2, Cards(brick: 2))).Ok);
            StepTo(match, () => match.State.Round >= MatchController.ActLength || match.State.Bounties.Count == 0);
            if (match.State.Bounties.Count == 0 && match.State.Round < MatchController.ActLength)
                Assert.That(match.State.Log, Has.Some.Contains("claims"));
            else
                Assert.That(match.State.Log, Has.Some.Contains("unclaimed bounties return"));
        }

        // ---------- sabotage ----------

        private static MatchController WithCard(int seed, MatchSabotageKind kind)
        {
            var match = AtMain(3, seed);
            match.State.Seats[0].Sabotage.Add(kind);
            return match;
        }

        [Test]
        public void Sabotage_Buy_CostsGrainAndOre_MaxTwo()
        {
            var match = AtMain(3, 1451);
            match.State.Seats[0].Hand = Cards(wheat: 3, stone: 3);
            Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.BuySabotage, 0)).Ok);
            Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.BuySabotage, 0)).Ok);
            Assert.IsFalse(match.Apply(MatchCommand.Simple(MatchCommandType.BuySabotage, 0)).Ok);
            Assert.AreEqual(Cards(wheat: 1, stone: 1), match.State.Seats[0].Hand);
            Assert.AreEqual(2, match.State.Seats[0].Sabotage.Count);
            Assert.That(match.State.Log.Last(), Does.Not.Contain("Blight").And.Not.Contain("Spoiled").And.Not.Contain("Washed"));
        }

        [Test]
        public void SpoiledGrain_FaceDown_TriggersAtTheTargetsDawn_RevealsTheAuthor()
        {
            var match = WithCard(1452, MatchSabotageKind.SpoiledGrain);
            Assert.IsTrue(match.Apply(MatchCommand.Sabotage(0, MatchSabotageKind.SpoiledGrain, 1)).Ok);
            Assert.IsEmpty(match.State.Seats[0].Sabotage);
            Assert.AreEqual(1, match.PendingSabotageOn(1).Target);
            Assert.That(match.State.Log.Last(), Does.Not.Contain(match.State.Seats[0].Name), "played face-down");
            Assert.IsNull(match.State.LastSabotageReveal);
            Assert.IsFalse(match.Apply(MatchCommand.Sabotage(0, MatchSabotageKind.SpoiledGrain, 1)).Ok);

            match.State.ActiveTrade = null;
            Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.EndTurn, 0)).Ok);
            match.State.Seats[1].Hand = Cards(wheat: 2);
            Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.RollDice, 1)).Ok);
            var reveal = match.State.LastSabotageReveal;
            Assert.AreEqual(MatchSabotageKind.SpoiledGrain, reveal.Kind);
            Assert.AreEqual(0, reveal.Author);
            Assert.AreEqual(1, reveal.Target);
            Assert.IsNull(match.PendingSabotageOn(1));
            Assert.That(match.State.Log, Has.Some.Contains("Spoiled Grain by " + match.State.Seats[0].Name));
        }

        [Test]
        public void Guess_RightCancelsAndUnmasks_WrongCostsACard()
        {
            var match = WithCard(1453, MatchSabotageKind.SpoiledGrain);
            match.State.Seats[0].Sabotage.Add(MatchSabotageKind.Blight);
            Assert.IsTrue(match.Apply(MatchCommand.Sabotage(0, MatchSabotageKind.SpoiledGrain, 1)).Ok);
            match.State.ActiveTrade = null;
            Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.EndTurn, 0)).Ok);
            match.State.Seats[1].Hand = Cards(wood: 2, wheat: 1);
            Assert.IsTrue(match.CanGuessSaboteur(1));
            Assert.IsFalse(match.Apply(MatchCommand.Guess(1, 2, Cards(wood: 2))).Ok, "a guess costs exactly 1 card");
            Assert.IsTrue(match.Apply(MatchCommand.Guess(1, 0, Cards(wood: 1))).Ok);
            Assert.IsNull(match.PendingSabotageOn(1));
            Assert.IsTrue(match.State.LastSabotageReveal.Cancelled);
            Assert.AreEqual(0, match.State.LastSabotageReveal.Author);
            Assert.AreEqual(Cards(wood: 1, wheat: 1), match.State.Seats[1].Hand);

            // A wrong guess: the card stays face-down and only one guess per card.
            var other = WithCard(1454, MatchSabotageKind.SpoiledGrain);
            Assert.IsTrue(other.Apply(MatchCommand.Sabotage(0, MatchSabotageKind.SpoiledGrain, 1)).Ok);
            other.State.ActiveTrade = null;
            Assert.IsTrue(other.Apply(MatchCommand.Simple(MatchCommandType.EndTurn, 0)).Ok);
            other.State.Seats[1].Hand = Cards(wood: 2, wheat: 1);
            Assert.IsTrue(other.Apply(MatchCommand.Guess(1, 2, Cards(wood: 1))).Ok);
            Assert.IsTrue(other.PendingSabotageOn(1).Guessed);
            Assert.IsFalse(other.CanGuessSaboteur(1));
            Assert.IsNull(other.State.LastSabotageReveal);
            Assert.IsTrue(other.Apply(MatchCommand.Simple(MatchCommandType.RollDice, 1)).Ok);
            Assert.AreEqual(0, other.State.LastSabotageReveal.Author, "it triggers with its author at dawn");
        }

        [Test]
        public void Blight_OnlyOnTheTargetsTiles_RevealedAtDawn()
        {
            var match = WithCard(1455, MatchSabotageKind.Blight);
            var theirs = match.BlightTiles(1).ToList();
            var notTheirs = match.State.Board.Tiles.Values
                .First(t => !t.IsDesert && t.NumberToken.HasValue && !theirs.Contains(t.Coord)).Coord;
            Assert.IsFalse(match.Apply(MatchCommand.Sabotage(0, MatchSabotageKind.Blight, 1, notTheirs)).Ok);
            Assert.IsTrue(match.Apply(MatchCommand.Sabotage(0, MatchSabotageKind.Blight, 1, theirs[0])).Ok);
            match.State.ActiveTrade = null;
            Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.EndTurn, 0)).Ok);
            Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.RollDice, 1)).Ok);
            Assert.AreEqual(MatchSabotageKind.Blight, match.State.LastSabotageReveal.Kind);
            Assert.IsNull(match.State.ActiveBlight, "the blight lasts one roll");
            Assert.That(match.State.Log, Has.Some.Contains("Blight by"));
        }

        [Test]
        public void Blight_BlanksTheTargetsProductionOnThatHex()
        {
            // Search seeds for a roll that hits the blighted hex (deterministic dice).
            for (int seed = 1460; seed < 1560; seed++)
            {
                var match = WithCard(seed, MatchSabotageKind.Blight);
                var probe = MatchController.Replay(match.State.Config, match.CommandLog);
                probe.State.ActiveTrade = null;
                Assert.IsTrue(probe.Apply(MatchCommand.Simple(MatchCommandType.EndTurn, 0)).Ok);
                if (probe.State.Phase != MatchPhase.Roll)
                    continue;
                Assert.IsTrue(probe.Apply(MatchCommand.Simple(MatchCommandType.RollDice, 1)).Ok);
                int roll = probe.State.LastRoll;
                var hit = match.BlightTiles(1).Where(h => match.State.Board.Tiles[h].NumberToken == roll
                                                         && match.State.Board.RobberTile != h).ToList();
                if (roll == 7 || hit.Count == 0)
                    continue;
                // Only blight when the target's whole take of that roll comes from this one hex.
                var hex = hit[0];
                Assert.IsTrue(match.Apply(MatchCommand.Sabotage(0, MatchSabotageKind.Blight, 1, hex)).Ok);
                match.State.ActiveTrade = null;
                Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.EndTurn, 0)).Ok);
                var before = match.State.Seats[1].Hand;
                var res = match.State.Board.Tiles[hex].Resource;
                var probeGain = probe.State.Seats[1].Hand[res] - before[res];
                Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.RollDice, 1)).Ok);
                Assert.AreEqual(roll, match.State.LastRoll);
                int gain = match.State.Seats[1].Hand[res] - before[res];
                Assert.Less(gain, probeGain, $"seed {seed}: the blighted hex paid nothing to the target");
                return;
            }

            Assert.Inconclusive("no seed rolled onto a target tile");
        }

        [Test]
        public void WashedOutRoad_ImmediateAndAttributed_RepairedAtNight()
        {
            var match = WithCard(1471, MatchSabotageKind.WashedOutRoad);
            var road = match.WashableRoads(1).First();
            int len = RouteCalculator.LongestRoadLength(match.State.Board, PlayerId.Ai);
            Assert.IsTrue(match.Apply(MatchCommand.SabotageRoad(0, 1, road)).Ok);
            Assert.IsTrue(match.State.Board.DisabledRoads.Contains(road));
            Assert.LessOrEqual(RouteCalculator.LongestRoadLength(match.State.Board, PlayerId.Ai), len);
            Assert.AreEqual(0, match.State.LastSabotageReveal.Author, "visible attribution");
            Assert.That(match.State.Log.Last(), Does.Contain(match.State.Seats[0].Name));
            StepTo(match, () => match.State.WashedRoads.Count == 0);
            Assert.IsFalse(match.State.Board.DisabledRoads.Contains(road));
        }

        // ---------- Chaos ----------

        [Test]
        public void Chaos_RobberJumps_EveryonePassesOneCardLeft()
        {
            var match = AtMain(4, 1481);
            int last = 3;
            StepTo(match, () => match.State.Phase == MatchPhase.Main && match.State.CurrentSeat == last);
            match.State.ActiveTrade = null;
            match.State.Seats[0].Hand = Cards(wood: 2);
            match.State.Seats[1].Hand = Cards(brick: 1);
            match.State.Seats[2].Hand = ResourceBundle.Zero;
            match.State.Seats[3].Hand = Cards(stone: 1);
            var robber = match.State.Board.RobberTile;
            match.State.EventDeck.Add(EventId.Chaos);
            Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.EndTurn, last)).Ok);
            Assert.AreNotEqual(robber, match.State.Board.RobberTile);
            Assert.AreEqual(Cards(wood: 1, stone: 1), match.State.Seats[0].Hand);
            Assert.AreEqual(Cards(wood: 1), match.State.Seats[1].Hand);
            Assert.AreEqual(Cards(brick: 1), match.State.Seats[2].Hand);
            Assert.AreEqual(ResourceBundle.Zero, match.State.Seats[3].Hand);
        }

        // ---------- determinism + AI ----------

        [Test]
        public void AiParty_UsesEveryShenanigan_AndReplaysToTheSameHash()
        {
            var seen = new HashSet<MatchCommandType>();
            for (int seed = 1; seed <= 24; seed++)
            {
                int n = 3 + seed % 3;
                var config = MatchConfig.Create(n, 0, seed, partyRules: true);
                var match = new MatchController(config);
                MatchRunner.RunToEnd(match, 600);
                foreach (var c in match.CommandLog)
                    seen.Add(c.Type);
                var replay = MatchController.Replay(config, MatchCommand.LogFromJson(MatchCommand.LogToJson(match.CommandLog)));
                Assert.AreEqual(match.State.ComputeHash(), replay.State.ComputeHash(), $"seed {seed}");
            }

            foreach (var t in new[]
                     {
                         MatchCommandType.CastVote, MatchCommandType.PlaceBid, MatchCommandType.PostBounty,
                         MatchCommandType.BuySabotage, MatchCommandType.PlaySabotage, MatchCommandType.GuessSaboteur,
                         MatchCommandType.RerollToken
                     })
                Assert.Contains(t, seen.ToList(), $"the AI never used {t}");
        }

        [Test]
        public void ShenaniganState_IsInTheHash()
        {
            var a = AtMain(3, 1491);
            var b = AtMain(3, 1491);
            Assert.AreEqual(a.State.ComputeHash(), b.State.ComputeHash());
            a.State.Seats[0].Hand = Cards(sheep: 1);
            b.State.Seats[0].Hand = Cards(sheep: 1);
            Assert.IsTrue(a.Apply(MatchCommand.Bounty(0, 1, Cards(sheep: 1))).Ok);
            Assert.AreNotEqual(a.State.ComputeHash(), b.State.ComputeHash());
            b.State.Seats[0].Sabotage.Add(MatchSabotageKind.Blight);
            Assert.AreNotEqual(a.State.ComputeHash(), b.State.ComputeHash());
        }
    }
}
