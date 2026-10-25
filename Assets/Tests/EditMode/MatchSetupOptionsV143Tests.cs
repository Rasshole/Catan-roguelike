using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Match;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    /// <summary>
    /// v143 setup screen: every map size plays with 2–5 seats, the turn timer can be switched off or lengthened,
    /// event cards and shenanigans can be switched off, and Fishermen of Catan (fish → cards, the AI uses it too).
    /// </summary>
    public class MatchSetupOptionsV143Tests
    {
        private static MatchConfig Config(int seats, int seed, MapSize map, bool party = false)
        {
            var config = MatchConfig.Create(seats, 0, seed, map, party);
            return config;
        }

        /// <summary>Plays with the AI policy for every seat; returns the commands applied.</summary>
        private static List<MatchCommand> Play(MatchController match, int maxTurns, System.Action<MatchController> afterEach = null)
        {
            var applied = new List<MatchCommand>();
            int guard = 0;
            while (!match.IsOver && match.State.TurnNumber <= maxTurns && guard++ < 200000)
            {
                var command = MatchRunner.PendingResponse(match) ?? MatchAi.ChooseCommand(match);
                var result = match.Apply(command);
                Assert.IsTrue(result.Ok, $"{command}: {result.Error}");
                applied.Add(command);
                afterEach?.Invoke(match);
            }

            return applied;
        }

        [Test]
        public void EveryMapSize_PlaysWithTwoToFiveSeats(
            [Values(MapSize.Small, MapSize.Medium, MapSize.Large, MapSize.Extended, MapSize.Huge)] MapSize map,
            [Values(2, 3, 4, 5)] int seats)
        {
            var config = Config(seats, 7 + seats, map, party: true);
            config.Fishermen = true;
            var match = new MatchController(config);
            Assert.AreEqual(MapPresets.GetHexCount(map), match.State.Board.LandTileCount, "land hexes");
            Play(match, 40);
            Assert.That(match.State.Phase, Is.Not.EqualTo(MatchPhase.SetupSettlement), "setup finished");
            Assert.Greater(match.State.TurnNumber, 1);
        }

        [Test]
        public void TurnClock_OffNeverExpires_AndLengthIsConfigurable()
        {
            var off = new TurnClock(90f, enabled: false);
            off.StartTurn();
            off.Tick(10000f);
            Assert.IsFalse(off.Expired);
            Assert.IsFalse(off.ShouldPulse);
            Assert.AreEqual(off.Budget, off.Remaining);

            var slow = new TurnClock(240f, enabled: true);
            slow.StartTurn();
            Assert.AreEqual(240f, slow.Remaining, 0.01f);
            slow.Tick(239f);
            Assert.IsFalse(slow.Expired);
            slow.Tick(2f);
            Assert.IsTrue(slow.Expired);

            var config = MatchConfig.Create(3, 3, 5);
            config.TurnTimer = false;
            var session = new HotSeatSession(config);
            Assert.IsFalse(session.Clock.Enabled);
            Assert.IsFalse(session.CanUseExtraTime, "no +time charge on an untimed table");

            var bad = MatchConfig.Create(3, 3, 5);
            bad.TurnSeconds = 5;
            Assert.Throws<System.ArgumentException>(() => bad.Validate());
        }

        [Test]
        public void EventCardsOff_NoEventEverDrawn([Values(1, 2, 3)] int seed)
        {
            var config = Config(4, seed, MapSize.Large, party: true);
            config.EventCards = false;
            var match = new MatchController(config);
            Assert.IsFalse(match.EventsOn);
            Play(match, 30, m => Assert.AreEqual(EventId.None, m.State.ActiveEvent, "no event card"));
        }

        [Test]
        public void ShenanigansOff_NoSabotage_NoShenaniganEvents([Values(1, 2, 3)] int seed)
        {
            var config = Config(4, seed, MapSize.Large, party: true);
            config.Shenanigans = false;
            var match = new MatchController(config);
            Assert.IsFalse(match.ShenanigansOn);
            Assert.IsTrue(match.EventsOn);
            var banned = new[] { EventId.Decree, EventId.NightMarket, EventId.Chaos };
            var commands = Play(match, 30, m =>
            {
                Assert.IsFalse(m.State.EventDeck.Any(banned.Contains), "shenanigan events removed from the deck");
                Assert.IsFalse(banned.Contains(m.State.ActiveEvent));
            });
            Assert.IsFalse(commands.Any(c => c.Type == MatchCommandType.BuySabotage || c.Type == MatchCommandType.PlaySabotage
                                             || c.Type == MatchCommandType.PostBounty || c.Type == MatchCommandType.GuessSaboteur));
            if (!match.IsOver && match.State.Phase == MatchPhase.Main)
            {
                var r = match.Apply(MatchCommand.Simple(MatchCommandType.BuySabotage, match.State.CurrentSeat));
                Assert.IsFalse(r.Ok);
            }
        }

        [Test]
        public void Fishermen_FishBuyACard_OnlyWithFourFish_OnYourTurn()
        {
            var config = MatchConfig.Create(3, 3, 11);
            config.Fishermen = true;
            var match = new MatchController(config);
            Assert.Greater(match.State.Board.SeaFishGrounds.Count, 0, "fishing grounds on the board");
            match.State.CurrentSeat = 1;
            match.State.Phase = MatchPhase.Main;
            var seat = match.State.Seats[1];
            seat.Fish = 3;
            Assert.IsFalse(match.CanFishForCard(1));
            Assert.IsFalse(match.Apply(MatchCommand.Fish(1, ResourceType.Stone)).Ok, "needs four fish");
            seat.Fish = 9;
            int stone = seat.Hand.Stone;
            Assert.IsTrue(match.Apply(MatchCommand.Fish(1, ResourceType.Stone)).Ok);
            Assert.AreEqual(stone + 1, seat.Hand.Stone);
            Assert.AreEqual(5, seat.Fish);
            Assert.IsFalse(match.Apply(MatchCommand.Fish(0, ResourceType.Stone)).Ok, "not your turn");

            var json = MatchCommand.Fish(1, ResourceType.Wood).ToJson();
            var back = MatchCommand.FromJson(json);
            Assert.AreEqual(MatchCommandType.FishForCard, back.Type);
            Assert.IsTrue(match.Apply(back).Ok, "serializable command");
            Assert.AreEqual(1, seat.Fish);

            var classic = new MatchController(MatchConfig.Create(3, 3, 11));
            classic.State.Phase = MatchPhase.Main;
            classic.State.Seats[classic.State.CurrentSeat].Fish = 8;
            Assert.IsFalse(classic.Apply(MatchCommand.Fish(classic.State.CurrentSeat, ResourceType.Wood)).Ok);
        }

        [Test]
        public void Fishermen_CoastSettlementsCatchFish_AndTheAiTradesThem()
        {
            int caught = 0, traded = 0;
            for (int seed = 1; seed <= 6; seed++)
            {
                var config = Config(4, seed, MapSize.Large);
                config.Fishermen = true;
                var match = new MatchController(config);
                int last = 0;
                var commands = Play(match, 80, m =>
                {
                    int total = m.State.Seats.Sum(s => s.Fish);
                    if (total > last)
                        caught += total - last;
                    last = total;
                });
                traded += commands.Count(c => c.Type == MatchCommandType.FishForCard);
            }

            Assert.Greater(caught, 0, "fishing grounds feed coastal settlements");
            Assert.Greater(traded, 0, "the AI spends its fish");
        }

        [Test]
        public void Fishermen_IsDeterministic()
        {
            ulong Run()
            {
                var config = Config(4, 21, MapSize.Extended, party: true);
                config.Fishermen = true;
                var match = new MatchController(config);
                Play(match, 25);
                return match.State.ComputeHash();
            }

            Assert.AreEqual(Run(), Run());
        }
    }
}
