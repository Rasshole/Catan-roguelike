using CatanRoguelike.Core;
using CatanRoguelike.Core.Turn;

namespace CatanRoguelike.Tests
{
    public static class GameControllerTestHelpers
    {
        public static void ResolveMorningRollIfNeeded(GameController game)
        {
            if (game.State.Phase == GamePhase.DayProduction)
                game.ResolveDayProduction();
        }

        public static void SkipNightAndResolveMorning(GameController game)
        {
            game.SkipNightCard();
            ResolveMorningRollIfNeeded(game);
        }

        public static void EndDayWithAi(GameController game)
        {
            game.EndPlayerDay();
            game.CompleteAiDayTurn();
        }
    }
}
