namespace CatanRoguelike.Core.UI
{
    public static class HudResourceFormatting
    {
        public static string FormatQuantity(int amount, ResourceType resource) =>
            $"{amount} {ResourceDisplayNames.GetDanish(resource)}";

        public static string FormatInventoryLine(ResourceBundle bundle, ResourceType resource)
        {
            int amount = bundle[resource];
            return FormatQuantity(amount, resource);
        }
    }
}
