namespace CatanRoguelike.Core.Events
{
    public enum EventId
    {
        None,
        Storm,
        Famine,
        GoldRush,
        MarketDay,
        GoodHarvest,
        BanditRaid,
        PortBlockade,
        ResourceLevy,

        // v131 (appended: saves store the enum as an int)
        Earthquake,
        ShiftingFortunes,
        DoubleHarvest,
        LeanTimes,
        RoyalPatronage,
        HarvestFestival,
        Blight,
        Squall,
        Bountiful,
        RestlessRobber
    }
}
