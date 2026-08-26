using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Progression;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class RunProgressionTests
    {
        private const int RunSeed = 42;

        private static GameState CreateState(int dayNumber = 1)
        {
            var board = MapPresets.CreateBoard();
            board.DayNumber = dayNumber;
            return new GameState(board) { Leader = LeaderId.Merchant };
        }

        [Test]
        public void WillOfferLevelUpAfterThisDay_True_OnDayBeforeInterval()
        {
            var state = CreateState(dayNumber: 4);
            Assert.IsTrue(RunProgression.WillOfferLevelUpAfterThisDay(state));
        }

        [Test]
        public void WillOfferLevelUpAfterThisDay_False_WhenNextDayNotOnInterval()
        {
            var state = CreateState(dayNumber: 3);
            Assert.IsFalse(RunProgression.WillOfferLevelUpAfterThisDay(state));
        }

        [Test]
        public void ShouldOfferLevelUp_True_OnDayFiveAfterIncrement()
        {
            var state = CreateState(dayNumber: 5);
            Assert.IsTrue(RunProgression.ShouldOfferLevelUp(state));
        }

        [Test]
        public void ShouldOfferLevelUp_False_WhenMaxLevelUpsTaken()
        {
            var state = CreateState(dayNumber: 5);
            state.LevelUpsTaken = RunProgression.MaxLevelUpsPerRun;
            Assert.IsFalse(RunProgression.ShouldOfferLevelUp(state));
            Assert.IsFalse(RunProgression.WillOfferLevelUpAfterThisDay(CreateState(dayNumber: 4)));
        }

        [Test]
        public void ShouldOfferLevelUp_False_WhenAlreadyOfferedOnSameDay()
        {
            var state = CreateState(dayNumber: 5);
            state.LastLevelUpDay = 5;
            Assert.IsFalse(RunProgression.ShouldOfferLevelUp(state));
        }

        [Test]
        public void LastLevelUpDay_PreventsDoubleOfferOnSameDayNumber()
        {
            var state = CreateState(dayNumber: 5);
            state.LastLevelUpDay = 5;
            Assert.IsFalse(RunProgression.ShouldOfferLevelUp(state));

            state.LastLevelUpDay = 4;
            Assert.IsTrue(RunProgression.ShouldOfferLevelUp(state));
        }

        [Test]
        public void PreviewChoices_MatchOfferedChoices_ForSeededDay()
        {
            var state = CreateState(dayNumber: 4);
            var preview = RunProgression.PreviewLevelUpChoices(state, RunSeed);

            state.Board.DayNumber = 5;
            var offered = RunProgression.GenerateLevelUpChoices(
                state, RunProgression.CreateLevelUpRandom(RunSeed, state.Board.DayNumber));

            Assert.AreEqual(preview, offered);
        }

        [Test]
        public void CreateLevelUpRandom_IsDeterministicForSeedAndDay()
        {
            var state = CreateState(dayNumber: 4);
            var first = RunProgression.PreviewLevelUpChoices(state, RunSeed);
            var second = RunProgression.PreviewLevelUpChoices(state, RunSeed);

            Assert.AreEqual(first, second);
        }
    }
}
