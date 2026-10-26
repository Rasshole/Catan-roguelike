using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Match;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    /// <summary>v134 Phase 2: pass-and-play session (timer, manual +90 s charge, pass screen hiding hands).</summary>
    public class HotSeatSessionV134Tests
    {
        private static HotSeatSession TwoHumansTwoAi() => new(MatchConfig.Create(4, 2, 11));

        [Test]
        public void FirstHumanTurn_ShowsPassScreen_HandHiddenUntilConfirmed()
        {
            var s = TwoHumansTwoAi();
            Assert.IsFalse(s.Current.IsAi);
            Assert.IsTrue(s.PassScreenPending);
            Assert.IsFalse(s.IsHandVisible(s.State.CurrentSeat));
            Assert.IsFalse(s.Apply(MatchCommand.Simple(MatchCommandType.EndTurn, s.State.CurrentSeat)).Ok);
            s.ConfirmPass();
            Assert.IsFalse(s.PassScreenPending);
            Assert.IsTrue(s.IsHandVisible(s.State.CurrentSeat));
            int other = (s.State.CurrentSeat + 1) % 4;
            Assert.IsFalse(s.IsHandVisible(other));
        }

        [Test]
        public void ClockPausedDuringPassScreen()
        {
            var s = TwoHumansTwoAi();
            s.Tick(30f);
            Assert.AreEqual(TurnClock.TurnSeconds, s.Clock.Remaining, 0.001f);
            s.ConfirmPass();
            s.Tick(30f);
            Assert.AreEqual(TurnClock.TurnSeconds - 30f, s.Clock.Remaining, 0.001f);
        }

        [Test]
        public void SingleHuman_NoPassScreen()
        {
            var s = new HotSeatSession(MatchConfig.Create(5, 1, 3));
            Assert.IsFalse(s.PassScreenPending);
            Assert.IsTrue(s.IsHandVisible(s.State.CurrentSeat));
        }

        [Test]
        public void HandingToTheOtherHuman_ShowsPassScreenAgain()
        {
            var s = TwoHumansTwoAi();
            int first = s.State.CurrentSeat;
            s.ConfirmPass();
            int guard = 0;
            while (s.State.CurrentSeat == first && guard++ < 10)
                s.ApplySuggested();
            s.Tick(0.1f); // AI seats (if any) act
            guard = 0;
            while (s.State.CurrentSeat == first && guard++ < 10)
            {
                if (s.PassScreenPending) s.ConfirmPass();
                s.ApplySuggested();
                s.Tick(0.1f);
            }

            Assert.AreNotEqual(first, s.State.CurrentSeat);
            Assert.IsFalse(s.Current.IsAi);
            Assert.IsTrue(s.PassScreenPending);
        }

        [Test]
        public void ExtraTimeCharge_AddsNinetySeconds_OncePerTurn_NotInSetup()
        {
            var s = new HotSeatSession(MatchConfig.Create(4, 1, 21));
            Assert.IsFalse(s.CanUseExtraTime, "No charge during setup placement.");
            int guard = 0;
            while (s.Match.IsSetup && guard++ < 50)
            {
                if (!s.Current.IsAi) s.ApplySuggested();
                s.Tick(0.01f);
            }

            Assert.IsFalse(s.Match.IsSetup);
            Assert.IsFalse(s.Current.IsAi);
            s.Tick(80f);
            Assert.IsTrue(s.Clock.ShouldPulse == false);
            s.Tick(6f);
            Assert.IsTrue(s.Clock.ShouldPulse, "last 5 s pulse");
            Assert.IsTrue(s.CanUseExtraTime);
            Assert.IsTrue(s.UseExtraTime().Ok);
            Assert.AreEqual(94f, s.Clock.Remaining, 0.1f);
            Assert.AreEqual(0, s.Current.ExtraTimeCharges);
            Assert.IsFalse(s.CanUseExtraTime);
            Assert.IsFalse(s.UseExtraTime().Ok);
        }

        [Test]
        public void Timeout_EndsTheHumanTurn_AndGameStillFinishes()
        {
            var s = new HotSeatSession(MatchConfig.Create(3, 2, 5));
            int guard = 0;
            while (!s.IsOver && guard++ < 20000)
            {
                if (s.PassScreenPending) s.ConfirmPass();
                if (!s.Current.IsAi && !s.Match.IsSetup && s.State.Phase == MatchPhase.Main)
                    s.AutoBuild();
                s.Tick(TurnClock.TurnSeconds + 1f); // every human turn times out
            }

            Assert.IsTrue(s.IsOver);
            Assert.IsTrue(s.State.Seats.Any(x => x.VictoryPoints >= s.State.Config.VictoryPoints));
        }
    }
}
