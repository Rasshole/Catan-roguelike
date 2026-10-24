using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Match;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    /// <summary>v140: a closed offer leaves a closing line in the log, so the status line never shows "offers a trade".</summary>
    public class MatchTradeStatusV140Tests
    {
        private static MatchController AfterSetup(int seed)
        {
            var match = new MatchController(MatchConfig.Create(3, 3, seed));
            while (match.IsSetup)
                Assert.IsTrue(match.Apply(MatchAi.ChooseCommand(match)).Ok);
            match.State.CurrentSeat = 0;
            match.State.Phase = MatchPhase.Main;
            match.State.Seats[0].Hand = new ResourceBundle { Wood = 2, Brick = 2 };
            return match;
        }

        [Test]
        public void WithdrawnOffer_LogsTheWithdrawal()
        {
            var match = AfterSetup(140);
            Assert.IsTrue(match.Apply(MatchCommand.Propose(0, new ResourceBundle { Wood = 1 }, new ResourceBundle { Stone = 1 })).Ok);
            Assert.That(match.State.Log.Last(), Does.EndWith("offers a trade."));
            Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.CancelTrade, 0)).Ok);
            Assert.IsNull(match.State.ActiveTrade);
            Assert.That(match.State.Log.Last(), Does.Contain("withdrew the trade offer"));
        }

        [Test]
        public void EndTurn_WithOpenOffer_LogsTheLapse()
        {
            var match = AfterSetup(141);
            Assert.IsTrue(match.Apply(MatchCommand.Propose(0, new ResourceBundle { Wood = 1 }, new ResourceBundle { Stone = 1 })).Ok);
            Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.EndTurn, 0)).Ok);
            Assert.IsNull(match.State.ActiveTrade);
            Assert.IsTrue(match.State.Log.Any(l => l.Contains("trade offer lapsed")));
            Assert.IsFalse(match.State.Log.Last().EndsWith("offers a trade."));
        }
    }
}
