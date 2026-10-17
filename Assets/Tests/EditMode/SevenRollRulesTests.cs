using CatanRoguelike.Core.Yield;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class SevenRollRulesTests
    {
        [Test]
        public void RolledSeven_DetectsSeven()
        {
            Assert.IsTrue(SevenRollRules.RolledSeven(new[] { 3, 4, 7 }));
            Assert.IsFalse(SevenRollRules.RolledSeven(new[] { 6, 6 }));
        }

        [Test]
        public void GetDiscardCount_HandOverSeven_DiscardsHalfRoundedDown()
        {
            Assert.AreEqual(0, SevenRollRules.GetDiscardCount(7));
            Assert.AreEqual(4, SevenRollRules.GetDiscardCount(9));
            Assert.AreEqual(5, SevenRollRules.GetDiscardCount(10));
        }

        [Test]
        public void ValidateDiscardTotal_MatchesRequired()
        {
            var discard = new CatanRoguelike.Core.ResourceBundle { Wood = 2, Brick = 2 };
            Assert.IsTrue(SevenRollRules.ValidateDiscardTotal(4, discard));
            Assert.IsFalse(SevenRollRules.ValidateDiscardTotal(3, discard));
        }
    }
}
