using System;
using System.Collections.Generic;

namespace CatanRoguelike.Core.Cards
{
    public sealed class CardDefinition
    {
        public CardId Id { get; }
        public string Name { get; }
        public string Description { get; }
        public CardCategory Category { get; }
        public bool AiCanUse { get; }
        public float AiWeight { get; }

        public CardDefinition(CardId id, string name, string description, CardCategory category,
            bool aiCanUse = true, float aiWeight = 1f)
        {
            Id = id;
            Name = name;
            Description = description;
            Category = category;
            AiCanUse = aiCanUse;
            AiWeight = aiWeight;
        }
    }

    public static class CardLibrary
    {
        private static readonly Dictionary<CardId, CardDefinition> Definitions = new()
        {
            [CardId.MerchantsLedger] = new(CardId.MerchantsLedger, "Merchant's Ledger",
                "Choose a resource: if tomorrow's roll is 0, set it to 1.", CardCategory.RollManipulation),
            [CardId.Drought] = new(CardId.Drought, "Drought",
                "Choose a resource: cap tomorrow's roll at 1.", CardCategory.RollManipulation),
            [CardId.FertileSeason] = new(CardId.FertileSeason, "Fertile Season",
                "Choose a resource: +1 to tomorrow's roll (respects cap).", CardCategory.RollManipulation),
            [CardId.RoadBuilder] = new(CardId.RoadBuilder, "Road Builder",
                "Place one road for free on your next day.", CardCategory.BuildTempo, aiWeight: 0.8f),
            [CardId.YearOfPlenty] = new(CardId.YearOfPlenty, "Year of Plenty",
                "Gain 2 resources of your choice.", CardCategory.BuildTempo),
            [CardId.Monopoly] = new(CardId.Monopoly, "Monopoly",
                "Take half of opponent's stock of one resource (rounded up).", CardCategory.Sabotage, aiWeight: 1.2f),
            [CardId.Knight] = new(CardId.Knight, "Knight",
                "Move the robber and steal 1 random resource.", CardCategory.Sabotage),
            [CardId.BanditRaid] = new(CardId.BanditRaid, "Bandit Raid",
                "Disable one opponent road for the next day.", CardCategory.Sabotage, aiWeight: 1.1f),
            [CardId.Embargo] = new(CardId.Embargo, "Embargo",
                "Opponent cannot buy shop deals using one resource tomorrow.", CardCategory.Sabotage, aiWeight: 0.7f),
            [CardId.HarborCharter] = new(CardId.HarborCharter, "Harbor Charter",
                "Next coastal settlement grants +1 VP.", CardCategory.Synergy, aiCanUse: false),
            [CardId.MasterBuilder] = new(CardId.MasterBuilder, "Master Builder",
                "Next building costs 25% less.", CardCategory.Synergy),
            [CardId.Forecast] = new(CardId.Forecast, "Forecast",
                "Reroll one resource's tomorrow roll before cap is applied.", CardCategory.RollManipulation)
        };

        public static CardDefinition Get(CardId id) => Definitions[id];

        public static IReadOnlyList<CardId> AllCards => new List<CardId>(Definitions.Keys);

        public static IReadOnlyList<CardId> AiPool => new List<CardId>
        {
            CardId.Drought,
            CardId.Knight,
            CardId.BanditRaid,
            CardId.Monopoly,
            CardId.RoadBuilder,
            CardId.YearOfPlenty
        };
    }
}
