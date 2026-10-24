using System;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Match;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    /// <summary>v134 Phase 1: N-player match core (2–5 seats, any human/AI mix, serializable commands).</summary>
    public class MatchCoreV134Tests
    {
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void Config_AcceptsTwoToFiveSeats(int seats)
        {
            var config = MatchConfig.Create(seats, 1, 7);
            Assert.AreEqual(seats, config.SeatCount);
            Assert.AreEqual(1, config.HumanCount);
            Assert.AreEqual(seats - 1, config.AiCount);
            Assert.AreEqual(seats >= 5 ? MapSize.Extended : MapSize.Large, config.MapSize);
        }

        [Test]
        public void Config_RejectsOneOrSixSeats()
        {
            Assert.Throws<ArgumentException>(() => MatchConfig.Create(1, 1, 1));
            Assert.Throws<ArgumentException>(() => MatchConfig.Create(6, 1, 1));
        }

        [Test]
        public void Config_ThreeHumansPlusOneAi_IsValid_AndHumansComeFirst()
        {
            var config = MatchConfig.Create(4, 3, 11);
            Assert.AreEqual(3, config.HumanCount);
            Assert.AreEqual(1, config.AiCount);
            Assert.AreEqual(SeatKind.Ai, config.Seats[3].Kind);
            var match = new MatchController(config);
            Assert.AreEqual(4, match.State.Seats.Count);
            Assert.IsFalse(match.State.Seats[0].IsAi);
            Assert.IsTrue(match.State.Seats[3].IsAi);
        }

        [Test]
        public void Config_FillEmptySeatsWithAi_ReplacesNullSeats()
        {
            var config = new MatchConfig { Seed = 3 };
            config.Seats.Add(new SeatConfig(SeatKind.Human, "Rasmus"));
            config.Seats.Add(null);
            config.Seats.Add(new SeatConfig(SeatKind.Human, "Guest"));
            config.Seats.Add(null);
            Assert.Throws<ArgumentException>(() => config.Validate());
            config.FillEmptySeatsWithAi();
            Assert.DoesNotThrow(() => config.Validate());
            Assert.AreEqual(2, config.HumanCount);
            Assert.AreEqual(2, config.AiCount);
        }

        [Test]
        public void Commands_FromWrongSeat_AreRejected_AndNotLogged()
        {
            var match = new MatchController(MatchConfig.Create(3, 3, 5));
            var spot = match.ValidSetupSettlements().First();
            var result = match.Apply(MatchCommand.AtVertex(MatchCommandType.PlaceSetupSettlement, 1, spot));
            Assert.IsFalse(result.Ok);
            Assert.AreEqual(0, match.CommandLog.Count);
            Assert.IsTrue(match.Apply(MatchCommand.AtVertex(MatchCommandType.PlaceSetupSettlement, 0, spot)).Ok);
            Assert.AreEqual(1, match.CommandLog.Count);
        }

        [Test]
        public void Setup_IsSnakeOrder()
        {
            var match = new MatchController(MatchConfig.Create(4, 0, 9));
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 3, 2, 1, 0 }, match.State.SetupOrder);
        }

        [Test]
        public void Command_JsonRoundTrip_PreservesEveryField()
        {
            var original = MatchCommand.Robber(2, new HexCoord(1, -2), 4);
            var copy = MatchCommand.FromJson(original.ToJson());
            Assert.AreEqual(original, copy);
            Assert.AreEqual(4, copy.Victim);

            var trade = MatchCommand.Trade(1, ResourceType.Wood, ResourceType.Stone);
            var tradeCopy = MatchCommand.FromJson(trade.ToJson());
            Assert.AreEqual(ResourceType.Wood, tradeCopy.Give);
            Assert.AreEqual(ResourceType.Stone, tradeCopy.Receive);
        }

        [Test]
        public void SeededFiveAiGame_FinishesDeterministically_AndReplaysFromJson()
        {
            var config = MatchConfig.Create(5, 0, 42);
            var a = new MatchController(config);
            MatchRunner.RunToEnd(a, 1500);
            Assert.IsTrue(a.IsOver, "5-AI game should end with a winner");
            Assert.IsTrue(a.State.Winner.HasValue);
            Assert.GreaterOrEqual(a.State.Seats[a.State.Winner.Value].VictoryPoints, config.VictoryPoints);

            var b = new MatchController(MatchConfig.Create(5, 0, 42));
            MatchRunner.RunToEnd(b, 1500);
            Assert.AreEqual(a.State.ComputeHash(), b.State.ComputeHash());
            Assert.AreEqual(MatchCommand.LogToJson(a.CommandLog), MatchCommand.LogToJson(b.CommandLog));

            var replay = MatchController.Replay(config, MatchCommand.LogFromJson(MatchCommand.LogToJson(a.CommandLog)));
            Assert.AreEqual(a.State.ComputeHash(), replay.State.ComputeHash());
            Assert.AreEqual(a.State.Winner, replay.State.Winner);
        }

        [TestCase(2, 3)]
        [TestCase(3, 4)]
        [TestCase(4, 5)]
        public void SeededAllAiGames_FinishForEverySeatCount(int seats, int seed)
        {
            var match = new MatchController(MatchConfig.Create(seats, 0, seed));
            MatchRunner.RunToEnd(match, 1500);
            Assert.IsTrue(match.IsOver);
        }

        [Test]
        public void DifferentSeeds_GiveDifferentGames()
        {
            var a = new MatchController(MatchConfig.Create(4, 0, 1));
            var b = new MatchController(MatchConfig.Create(4, 0, 2));
            MatchRunner.RunToEnd(a, 1500);
            MatchRunner.RunToEnd(b, 1500);
            Assert.AreNotEqual(a.State.ComputeHash(), b.State.ComputeHash());
        }

        [Test]
        public void AdvanceAi_StopsAtTheFirstHumanSeat()
        {
            // Seat 0 human, seats 1–3 AI: the human places, then the AIs act until the snake returns to a human.
            var match = new MatchController(MatchConfig.Create(4, 1, 13));
            Assert.AreEqual(0, MatchRunner.AdvanceAi(match));
            var spot = match.ValidSetupSettlements().First();
            Assert.IsTrue(match.Apply(MatchCommand.AtVertex(MatchCommandType.PlaceSetupSettlement, 0, spot)).Ok);
            Assert.IsTrue(match.Apply(MatchCommand.AtEdge(MatchCommandType.PlaceSetupRoad, 0, match.ValidSetupRoads().First())).Ok);
            int applied = MatchRunner.AdvanceAi(match);
            Assert.Greater(applied, 0);
            Assert.AreEqual(0, match.State.CurrentSeat);
            Assert.IsFalse(match.State.Current.IsAi);
        }

        [Test]
        public void MixedGame_HumansDrivenByTimeoutPolicy_StillFinishes()
        {
            var match = new MatchController(MatchConfig.Create(4, 3, 21));
            int guard = 0;
            while (!match.IsOver && guard++ < 5000)
            {
                MatchRunner.AdvanceAi(match);
                if (match.IsOver)
                    break;
                // A human seat: play like a stand-in until the turn ends (as if each human used the AI hints).
                int seat = match.State.CurrentSeat;
                while (!match.IsOver && match.State.CurrentSeat == seat)
                    Assert.IsTrue(match.Apply(MatchAi.ChooseCommand(match)).Ok);
            }

            Assert.IsTrue(match.IsOver);
        }

        [Test]
        public void TurnClock_PulsesOnlyInLastFiveSeconds()
        {
            var clock = new TurnClock();
            clock.StartTurn();
            Assert.AreEqual(90, clock.DisplaySeconds);
            clock.Tick(84f);
            Assert.IsFalse(clock.ShouldPulse);
            Assert.AreEqual(1f, clock.PulseScale(0.3f));
            clock.Tick(1.5f);
            Assert.IsTrue(clock.ShouldPulse);
            Assert.Greater(clock.PulseScale(0.125f), 1.1f);
            clock.Tick(10f);
            Assert.IsTrue(clock.Expired);
            Assert.IsFalse(clock.ShouldPulse);
        }

        [Test]
        public void TurnClock_ExtraTimeAddsNinetySeconds_AndStopsThePulse()
        {
            var clock = new TurnClock();
            clock.StartTurn();
            clock.Tick(87f);
            Assert.IsTrue(clock.ShouldPulse);
            clock.AddExtraTime();
            Assert.AreEqual(93, clock.DisplaySeconds);
            Assert.IsFalse(clock.ShouldPulse);
            clock.Paused = true;
            clock.Tick(50f);
            Assert.AreEqual(93, clock.DisplaySeconds);
        }

        [Test]
        public void ExtraTimeCharges_StartAtOne_OncePerTurn_PlusOneEveryFiveRounds()
        {
            Assert.AreEqual(1, TurnClock.ChargesEarned(0));
            Assert.AreEqual(1, TurnClock.ChargesEarned(4));
            Assert.AreEqual(2, TurnClock.ChargesEarned(5));
            Assert.AreEqual(3, TurnClock.ChargesEarned(10));

            var match = new MatchController(MatchConfig.Create(2, 2, 17));
            Assert.IsTrue(match.State.Seats.All(s => s.ExtraTimeCharges == 1));
            // Extra time is not available during setup.
            Assert.IsFalse(match.Apply(MatchCommand.Simple(MatchCommandType.UseExtraTime, 0)).Ok);
            while (match.IsSetup)
                Assert.IsTrue(match.Apply(MatchAi.ChooseCommand(match)).Ok);

            int seat = match.State.CurrentSeat;
            Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.UseExtraTime, seat)).Ok);
            Assert.AreEqual(0, match.State.Seats[seat].ExtraTimeCharges);
            Assert.IsFalse(match.Apply(MatchCommand.Simple(MatchCommandType.UseExtraTime, seat)).Ok, "once per turn");

            // Play 5 full rounds without using charges: everyone gains exactly one.
            while (!match.IsOver && match.State.Round < 5)
                Assert.IsTrue(match.Apply(MatchAi.ChooseCommand(match)).Ok);
            Assume.That(!match.IsOver);
            Assert.AreEqual(1, match.State.Seats[seat].ExtraTimeCharges);
            Assert.AreEqual(2, match.State.Seats[1 - seat].ExtraTimeCharges);
        }

        [Test]
        public void ResolveTimeout_EndsTheHumansTurn()
        {
            var match = new MatchController(MatchConfig.Create(3, 3, 8));
            while (match.IsSetup)
                match.Apply(MatchAi.ChooseCommand(match));
            int seat = match.State.CurrentSeat;
            MatchRunner.ResolveTimeout(match);
            Assert.AreNotEqual(seat, match.State.CurrentSeat);
        }

        [Test]
        public void ExtendedSeedThree_BoardGenerationTerminates()
        {
            // v134: token backtracking used to retry duplicate values and could spin for minutes on this seed.
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var board = MapPresets.CreateBoard(MapSize.Extended, 3);
            Assert.AreEqual(30, board.Tiles.Count);
            Assert.Less(watch.ElapsedMilliseconds, 5000);
        }
    }
}
