using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Turn;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class OnboardingCopyTests
    {
        private static GameState CreateState()
        {
            var board = MapPresets.CreateBoard();
            return new GameState(board);
        }

        [Test]
        public void ForPhase_MapSelect_MentionsGrowthAndStars()
        {
            var line = OnboardingCopy.ForPhase(OnboardingCopy.OnboardingBeat.MapSelect);
            Assert.That(line, Does.Contain("Small"));
            Assert.That(line, Does.Contain("stars"));
        }

        [Test]
        public void ForPhase_DraftSelect_MentionsUniques()
        {
            var line = OnboardingCopy.ForPhase(OnboardingCopy.OnboardingBeat.DraftSelect);
            Assert.That(line, Does.Contain("Draft"));
            Assert.That(line, Does.Contain("unique"));
        }

        [Test]
        public void ForPhase_FirstNight_MentionsDiceOrWeather()
        {
            var line = OnboardingCopy.ForPhase(OnboardingCopy.OnboardingBeat.FirstNight);
            Assert.That(line, Does.Contain("dice"));
            Assert.That(line, Does.Contain("weather"));
        }

        [Test]
        public void ForPhase_FirstDay_MentionsShopAndEndDay()
        {
            var line = OnboardingCopy.ForPhase(OnboardingCopy.OnboardingBeat.FirstDay);
            Assert.That(line, Does.Contain("Shop"));
            Assert.That(line, Does.Contain("End Day"));
        }

        [Test]
        public void ForGameOver_WithStars_MentionsPersist()
        {
            var line = OnboardingCopy.ForGameOver(3);
            Assert.That(line, Does.Contain("+3 stars"));
            Assert.That(line, Does.Contain("persist"));
        }

        [Test]
        public void ForGameOver_ZeroStars_StillMentionsPersist()
        {
            var line = OnboardingCopy.ForGameOver(0);
            Assert.That(line, Does.Contain("persist"));
        }

        [Test]
        public void TryGetPhaseBanner_MapSelect_ReturnsLine()
        {
            var ctx = new OnboardingCopy.Context(GamePhase.RunSelectMap, tipsEnabled: true);

            Assert.IsTrue(OnboardingCopy.TryGetPhaseBanner(ctx, out var line));
            Assert.That(line, Does.Contain("Small"));
        }

        [Test]
        public void TryGetPhaseBanner_TipsDisabled_ReturnsFalse()
        {
            var ctx = new OnboardingCopy.Context(GamePhase.RunSelectMap, tipsEnabled: false);

            Assert.IsFalse(OnboardingCopy.TryGetPhaseBanner(ctx, out _));
        }

        [Test]
        public void TryGetFirstNightHybridHint_OnlyOnDayOneNight()
        {
            var state = CreateState();
            state.Board.DayNumber = 1;
            state.Phase = GamePhase.NightPlayCard;

            var ctx = new OnboardingCopy.Context(
                state.Phase,
                state.Board.DayNumber,
                tipsEnabled: true);

            Assert.IsTrue(OnboardingCopy.TryGetFirstNightHybridHint(ctx, out var line));
            Assert.That(line, Does.Contain("2d6"));
            Assert.That(line, Does.Contain("token"));
        }

        [Test]
        public void TryGetFirstNightHybridHint_DayTwo_ReturnsFalse()
        {
            var ctx = new OnboardingCopy.Context(GamePhase.NightPlayCard, dayNumber: 2, tipsEnabled: true);

            Assert.IsFalse(OnboardingCopy.TryGetFirstNightHybridHint(ctx, out _));
        }

        [Test]
        public void ResolveBeat_GameOver_TakesPriority()
        {
            var beat = OnboardingCopy.ResolveBeat(GamePhase.DayPlayerActions, 5, hasWinner: true);
            Assert.AreEqual(OnboardingCopy.OnboardingBeat.GameOver, beat);
        }
    }
}
