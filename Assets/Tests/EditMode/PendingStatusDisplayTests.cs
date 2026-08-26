using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Turn;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class PendingStatusDisplayTests
    {
        private static GameState CreateState()
        {
            var board = MapPresets.CreateBoard();
            return new GameState(board);
        }

        [Test]
        public void HarborCharterLine_ReturnsFalse_WhenNotPending()
        {
            var state = CreateState();
            Assert.IsFalse(PendingStatusDisplay.TryGetHarborCharterLine(state, out _));
        }

        [Test]
        public void HarborCharterLine_ReturnsMessage_WhenPending()
        {
            var state = CreateState();
            state.HarborCharterPending = true;

            Assert.IsTrue(PendingStatusDisplay.TryGetHarborCharterLine(state, out var line));
            Assert.That(line, Does.Contain("Harbor Charter"));
            Assert.That(line, Does.Contain("+1 VP"));
        }

        [Test]
        public void EmbargoLine_ReturnsFalse_WhenNoEmbargo()
        {
            var state = CreateState();
            Assert.IsFalse(PendingStatusDisplay.TryGetEmbargoLine(state, out _));
        }

        [Test]
        public void EmbargoLine_ReturnsFalse_WhenDaysLeftIsZero()
        {
            var state = CreateState();
            state.AiShopEmbargo = ResourceType.Wood;
            state.AiEmbargoDaysLeft = 0;

            Assert.IsFalse(PendingStatusDisplay.TryGetEmbargoLine(state, out _));
        }

        [Test]
        public void EmbargoLine_ShowsResourceAndDays_WhenActive()
        {
            var state = CreateState();
            state.AiShopEmbargo = ResourceType.Brick;
            state.AiEmbargoDaysLeft = 2;

            Assert.IsTrue(PendingStatusDisplay.TryGetEmbargoLine(state, out var line));
            Assert.That(line, Does.Contain("Embargo"));
            Assert.That(line, Does.Contain("Brick"));
            Assert.That(line, Does.Contain("2 days"));
        }

        [Test]
        public void EmbargoLine_UsesSingularDay_WhenOneDayLeft()
        {
            var state = CreateState();
            state.AiShopEmbargo = ResourceType.Wheat;
            state.AiEmbargoDaysLeft = 1;

            Assert.IsTrue(PendingStatusDisplay.TryGetEmbargoLine(state, out var line));
            Assert.That(line, Does.Contain("1 day"));
            Assert.That(line, Does.Not.Contain("1 days"));
        }

        [Test]
        public void LevelUpPreviewLine_ReturnsFalse_OutsideDayPlayerActions()
        {
            var state = CreateState();
            state.Board.DayNumber = 4;
            state.Phase = GamePhase.NightRoll;

            Assert.IsFalse(PendingStatusDisplay.TryGetLevelUpPreviewLine(state, 42, out _));
        }

        [Test]
        public void LevelUpPreviewLine_ReturnsMessage_OnDayBeforeLevelUp()
        {
            var state = CreateState();
            state.Board.DayNumber = 4;
            state.Phase = GamePhase.DayPlayerActions;
            state.Leader = LeaderId.Merchant;

            Assert.IsTrue(PendingStatusDisplay.TryGetLevelUpPreviewLine(state, 42, out var line));
            Assert.That(line, Does.Contain("Level-up after End Day"));
            Assert.That(line, Does.Contain("choose one"));
        }

        [Test]
        public void LevelUpPreviewLine_ReturnsFalse_WhenNoLevelUpTomorrow()
        {
            var state = CreateState();
            state.Board.DayNumber = 3;
            state.Phase = GamePhase.DayPlayerActions;

            Assert.IsFalse(PendingStatusDisplay.TryGetLevelUpPreviewLine(state, 42, out _));
        }
    }
}
