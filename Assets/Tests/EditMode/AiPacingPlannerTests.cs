using CatanRoguelike.Core;
using CatanRoguelike.Core.Ai;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Turn;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class AiPacingPlannerTests
    {
        [Test]
        public void PlanDayActions_AlwaysIncludesRobberStep()
        {
            var game = new GameController(3, MapSize.Small);
            game.State.Phase = GamePhase.DayPlayerActions;
            var steps = AiPacingPlanner.PlanDayActions(game.State);
            Assert.That(steps, Does.Contain(AiDayActionKind.MoveRobber));
        }

        [Test]
        public void PlanDayActions_IsNonEmptyDuringDay()
        {
            var game = new GameController(4, MapSize.Small);
            game.State.Phase = GamePhase.DayPlayerActions;
            var steps = AiPacingPlanner.PlanDayActions(game.State);
            Assert.Greater(steps.Count, 0);
        }
    }
}
