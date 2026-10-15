namespace CatanRoguelike.Core.UI
{
    /// <summary>Danish resource labels for colonist-style HUD (matches in-game copy tone).</summary>
    public static class ResourceDisplayNames
    {
        public static string GetDanish(ResourceType resource) => resource switch
        {
            ResourceType.Wood => "Træ",
            ResourceType.Brick => "Tegl",
            ResourceType.Wheat => "Korn",
            ResourceType.Sheep => "Uld",
            ResourceType.Stone => "Sten",
            _ => resource.ToString()
        };
    }
}
