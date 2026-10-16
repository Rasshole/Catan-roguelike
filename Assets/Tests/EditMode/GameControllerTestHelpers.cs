using CatanRoguelike.Core;
using CatanRoguelike.Core.Turn;

namespace CatanRoguelike.Tests
{
    public static class GameControllerTestHelpers
    {
        public static void ResolveMorningRollIfNeeded(GameController game)
        {
            const int maxSteps = 24;
            for (int step = 0; step < maxSteps; step++)
            {
                switch (game.State.Phase)
                {
                    case GamePhase.DayProduction:
                        game.ResolveDayProduction();
                        break;
                    case GamePhase.DayDiscardForSeven:
                        SubmitAutoSevenDiscard(game);
                        break;
                    case GamePhase.DayPlaceRobberSeven:
                        PlaceRobberOnFirstTile(game);
                        break;
                    default:
                        return;
                }
            }
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

        private static void SubmitAutoSevenDiscard(GameController game)
        {
            int need = game.State.PendingSevenDiscard;
            if (need <= 0)
                return;

            var discard = ResourceBundle.Zero;
            var inv = game.State.PlayerInventory;
            foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            {
                int take = System.Math.Min(need, inv[type]);
                for (int i = 0; i < take; i++)
                    discard.Add(type, 1);
                need -= take;
            }

            if (!game.SubmitSevenDiscard(discard))
                throw new System.InvalidOperationException(
                    $"Auto seven-discard failed (need {game.State.PendingSevenDiscard}, got {discard.Total}).");
        }

        private static void PlaceRobberOnFirstTile(GameController game)
        {
            foreach (var coord in game.State.Board.Tiles.Keys)
            {
                if (game.PlaceRobberForSeven(coord))
                    return;
            }

            throw new System.InvalidOperationException("Could not auto-place robber after seven.");
        }
    }
}
