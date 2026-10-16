namespace CatanRoguelike.Core.UI
{
    public static class ResourceDisplayNames
    {
        public static string GetEnglish(ResourceType resource) => resource switch
        {
            ResourceType.Wood => "Lumber",
            ResourceType.Brick => "Brick",
            ResourceType.Wheat => "Wheat",
            ResourceType.Sheep => "Wool",
            ResourceType.Stone => "Ore",
            _ => resource.ToString()
        };

        /// <summary>Legacy alias — English labels for colonist HUD.</summary>
        public static string GetDanish(ResourceType resource) => GetEnglish(resource);
    }
}
