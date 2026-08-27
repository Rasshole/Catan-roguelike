using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Leaders;

namespace CatanRoguelike.Core.Progression
{
    /// <summary>
    /// Unlock tree costs and default availability for fresh meta profiles.
    /// Star award per run: humanVP + dayNumber/2 + <see cref="WinBonusStars"/> if human wins.
    /// Unlocks change how you play — not raw production multipliers.
    /// </summary>
    public static class MetaCatalog
    {
        public const int WinBonusStars = 2;

        // Small map, Merchant + Pioneer, Sawmill + Guild Hall, and starter cards are free without ids.
        public static readonly IReadOnlyList<MetaUnlockId> DefaultUnlocked = Array.Empty<MetaUnlockId>();

        public static readonly IReadOnlyList<UniqueBuildingId> DefaultFreeUniques = new[]
        {
            UniqueBuildingId.Sawmill,
            UniqueBuildingId.GuildHall
        };

        /// <summary>Starter night-draw pool for fresh profiles (7 of 12 cards).</summary>
        public static readonly IReadOnlyList<CardId> DefaultFreeCards = new[]
        {
            CardId.Knight,
            CardId.RoadBuilder,
            CardId.YearOfPlenty,
            CardId.Monopoly,
            CardId.Drought,
            CardId.MasterBuilder,
            CardId.FertileSeason
        };

        public static readonly IReadOnlyDictionary<MetaUnlockId, MetaUnlockDefinition> All =
            new Dictionary<MetaUnlockId, MetaUnlockDefinition>
            {
                [MetaUnlockId.MediumMap] = new(
                    MetaUnlockId.MediumMap,
                    4,
                    "Medium map (13 hex)",
                    "Unlock the 13-hex map for longer runs."),

                [MetaUnlockId.LargeMap] = new(
                    MetaUnlockId.LargeMap,
                    8,
                    "Large map (19 hex)",
                    "Unlock the classic 19-hex map."),

                [MetaUnlockId.LeaderWarlord] = new(
                    MetaUnlockId.LeaderWarlord,
                    3,
                    "Leader: Warlord",
                    "Knight steals 2 resources instead of 1."),

                [MetaUnlockId.LeaderArchitect] = new(
                    MetaUnlockId.LeaderArchitect,
                    5,
                    "Leader: Architect",
                    "Master Builder and threshold builds cost 10% less."),

                [MetaUnlockId.ExtraDraftPick] = new(
                    MetaUnlockId.ExtraDraftPick,
                    6,
                    "Extra unique draft",
                    "Draft 3 unique buildings instead of 2 at run start (when enough are unlocked)."),

                [MetaUnlockId.StartBonusWheat] = new(
                    MetaUnlockId.StartBonusWheat,
                    2,
                    "Wheat ration",
                    "Start each run with +1 wheat before setup."),

                [MetaUnlockId.StartBonusCard] = new(
                    MetaUnlockId.StartBonusCard,
                    4,
                    "Road Builder voucher",
                    "Start the first night with a free Road Builder card."),

                [MetaUnlockId.UniqueMonastery] = new(
                    MetaUnlockId.UniqueMonastery,
                    3,
                    "Unique: Monastery",
                    "Add Monastery to the run-start unique draft pool."),

                [MetaUnlockId.UniqueCaravanPost] = new(
                    MetaUnlockId.UniqueCaravanPost,
                    4,
                    "Unique: Caravan Post",
                    "Add Caravan Post to the run-start unique draft pool."),

                [MetaUnlockId.UniqueFortressOutpost] = new(
                    MetaUnlockId.UniqueFortressOutpost,
                    5,
                    "Unique: Fortress Outpost",
                    "Add Fortress Outpost to the run-start unique draft pool."),

                [MetaUnlockId.CardPackSabotage] = new(
                    MetaUnlockId.CardPackSabotage,
                    3,
                    "Sabotage card pack",
                    "Unlock Bandit Raid and Embargo in your night draw pool."),

                [MetaUnlockId.CardPackMarket] = new(
                    MetaUnlockId.CardPackMarket,
                    4,
                    "Market card pack",
                    "Unlock Harbor Charter, Merchant's Ledger, and Forecast in your night draw pool.")
            };

        public static MetaUnlockDefinition Get(MetaUnlockId id) => All[id];

        public static bool IsMapAlwaysAvailable(MapSize size) => size == MapSize.Small;

        public static bool IsLeaderAlwaysAvailable(LeaderId leader) =>
            leader == LeaderId.Merchant || leader == LeaderId.Pioneer;

        public static bool IsUniqueAlwaysAvailable(UniqueBuildingId id) =>
            DefaultFreeUniques.Contains(id);

        public static bool IsCardAlwaysAvailable(CardId id) =>
            DefaultFreeCards.Contains(id);

        public static MetaUnlockId? MapUnlockFor(MapSize size) => size switch
        {
            MapSize.Medium => MetaUnlockId.MediumMap,
            MapSize.Large => MetaUnlockId.LargeMap,
            _ => null
        };

        public static MetaUnlockId? LeaderUnlockFor(LeaderId leader) => leader switch
        {
            LeaderId.Warlord => MetaUnlockId.LeaderWarlord,
            LeaderId.Architect => MetaUnlockId.LeaderArchitect,
            _ => null
        };

        public static MetaUnlockId? UniqueUnlockFor(UniqueBuildingId id) => id switch
        {
            UniqueBuildingId.Monastery => MetaUnlockId.UniqueMonastery,
            UniqueBuildingId.CaravanPost => MetaUnlockId.UniqueCaravanPost,
            UniqueBuildingId.FortressOutpost => MetaUnlockId.UniqueFortressOutpost,
            _ => null
        };

        public static MetaUnlockId? CardUnlockFor(CardId id) => id switch
        {
            CardId.BanditRaid => MetaUnlockId.CardPackSabotage,
            CardId.Embargo => MetaUnlockId.CardPackSabotage,
            CardId.HarborCharter => MetaUnlockId.CardPackMarket,
            CardId.MerchantsLedger => MetaUnlockId.CardPackMarket,
            CardId.Forecast => MetaUnlockId.CardPackMarket,
            _ => null
        };

        public static CardId StartBonusCard => CardId.RoadBuilder;
    }

    public sealed class MetaUnlockDefinition
    {
        public MetaUnlockId Id { get; }
        public int Cost { get; }
        public string Title { get; }
        public string Description { get; }

        public MetaUnlockDefinition(MetaUnlockId id, int cost, string title, string description)
        {
            Id = id;
            Cost = cost;
            Title = title;
            Description = description;
        }
    }
}
