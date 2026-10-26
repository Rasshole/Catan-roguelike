using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Match;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    /// <summary>v146: a player trade offer stays open 15 s (counting down), then closes by itself.</summary>
    public class HotSeatSessionV146Tests
    {
        private static HotSeatSession HumanWithCardsAtMain(int seed)
        {
            var s = new HotSeatSession(MatchConfig.Create(3, 1, seed));
            int guard = 0;
            while (guard++ < 4000 && !s.IsOver)
            {
                s.Tick(0.01f);
                if (s.Acting.IsAi || s.PassScreenPending)
                    continue;
                var st = s.State;
                if (st.Phase == MatchPhase.Main && st.ActiveTrade == null && s.Current.Hand.Total >= 1)
                    return s;
                if (st.Phase == MatchPhase.Roll)
                    s.Roll();
                else if (st.Phase == MatchPhase.Main)
                    s.EndTurn();
                else
                    s.ApplySuggested();
            }

            return null;
        }

        [Test]
        public void TradeOffer_ClosesAutomaticallyAfter15Seconds()
        {
            var s = HumanWithCardsAtMain(5);
            Assert.IsNotNull(s, "a human main phase with cards");
            int me = s.State.CurrentSeat;
            var hand = s.Current.Hand;
            var giveRes = MatchController.AllResources.First(r => hand[r] > 0);
            var getRes = MatchController.AllResources.First(r => r != giveRes);
            var give = ResourceBundle.Zero;
            give.Add(giveRes, 1);
            var get = ResourceBundle.Zero;
            get.Add(getRes, 1);
            Assert.AreEqual(0f, s.TradeSecondsLeft);
            Assert.IsTrue(s.Apply(MatchCommand.Propose(me, give, get)).Ok);
            Assert.IsNotNull(s.State.ActiveTrade);
            Assert.AreEqual(HotSeatSession.TradeOpenSeconds, s.TradeSecondsLeft, 0.001f);
            s.Tick(5f);
            Assert.IsNotNull(s.State.ActiveTrade, "still open after 5 s");
            Assert.AreEqual(10f, s.TradeSecondsLeft, 0.01f, "the countdown runs");
            s.Tick(9.9f);
            Assert.IsNotNull(s.State.ActiveTrade, "still open just before 15 s");
            s.Tick(0.2f);
            Assert.IsNull(s.State.ActiveTrade, "closed at 15 s");
            StringAssert.Contains("closed after 15 s", s.LastMessage);
            Assert.AreEqual(MatchPhase.Main, s.State.Phase, "the turn goes on");
            Assert.AreEqual(me, s.State.CurrentSeat);
            Assert.AreEqual(0f, s.TradeSecondsLeft);

            // A new offer gets a fresh 15 s.
            Assert.IsTrue(s.Apply(MatchCommand.Propose(me, give, get)).Ok);
            s.Tick(1f);
            Assert.AreEqual(14f, s.TradeSecondsLeft, 0.01f);
        }
    }
}
