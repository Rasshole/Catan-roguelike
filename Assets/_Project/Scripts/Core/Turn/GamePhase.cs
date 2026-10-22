namespace CatanRoguelike.Core.Turn
{
    public enum GamePhase
    {
        RunSelectMap,
        RunSelectLeader,
        RunSelectDraft,
        SetupAiSettlement1,
        SetupAiRoad1,
        SetupAiSettlement2,
        SetupAiRoad2,
        SetupPlayerSettlement1,
        SetupPlayerRoad1,
        SetupPlayerSettlement2,
        SetupPlayerRoad2,
        NightRoll,
        NightPlayCard,
        NightAiPlan,
        DayProduction,
        DayDiscardForSeven,
        DayPlaceRobberSeven,
        DayPlayerActions,
        DayAiTurn,
        LevelUpChoice,
        GameOver,
        /// <summary>v131: pick 1 of 3 offered civilizations (after the unique draft, before setup).</summary>
        RunSelectCivilization
    }
}
