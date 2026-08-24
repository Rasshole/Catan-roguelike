using System.Collections.Generic;

namespace CatanRoguelike.Core.Buildings
{
    public sealed class UniqueBuildingDefinition
    {
        public UniqueBuildingId Id { get; }
        public string Name { get; }
        public string Description { get; }

        public UniqueBuildingDefinition(UniqueBuildingId id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }
    }

    public static class UniqueBuildingLibrary
    {
        public static readonly Dictionary<UniqueBuildingId, UniqueBuildingDefinition> All = new()
        {
            [UniqueBuildingId.Sawmill] = new(UniqueBuildingId.Sawmill, "Sawmill",
                "Your buildings on or adjacent to wood tiles produce +1 wood each day."),

            [UniqueBuildingId.GuildHall] = new(UniqueBuildingId.GuildHall, "Guild Hall",
                "Settlement cost threshold (+50%) starts at 6 built instead of 5."),

            [UniqueBuildingId.Monastery] = new(UniqueBuildingId.Monastery, "Monastery",
                "Once per run: turn one 0 roll into 1 at night (your lowest roll resource)."),

            [UniqueBuildingId.CaravanPost] = new(UniqueBuildingId.CaravanPost, "Caravan Post",
                "Draw 1 extra development card each night."),

            [UniqueBuildingId.FortressOutpost] = new(UniqueBuildingId.FortressOutpost, "Fortress Outpost",
                "Tiles adjacent to your coastal settlements ignore the robber.")
        };

        public static UniqueBuildingDefinition Get(UniqueBuildingId id) => All[id];
    }
}
