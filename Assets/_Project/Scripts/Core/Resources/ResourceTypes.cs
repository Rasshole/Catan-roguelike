using System.Collections.Generic;

namespace CatanRoguelike.Core
{
    /// <summary>Tradeable / bank / roll resources — excludes terrain-only types.</summary>
    public static class ResourceTypes
    {
        public static readonly ResourceType[] Productive =
        {
            ResourceType.Wood,
            ResourceType.Brick,
            ResourceType.Wheat,
            ResourceType.Sheep,
            ResourceType.Stone
        };

        public static IEnumerable<ResourceType> AllProductive => Productive;
    }
}
