using System.Collections.Generic;

namespace CatanRoguelike.Core.Leaders
{
    public sealed class LeaderDefinition
    {
        public LeaderId Id { get; }
        public string Name { get; }
        public string PassiveDescription { get; }
        public IReadOnlyList<LevelUpPerkId> PerkPool { get; }

        public LeaderDefinition(LeaderId id, string name, string passive, params LevelUpPerkId[] perks)
        {
            Id = id;
            Name = name;
            PassiveDescription = passive;
            PerkPool = perks;
        }
    }

    public static class LeaderLibrary
    {
        public static readonly Dictionary<LeaderId, LeaderDefinition> All = new()
        {
            [LeaderId.Merchant] = new(LeaderId.Merchant, "The Merchant",
                "Shop trades cost 1 fewer resource (minimum 2:1).",
                LevelUpPerkId.ExtraShopDeal, LevelUpPerkId.RiskyDealsSafe, LevelUpPerkId.PortDiscount),

            [LeaderId.Pioneer] = new(LeaderId.Pioneer, "The Pioneer",
                "Your first road each day is free.",
                LevelUpPerkId.DoubleRoadBuilder, LevelUpPerkId.LongRoadBonus, LevelUpPerkId.CheapSettlements),

            [LeaderId.Warlord] = new(LeaderId.Warlord, "The Warlord",
                "Knight steals 2 resources instead of 1.",
                LevelUpPerkId.EmbargoExtended, LevelUpPerkId.MonopolyFull, LevelUpPerkId.KnightMovesRobberTwice),

            [LeaderId.Architect] = new(LeaderId.Architect, "The Architect",
                "Master Builder and threshold-aware builds cost 10% less.",
                LevelUpPerkId.CityProductionBoost, LevelUpPerkId.FirstCityVp, LevelUpPerkId.CheapCities)
        };

        public static LeaderDefinition Get(LeaderId id) => All[id];
    }

    public static class LevelUpLibrary
    {
        public static readonly Dictionary<LevelUpPerkId, string> Descriptions = new()
        {
            [LevelUpPerkId.ExtraShopDeal] = "+1 shop deal available each day.",
            [LevelUpPerkId.RiskyDealsSafe] = "Risky shop deals no longer move the robber on you.",
            [LevelUpPerkId.PortDiscount] = "Ports also reduce unrelated trades by 1 (min 2).",
            [LevelUpPerkId.DoubleRoadBuilder] = "Road Builder places 2 free roads.",
            [LevelUpPerkId.LongRoadBonus] = "+1 VP when you claim longest route.",
            [LevelUpPerkId.CheapSettlements] = "Settlements cost 1 less sheep.",
            [LevelUpPerkId.EmbargoExtended] = "Embargo blocks AI shop for 2 days.",
            [LevelUpPerkId.MonopolyFull] = "Monopoly takes all of a resource (not half).",
            [LevelUpPerkId.KnightMovesRobberTwice] = "Knight also disables a random AI road for 1 day.",
            [LevelUpPerkId.CityProductionBoost] = "Cities produce +1 on their best adjacent resource.",
            [LevelUpPerkId.FirstCityVp] = "Your first city built this run grants +1 VP.",
            [LevelUpPerkId.CheapCities] = "Cities cost 1 less stone.",
            [LevelUpPerkId.ExtraCardDraw] = "Draw 1 extra card each night.",
            [LevelUpPerkId.RollInsurance] = "One 0 roll becomes 1 each night (auto: your scarcest resource).",
            [LevelUpPerkId.ThresholdDelay] = "Settlement threshold penalty starts at 8 instead of 7.",
        };

        public static string GetDescription(LevelUpPerkId id) => Descriptions[id];
    }
}
