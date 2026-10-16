namespace CatanRoguelike.Core.Yield
{
    public enum FishermenSpendAction
    {
        MoveRobberOffBoard = 2,
        StealFromOpponent = 3,
        FreeRoad = 4,
        DrawResource = 5,
        DrawDevelopmentCard = 7
    }

    /// <summary>Seafarers Fishermen of Catan fish spend costs (Colonist-style).</summary>
    public static class FishermenRules
    {
        public static int GetFishCost(FishermenSpendAction action) => (int)action;

        public static bool CanSpend(int fishBalance, FishermenSpendAction action) =>
            fishBalance >= GetFishCost(action);

        public static bool IsEnabled(GameState state) =>
            state != null && state.FishermenOfCatanEnabled;
    }
}
