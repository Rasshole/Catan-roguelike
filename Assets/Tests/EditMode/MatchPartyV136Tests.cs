using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Civilizations;
using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Match;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    /// <summary>v136 pass-and-play step 4: civ pick, secret quests + traits, match events, robber victim picker.</summary>
    public class MatchPartyV136Tests
    {
        private static MatchController Party(int seats, int seed, int humans = 0) =>
            new(MatchConfig.Create(seats, humans, seed, partyRules: true));

        private static void Step(MatchController match) => Assert.IsTrue(match.Apply(MatchAi.ChooseCommand(match)).Ok);

        private static MatchController ToQuestPick(int seats, int seed)
        {
            var match = Party(seats, seed);
            while (match.State.Phase != MatchPhase.QuestPick)
                Step(match);
            return match;
        }

        private static MatchController ToFirstRoll(int seats, int seed)
        {
            var match = ToQuestPick(seats, seed);
            while (match.State.IsPicking)
                Step(match);
            return match;
        }

        [Test]
        public void ClassicMatch_WithoutPartyRules_StartsAtPlacement()
        {
            var match = new MatchController(MatchConfig.Create(4, 1, 3));
            Assert.AreEqual(MatchPhase.SetupSettlement, match.State.Phase);
            Assert.AreEqual(MatchController.SettlementCost.Total, match.SettlementCostFor(0).Total);
        }

        [TestCase(4, 11)]
        [TestCase(5, 12)]
        public void CivPick_EachSeatInOrder_PicksOneOfThree_Unique_BeforePlacement(int seats, int seed)
        {
            var match = Party(seats, seed);
            Assert.AreEqual(MatchPhase.CivPick, match.State.Phase);
            var picked = new List<CivilizationId>();
            for (int i = 0; i < seats; i++)
            {
                Assert.AreEqual(i, match.State.ActingSeat, "civs are picked in seat order");
                var offer = match.State.Seats[i].CivOffer;
                Assert.AreEqual(3, offer.Count);
                Assert.AreEqual(3, offer.Distinct().Count());
                CollectionAssert.IsEmpty(offer.Intersect(picked), "a picked civ is never offered again");
                Assert.IsFalse(match.Apply(MatchCommand.PickCiv(i, CivilizationLibrary.Pool
                    .Select(c => c.Id).First(c => !offer.Contains(c)))).Ok, "only an offered civ");
                var choice = offer[i % 3];
                Assert.IsTrue(match.Apply(MatchCommand.PickCiv(i, choice)).Ok);
                picked.Add(choice);
                Assert.AreEqual(choice, match.State.Seats[i].Civilization);
            }

            Assert.AreEqual(seats, picked.Distinct().Count());
            Assert.AreEqual(MatchPhase.SetupSettlement, match.State.Phase, "placement starts after every civ is chosen");
            Assert.AreEqual(0, match.State.CurrentSeat);
        }

        [TestCase(4, 21)]
        [TestCase(5, 22)]
        public void QuestPick_LastPlacerFirst_MixedOffers_UniquePicks_ThenFirstRoll(int seats, int seed)
        {
            var match = ToQuestPick(seats, seed);
            var quests = new List<MatchQuestId>();
            var traits = new List<MatchTraitId>();
            for (int k = 0; k < seats; k++)
            {
                int seat = seats - 1 - k;
                Assert.AreEqual(seat, match.State.ActingSeat, "reverse placement order: the last placer picks first");
                var me = match.State.Seats[seat];
                Assert.AreEqual(3, me.QuestOffer.Count);
                Assert.AreEqual(2, me.TraitOffer.Count);
                var points = me.QuestOffer.Select(q => MatchPartyLibrary.Quest(q).Points).ToList();
                Assert.Contains(1, points, "an easier 1-VP quest is offered");
                Assert.Contains(2, points, "a harder 2-VP quest is offered");
                CollectionAssert.IsEmpty(me.QuestOffer.Intersect(quests), "quests are unique per match");
                CollectionAssert.IsEmpty(me.TraitOffer.Intersect(traits), "traits are unique per match");
                foreach (var q in me.QuestOffer)
                    Assert.IsFalse(MatchPartyLibrary.Quest(q).IsComplete(match, seat), "never already done");
                var quest = me.QuestOffer[k % 3];
                var trait = me.TraitOffer[k % 2];
                Assert.IsTrue(match.Apply(MatchCommand.PickQuestTrait(seat, quest, trait)).Ok);
                quests.Add(quest);
                traits.Add(trait);
            }

            Assert.AreEqual(MatchPhase.Roll, match.State.Phase);
            Assert.AreEqual(0, match.State.CurrentSeat);
            Assert.AreEqual(1, match.State.TurnNumber);
        }

        [Test]
        public void QuestOffers_FollowBoardPosition()
        {
            // Ore quests only for seats bordering ore; harbour quests only with a free harbour within 3 edges.
            for (int seed = 30; seed < 40; seed++)
            {
                var match = ToQuestPick(4, seed);
                while (match.State.Phase == MatchPhase.QuestPick)
                {
                    int seat = match.State.ActingSeat;
                    foreach (var q in match.State.Seats[seat].QuestOffer)
                    {
                        if (q is MatchQuestId.OreBaron or MatchQuestId.OreEmpire
                            && MatchPartyLibrary.Bordered(match, seat, ResourceType.Stone) == 0)
                        {
                            // Allowed only as a fallback when nothing plausible is left: the 2 plausible picks come first.
                            Assert.AreEqual(match.State.Seats[seat].QuestOffer.Last(), q, $"seed {seed}: ore quest without ore");
                        }
                    }

                    Step(match);
                }
            }
        }

        [Test]
        public void QuestVp_IsHidden_FromPublicScore_ButCountsToWin_AndIsRevealedAtTheEnd()
        {
            var match = ToFirstRoll(4, 41);
            var me = match.State.Seats[0];
            me.Quest = MatchQuestId.Knighthood;
            me.KnightsPlayed = 2;
            int before = me.VictoryPoints;
            Step(match); // roll: quests are checked after every command
            Assert.IsTrue(me.QuestDone);
            Assert.AreEqual(before + 1, me.VictoryPoints);
            Assert.AreEqual(before, me.PublicVictoryPoints, "the seat strip never shows quest VP");

            match.State.Config.VictoryPoints = me.VictoryPoints + 1;
            while (match.State.Phase != MatchPhase.Main) Step(match);
            me.QuestDone = false;
            me.Quest = MatchQuestId.Metropolis; // 2 VP: 2 cities and 5 buildings
            var spots = match.UpgradableSettlements(0).ToList();
            foreach (var v in spots) match.State.Board.VertexBuildings[v] = (BuildingType.City, me.Owner);
            int extra = 5 - spots.Count;
            for (int i = 0; i < extra; i++)
            {
                var v = match.ValidSetupSettlements().First();
                match.State.Board.VertexBuildings[v] = (BuildingType.City, me.Owner);
            }

            Assert.IsFalse(match.IsOver);
            Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.UseExtraTime, 0)).Ok, "any own-turn action");
            Assert.IsTrue(match.IsOver, "hidden quest VP wins the game");
            Assert.AreEqual(0, match.State.Winner);
            StringAssert.Contains("quest: Metropolis (2 VP) – done", string.Join("\n", match.State.Log));
        }

        [Test]
        public void CivAndTraitRules_ChangeCostsRatesAndDiscards()
        {
            var match = ToFirstRoll(4, 51);
            var s = match.State.Seats[1];
            s.Civilization = CivilizationId.SteppeNomads;
            Assert.AreEqual(0, match.SettlementCostFor(1).Brick);
            Assert.AreEqual(2, match.DevCardCostFor(1).Stone, "v142: Steppe development cards cost 1 extra Ore");
            s.Civilization = CivilizationId.Academy;
            Assert.AreEqual(0, match.DevCardCostFor(1).Wheat);
            Assert.AreEqual(2, match.SettlementCostFor(1).Wheat, "Academy settlements cost 1 extra Grain");
            s.Civilization = CivilizationId.Brickwrights;
            s.Trait = MatchTraitId.RoadCrew;
            Assert.AreEqual(0, match.RoadCostFor(1).Wood, "Brickwrights: roads cost no Lumber");
            Assert.AreEqual(match.CountRoads(1) % 2 == 1 ? 0 : 1, match.RoadCostFor(1).Total,
                "v142 Road Crew: every second road is free");
            Assert.AreEqual(2, match.CityCostFor(1).Wheat, "v142: Brickwrights cities cost the normal 2 Grain");
            Assert.AreEqual(2, match.DevCardCostFor(1).Wheat, "v142: Brickwrights development cards cost 1 extra Grain");

            s.Civilization = CivilizationId.SunKingdom;
            s.Trait = MatchTraitId.None;
            var plain = MatchController.AllResources.First(r => match.BankRatio(1, r) == 4);
            s.Trait = MatchTraitId.HarbourPilot;
            Assert.AreEqual(2, match.BankRatio(1, plain), "v142 Harbour Pilot: the first bank trade each turn is 2:1");
            s.BankTradesThisTurn = 1;
            Assert.AreEqual(3, match.BankRatio(1, plain), "v142: later trades one cheaper than 4:1");

            s.Hand = new ResourceBundle { Wood = 8 };
            s.Trait = MatchTraitId.Granary;
            Assert.AreEqual(0, match.DiscardOwed(1), "v142 Granary: 8 cards are safe");
            s.Trait = MatchTraitId.None;
            Assert.AreEqual(4, match.DiscardOwed(1));
            s.Trait = MatchTraitId.ToughHide;
            Assert.AreEqual(0, match.DiscardOwed(1), "v142 Tough Hide: a 7 never costs a card");
        }

        [Test]
        public void EventDeck_FlipsAnEventEachRound_FromRoundTwo()
        {
            var match = ToFirstRoll(5, 61);
            Assert.AreEqual(EventId.None, match.State.ActiveEvent, "no event in round 1");
            var seen = new HashSet<EventId>();
            int guard = 0;
            while (match.State.Round < 6 && !match.IsOver && guard++ < 5000)
            {
                int round = match.State.Round;
                Step(match);
                if (match.State.Round != round)
                {
                    Assert.AreNotEqual(EventId.None, match.State.ActiveEvent);
                    Assert.IsTrue(match.State.Log.Last().StartsWith("Event – ") || match.State.Log.Any(l => l.StartsWith("Event – ")));
                    seen.Add(match.State.ActiveEvent);
                }
            }

            Assert.GreaterOrEqual(seen.Count, 4, "a new card each round");
        }

        [Test]
        public void MarketDay_And_Storm_ApplyForTheRound()
        {
            var match = ToFirstRoll(4, 71);
            match.State.ActiveEvent = EventId.MarketDay;
            Assert.LessOrEqual(MatchController.AllResources.Max(r => match.BankRatio(2, r)), 3);
            var storm = match.State.Board.Tiles.Values.First(t => !t.IsDesert && t.NumberToken.HasValue
                && Enumerable.Range(0, 6).Any(c => match.State.Board.VertexBuildings.ContainsKey(
                    VertexGraph.Canonicalize(new HexMath.Vertex(t.Coord, c)))));
            storm.HasRobber = false;
            match.State.ActiveEvent = EventId.Storm;
            match.State.EventTile = storm.Coord;
            int before = match.State.Seats.Sum(s => s.CardCount);
            // Roll until the storm hex's number comes up: the storm hex adds nothing.
            for (int i = 0; i < 200 && match.State.LastRoll != storm.NumberToken; i++)
            {
                match.State.Phase = MatchPhase.Roll;
                match.State.PendingDiscards.Clear();
                foreach (var s in match.State.Seats) s.Hand = ResourceBundle.Zero;
                before = 0;
                match.Apply(MatchCommand.Simple(MatchCommandType.RollDice, match.State.CurrentSeat));
            }

            Assert.AreEqual(storm.NumberToken, match.State.LastRoll);
            int stormHits = 0;
            foreach (var other in match.State.Board.Tiles.Values.Where(t => t.NumberToken == storm.NumberToken && t.Coord != storm.Coord && !t.HasRobber))
                stormHits += Enumerable.Range(0, 6).Count(c => match.State.Board.VertexBuildings.ContainsKey(
                    VertexGraph.Canonicalize(new HexMath.Vertex(other.Coord, c))));
            if (stormHits == 0)
                Assert.AreEqual(before, match.State.Seats.Sum(s => s.CardCount), "the storm hex produced nothing");
        }

        [TestCase(4, 81)]
        [TestCase(5, 82)]
        public void PartyMatch_AllAi_FinishesAndReplaysDeterministically(int seats, int seed)
        {
            var config = MatchConfig.Create(seats, 0, seed, partyRules: true);
            var match = new MatchController(config);
            MatchRunner.RunToEnd(match);
            Assert.IsTrue(match.IsOver);
            Assert.IsTrue(match.State.Seats.All(s => s.Civilization != CivilizationId.None && s.Quest != MatchQuestId.None));
            Assert.AreEqual(seats, match.State.Log.Count(l => l.Contains("'s quest: ")), "game over reveals every quest");
            var replay = MatchController.Replay(config, MatchCommand.LogFromJson(MatchCommand.LogToJson(match.CommandLog)));
            Assert.AreEqual(match.State.ComputeHash(), replay.State.ComputeHash());
        }

        [Test]
        public void HotSeat_PartyPicks_ArePrivate_BehindPassScreens()
        {
            var session = new HotSeatSession(MatchConfig.Create(3, 3, 91, partyRules: true));
            Assert.AreEqual(MatchPhase.CivPick, session.State.Phase);
            Assert.IsTrue(session.PassScreenPending, "first picker gets a pass screen");
            Assert.IsFalse(session.IsHandVisible(0));
            session.ConfirmPass();
            Assert.IsTrue(session.IsHandVisible(0));
            Assert.IsTrue(session.ApplySuggested().Ok);
            Assert.AreEqual(1, session.State.ActingSeat);
            Assert.IsTrue(session.PassScreenPending, "the next picker gets the device behind a pass screen");
        }

        [Test]
        public void RobberClick_OnHexWithTwoVictims_AsksWhomToRob()
        {
            var match = new MatchController(MatchConfig.Create(4, 4, 101));
            while (match.IsSetup) Step(match);
            var board = match.State.Board;
            var hex = board.Tiles.Keys.Where(h => h != board.RobberTile)
                .OrderBy(h => h.Q).ThenBy(h => h.R).First(h => Enumerable.Range(0, 6)
                    .All(c => !board.VertexBuildings.ContainsKey(VertexGraph.Canonicalize(new HexMath.Vertex(h, c)))));
            board.VertexBuildings[VertexGraph.Canonicalize(new HexMath.Vertex(hex, 0))] = (BuildingType.Settlement, MatchSeats.ToPlayerId(1));
            board.VertexBuildings[VertexGraph.Canonicalize(new HexMath.Vertex(hex, 3))] = (BuildingType.Settlement, MatchSeats.ToPlayerId(2));
            match.State.Seats[1].Hand = new ResourceBundle { Wood = 1 };
            match.State.Seats[2].Hand = new ResourceBundle { Brick = 2 };
            match.State.CurrentSeat = 0;
            match.State.Phase = MatchPhase.MoveRobber;

            var (x, z) = HexMath.ToWorldPosition(hex, 1f);
            var click = MatchBoardPick.Pick(match, MatchBuildMode.Auto, x, z, 1f);
            Assert.AreEqual(MatchBoardPick.VictimPending, click.Victim, "two victims: the player chooses");
            Assert.IsFalse(match.Apply(click).Ok);
            CollectionAssert.AreEqual(new[] { 1, 2 }, match.RobberVictims(0, hex).ToArray());
            Assert.IsTrue(match.Apply(MatchCommand.Robber(0, hex, 1)).Ok);
            Assert.AreEqual(0, match.State.Seats[1].CardCount, "the chosen victim was robbed");
            Assert.AreEqual(2, match.State.Seats[2].CardCount);
        }
    }
}
