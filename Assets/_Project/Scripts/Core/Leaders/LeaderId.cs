namespace CatanRoguelike.Core.Leaders
{
    public enum LeaderId
    {
        Merchant,
        Pioneer,
        Warlord,
        Architect
    }

    public enum LevelUpPerkId
    {
        // Merchant
        ExtraShopDeal,
        RiskyDealsSafe,
        PortDiscount,

        // Pioneer
        DoubleRoadBuilder,
        LongRoadBonus,
        CheapSettlements,

        // Warlord
        EmbargoExtended,
        KnightMovesRobberTwice,
        MonopolyFull,

        // Architect
        CityProductionBoost,
        FirstCityVp,
        CheapCities,

        // General
        ExtraCardDraw,
        RollInsurance,
        ThresholdDelay
    }
}
