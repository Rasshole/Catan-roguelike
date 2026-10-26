using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Match;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    /// <summary>v135: harbours, development cards, manual discard on 7, player trading (offer / counter board).</summary>
    public class MatchRulesV135Tests
    {
        private static MatchController AfterSetup(int seats, int humans, int seed)
        {
            var match = new MatchController(MatchConfig.Create(seats, humans, seed));
            while (match.IsSetup)
                Assert.IsTrue(match.Apply(MatchAi.ChooseCommand(match)).Ok);
            return match;
        }

        private static void ToMain(MatchController match, int seat)
        {
            match.State.CurrentSeat = seat;
            match.State.Phase = MatchPhase.Main;
        }

        private static ResourceBundle Cards(int wood = 0, int brick = 0, int wheat = 0, int sheep = 0, int stone = 0) =>
            new() { Wood = wood, Brick = brick, Wheat = wheat, Sheep = sheep, Stone = stone };

        [Test]
        public void Harbours_AreDiscovered_AndGiveTwoToOneOrThreeToOne()
        {
            var match = new MatchController(MatchConfig.Create(4, 4, 3));
            Assert.AreEqual(9, match.State.Ports.Count, "Large board: 5 specific + 4 generic harbours");
            var spots = match.ValidSetupSettlements().ToList();
            var port = match.State.Ports.First(p => p.SpecificResource.HasValue && spots.Contains(p.Vertex));
            var res = port.SpecificResource.Value;
            Assert.AreEqual(4, match.BankRatio(0, res));
            Assert.IsTrue(match.Apply(MatchCommand.AtVertex(MatchCommandType.PlaceSetupSettlement, 0, port.Vertex)).Ok);
            Assert.AreEqual(2, match.BankRatio(0, res));
            while (match.IsSetup)
                Assert.IsTrue(match.Apply(MatchAi.ChooseCommand(match)).Ok);

            ToMain(match, 0);
            var other = MatchController.AllResources.First(r => r != res);
            var hand = Cards();
            hand.Add(res, 2);
            match.State.Seats[0].Hand = hand;
            Assert.IsTrue(match.Apply(MatchCommand.Trade(0, res, other)).Ok, "2:1 at a matching harbour");
            Assert.AreEqual(0, match.State.Seats[0].Hand[res]);
            Assert.AreEqual(1, match.State.Seats[0].Hand[other]);
        }

        [TestCase(4, 25, 14, 2)]
        [TestCase(5, 34, 20, 3)]
        public void DevDeck_HasClassicComposition(int seats, int total, int knights, int each)
        {
            var deck = new MatchController(MatchConfig.Create(seats, 1, 5)).State.DevDeck;
            Assert.AreEqual(total, deck.Count);
            Assert.AreEqual(knights, deck.Count(c => c == MatchDevCard.Knight));
            Assert.AreEqual(5, deck.Count(c => c == MatchDevCard.VictoryPoint));
            Assert.AreEqual(each, deck.Count(c => c == MatchDevCard.RoadBuilding));
            Assert.AreEqual(each, deck.Count(c => c == MatchDevCard.YearOfPlenty));
            Assert.AreEqual(each, deck.Count(c => c == MatchDevCard.Monopoly));
        }

        [Test]
        public void Knight_BoughtThisTurn_WaitsAFullTurn_ThenMovesTheRobber_OnePerTurn()
        {
            var match = AfterSetup(3, 3, 8);
            ToMain(match, 0);
            match.State.DevDeck.Add(MatchDevCard.Knight);
            match.State.Seats[0].Hand = Cards(wheat: 1, sheep: 1, stone: 1);
            Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.BuyDevCard, 0)).Ok);
            Assert.AreEqual(ResourceBundle.Zero.Total, match.State.Seats[0].Hand.Total);
            Assert.IsFalse(match.Apply(MatchCommand.Simple(MatchCommandType.PlayKnight, 0)).Ok, "bought this turn");
            Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.EndTurn, 0)).Ok);
            CollectionAssert.Contains(match.State.Seats[0].DevCards, MatchDevCard.Knight);

            match.State.Seats[0].DevCards.Add(MatchDevCard.Knight);
            match.State.CurrentSeat = 0;
            match.State.Phase = MatchPhase.Roll;
            Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.PlayKnight, 0)).Ok, "knight before rolling");
            Assert.AreEqual(MatchPhase.MoveRobber, match.State.Phase);
            Assert.IsTrue(match.Apply(MatchAi.ChooseCommand(match)).Ok);
            Assert.AreEqual(MatchPhase.Roll, match.State.Phase, "back to the roll after the knight");
            Assert.AreEqual(1, match.State.Seats[0].KnightsPlayed);
            Assert.IsFalse(match.Apply(MatchCommand.Simple(MatchCommandType.PlayKnight, 0)).Ok, "one card per turn");
        }

        [Test]
        public void LargestArmy_ThirdKnight_IsWorthTwoPoints()
        {
            var match = AfterSetup(3, 3, 9);
            ToMain(match, 1);
            var seat = match.State.Seats[1];
            seat.KnightsPlayed = 2;
            seat.DevCards.Add(MatchDevCard.Knight);
            int before = seat.VictoryPoints;
            Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.PlayKnight, 1)).Ok);
            Assert.IsTrue(match.Apply(MatchAi.ChooseCommand(match)).Ok);
            Assert.AreEqual(1, match.State.LargestArmySeat);
            Assert.AreEqual(before + MatchController.LargestArmyPoints, seat.VictoryPoints);
        }

        [Test]
        public void VictoryPointCard_CountsImmediately_ButStaysHidden()
        {
            var match = AfterSetup(3, 3, 10);
            ToMain(match, 0);
            match.State.DevDeck.Add(MatchDevCard.VictoryPoint);
            match.State.Seats[0].Hand = Cards(wheat: 1, sheep: 1, stone: 1);
            int before = match.State.Seats[0].VictoryPoints;
            Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.BuyDevCard, 0)).Ok);
            Assert.AreEqual(before + 1, match.State.Seats[0].VictoryPoints);
            Assert.AreEqual(before, match.State.Seats[0].PublicVictoryPoints);
        }

        [Test]
        public void RoadBuilding_PlacesTwoFreeRoads()
        {
            var match = AfterSetup(3, 3, 11);
            ToMain(match, 0);
            match.State.Seats[0].DevCards.Add(MatchDevCard.RoadBuilding);
            match.State.Seats[0].Hand = Cards();
            int roads = match.CountRoads(0);
            Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.PlayRoadBuilding, 0)).Ok);
            Assert.AreEqual(MatchPhase.RoadBuilding, match.State.Phase);
            for (int i = 0; i < 2; i++)
                Assert.IsTrue(match.Apply(MatchCommand.AtEdge(MatchCommandType.BuildRoad, 0, match.ValidRoads(0).First())).Ok);
            Assert.AreEqual(roads + 2, match.CountRoads(0));
            Assert.AreEqual(MatchPhase.Main, match.State.Phase);
        }

        [Test]
        public void YearOfPlenty_And_Monopoly()
        {
            var match = AfterSetup(3, 3, 12);
            ToMain(match, 0);
            var me = match.State.Seats[0];
            me.Hand = Cards();
            me.DevCards.Add(MatchDevCard.YearOfPlenty);
            me.DevCards.Add(MatchDevCard.Monopoly);
            Assert.IsTrue(match.Apply(MatchCommand.Card(MatchCommandType.PlayYearOfPlenty, 0, ResourceType.Wood, ResourceType.Stone)).Ok);
            Assert.AreEqual(1, me.Hand.Wood);
            Assert.AreEqual(1, me.Hand.Stone);

            match.Apply(MatchCommand.Simple(MatchCommandType.EndTurn, 0));
            ToMain(match, 0);
            match.State.Seats[1].Hand = Cards(wheat: 3, wood: 1);
            match.State.Seats[2].Hand = Cards(wheat: 2);
            Assert.IsTrue(match.Apply(MatchCommand.Card(MatchCommandType.PlayMonopoly, 0, ResourceType.Wheat, ResourceType.Wheat)).Ok);
            Assert.AreEqual(5, me.Hand.Wheat);
            Assert.AreEqual(0, match.State.Seats[1].Hand.Wheat);
            Assert.AreEqual(1, match.State.Seats[1].Hand.Wood);
        }

        /// <summary>Seeds until the current seat rolls a 7 with seats 1 and 2 over the hand limit.</summary>
        private static MatchController SevenWithDiscards(int humans)
        {
            for (int seed = 1; seed < 400; seed++)
            {
                var match = AfterSetup(3, humans, seed);
                match.State.Seats[0].Hand = Cards(wood: 2);
                match.State.Seats[1].Hand = Cards(wood: 5, brick: 4);
                match.State.Seats[2].Hand = Cards(wheat: 8);
                Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.RollDice, match.State.CurrentSeat)).Ok);
                if (match.State.LastRoll == 7)
                    return match;
            }

            Assert.Fail("no 7 found");
            return null;
        }

        [Test]
        public void Seven_EachOverLimitSeat_PicksItsOwnDiscard()
        {
            var match = SevenWithDiscards(3);
            Assert.AreEqual(MatchPhase.Discard, match.State.Phase);
            Assert.AreEqual(4, match.State.PendingDiscards[1]);
            Assert.AreEqual(4, match.State.PendingDiscards[2]);
            Assert.AreEqual(1, match.State.ActingSeat);
            Assert.IsFalse(match.Apply(MatchCommand.DiscardCards(1, Cards(wood: 3))).Ok, "wrong count");
            Assert.IsFalse(match.Apply(MatchCommand.DiscardCards(1, Cards(stone: 4))).Ok, "cards not held");
            Assert.IsFalse(match.Apply(MatchCommand.Simple(MatchCommandType.EndTurn, match.State.CurrentSeat)).Ok);
            Assert.IsTrue(match.Apply(MatchCommand.DiscardCards(1, Cards(brick: 4))).Ok);
            Assert.AreEqual(Cards(wood: 5).Total, match.State.Seats[1].Hand.Total);
            Assert.AreEqual(2, match.State.ActingSeat);
            Assert.IsTrue(match.Apply(MatchCommand.DiscardCards(2, Cards(wheat: 4))).Ok);
            Assert.AreEqual(MatchPhase.MoveRobber, match.State.Phase);
            Assert.AreEqual(match.State.CurrentSeat, match.State.ActingSeat);
        }

        [Test]
        public void HotSeat_Discard_IsBehindAPassScreen_PerPlayer()
        {
            HotSeatSession session = null;
            for (int seed = 1; seed < 400 && session == null; seed++)
            {
                var s = new HotSeatSession(MatchConfig.Create(3, 3, seed));
                int guard = 0;
                while (s.Match.IsSetup && guard++ < 100)
                {
                    if (s.PassScreenPending)
                        s.ConfirmPass();
                    s.ApplySuggested();
                }

                if (s.PassScreenPending)
                    s.ConfirmPass();
                if (s.State.CurrentSeat != 0)
                    continue;
                s.State.Seats[0].Hand = Cards(wood: 2);
                s.State.Seats[1].Hand = Cards(wood: 5, brick: 4);
                s.State.Seats[2].Hand = Cards(wheat: 8);
                s.Roll();
                if (s.State.LastRoll == 7)
                    session = s;
            }

            Assert.IsNotNull(session);
            Assert.IsTrue(session.PassScreenPending, "device goes to the first discarder");
            Assert.AreEqual(1, session.State.ActingSeat);
            Assert.IsFalse(session.IsHandVisible(1));
            session.ConfirmPass();
            Assert.IsTrue(session.IsHandVisible(1));
            Assert.IsFalse(session.IsHandVisible(0));
            Assert.IsTrue(session.Apply(MatchCommand.DiscardCards(1, Cards(wood: 4))).Ok);
            Assert.IsTrue(session.PassScreenPending, "then to the second discarder");
            Assert.IsFalse(session.IsHandVisible(1));
            session.ConfirmPass();
            Assert.IsTrue(session.Apply(MatchCommand.DiscardCards(2, Cards(wheat: 4))).Ok);
            Assert.IsTrue(session.PassScreenPending, "and back to the roller for the robber");
            Assert.AreEqual(0, session.State.ActingSeat);
        }

        [Test]
        public void AiDiscards_AreAutomatic_InAHumanTurn()
        {
            var match = SevenWithDiscards(1);
            MatchRunner.AdvanceAi(match);
            Assert.AreEqual(0, match.State.PendingDiscards.Count);
            Assert.AreEqual(5, match.State.Seats[1].Hand.Total);
            Assert.AreEqual(4, match.State.Seats[2].Hand.Total);
        }

        [Test]
        public void Trade_OfferAcceptConfirm_SwapsCards()
        {
            var session = new HotSeatSession(MatchConfig.Create(3, 1, 21));
            while (session.Match.IsSetup)
                session.ApplySuggested();
            ToMain(session.Match, 0);
            session.State.Seats[0].Hand = Cards(wood: 2);
            session.State.Seats[1].Hand = Cards(brick: 2);
            session.State.Seats[2].Hand = Cards();
            Assert.IsTrue(session.Apply(MatchCommand.Propose(0, Cards(wood: 1), Cards(brick: 1))).Ok);
            var trade = session.State.ActiveTrade;
            Assert.AreEqual(TradeResponseKind.Accepted, trade.Responses[1].Kind, "AI with the cards accepts");
            Assert.AreEqual(TradeResponseKind.Declined, trade.Responses[2].Kind, "AI without them declines");
            Assert.IsFalse(session.Apply(MatchCommand.Confirm(0, 2)).Ok);
            Assert.IsTrue(session.Apply(MatchCommand.Confirm(0, 1)).Ok);
            Assert.IsNull(session.State.ActiveTrade);
            Assert.AreEqual(1, session.State.Seats[0].Hand.Wood);
            Assert.AreEqual(1, session.State.Seats[0].Hand.Brick);
            Assert.AreEqual(1, session.State.Seats[1].Hand.Wood);
            Assert.AreEqual(1, session.State.Seats[1].Hand.Brick);
        }

        [Test]
        public void Trade_CounterOffer_IsWhatGetsExecuted()
        {
            var match = AfterSetup(3, 3, 22);
            ToMain(match, 0);
            match.State.Seats[0].Hand = Cards(wood: 3);
            match.State.Seats[1].Hand = Cards(stone: 1);
            Assert.IsTrue(match.Apply(MatchCommand.Propose(0, Cards(wood: 1), Cards(stone: 1))).Ok);
            Assert.IsFalse(match.Apply(MatchCommand.Respond(0, TradeResponseKind.Accepted)).Ok, "offerer can't answer");
            Assert.IsTrue(match.Apply(MatchCommand.Respond(1, TradeResponseKind.Countered, Cards(wood: 2), Cards(stone: 1))).Ok);
            Assert.IsTrue(match.Apply(MatchCommand.Respond(2, TradeResponseKind.Declined)).Ok);
            Assert.IsFalse(match.Apply(MatchCommand.Simple(MatchCommandType.BuyDevCard, 0)).Ok, "building waits for the trade");
            Assert.IsTrue(match.Apply(MatchCommand.Confirm(0, 1)).Ok);
            Assert.AreEqual(1, match.State.Seats[0].Hand.Wood);
            Assert.AreEqual(1, match.State.Seats[0].Hand.Stone);
            Assert.AreEqual(2, match.State.Seats[1].Hand.Wood);
        }

        [Test]
        public void Trade_LapsesAtEndOfTurn_AndCanBeCancelled()
        {
            var match = AfterSetup(3, 3, 23);
            ToMain(match, 0);
            match.State.Seats[0].Hand = Cards(wood: 1);
            Assert.IsTrue(match.Apply(MatchCommand.Propose(0, Cards(wood: 1), Cards(brick: 1))).Ok);
            Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.CancelTrade, 0)).Ok);
            Assert.IsNull(match.State.ActiveTrade);
            Assert.IsTrue(match.Apply(MatchCommand.Propose(0, Cards(wood: 1), Cards(brick: 1))).Ok);
            Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.EndTurn, 0)).Ok);
            Assert.IsNull(match.State.ActiveTrade);
        }

        [Test]
        public void BoardClick_PlacesSetupPieces_Builds_AndMovesTheRobber()
        {
            var match = new MatchController(MatchConfig.Create(3, 3, 41));
            const float scale = 1.7f;
            var spot = match.ValidSetupSettlements().First();
            var (vx, vz) = HexMath.VertexToWorldPosition(spot, scale);
            Assert.IsNull(MatchBoardPick.Pick(match, MatchBuildMode.Auto, vx + 5f * scale, vz, scale), "a click in the sea");
            var click = MatchBoardPick.Pick(match, MatchBuildMode.Auto, vx + 0.1f, vz - 0.1f, scale);
            Assert.AreEqual(MatchCommandType.PlaceSetupSettlement, click.Type);
            Assert.AreEqual(spot, click.Vertex);
            Assert.IsTrue(match.Apply(click).Ok);
            var road = match.ValidSetupRoads().First();
            var (mx, mz) = HexMath.EdgeMidpoint(road, scale);
            var roadClick = MatchBoardPick.Pick(match, MatchBuildMode.Auto, mx, mz, scale);
            Assert.AreEqual(MatchCommandType.PlaceSetupRoad, roadClick.Type);
            Assert.IsTrue(match.Apply(roadClick).Ok);
            while (match.IsSetup)
                Assert.IsTrue(match.Apply(MatchAi.ChooseCommand(match)).Ok);

            ToMain(match, 0);
            match.State.Seats[0].Hand = Cards(wood: 1, brick: 1);
            var target = match.ValidRoads(0).First();
            (mx, mz) = HexMath.EdgeMidpoint(target, scale);
            var build = MatchBoardPick.Pick(match, MatchBuildMode.Auto, mx, mz, scale);
            Assert.AreEqual(MatchCommandType.BuildRoad, build.Type);
            Assert.IsTrue(match.Apply(build).Ok);
            Assert.IsNull(MatchBoardPick.Pick(match, MatchBuildMode.Auto, mx, mz, scale), "can't afford another");

            match.State.Phase = MatchPhase.MoveRobber;
            var hex = match.State.Board.Tiles.Keys.First(h => h != match.State.Board.RobberTile);
            var (hx, hz) = HexMath.ToWorldPosition(hex, scale);
            var robber = MatchBoardPick.Pick(match, MatchBuildMode.Auto, hx, hz, scale);
            Assert.AreEqual(MatchCommandType.MoveRobber, robber.Type);
            Assert.AreEqual(hex, robber.Hex);
            Assert.IsTrue(match.Apply(robber).Ok);
        }

        [TestCase(4, 31)]
        [TestCase(5, 32)]
        public void AllAiMatch_WithFullRules_FinishesAndReplays(int seats, int seed)
        {
            var match = new MatchController(MatchConfig.Create(seats, 0, seed));
            MatchRunner.RunToEnd(match);
            Assert.IsTrue(match.IsOver);
            Assert.Less(match.State.DevDeck.Count, seats >= 5 ? 34 : 25, "AI buys development cards");
            var replay = MatchController.Replay(match.State.Config, match.CommandLog);
            Assert.AreEqual(match.State.ComputeHash(), replay.State.ComputeHash());
        }

        [Test]
        public void Commands_RoundTripThroughJson_WithBundles()
        {
            var c = MatchCommand.Respond(2, TradeResponseKind.Countered, Cards(wood: 2), Cards(stone: 1));
            Assert.AreEqual(c, MatchCommand.FromJson(c.ToJson()));
        }
    }
}
