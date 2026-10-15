using System.Text;
using CatanRoguelike.Core;

namespace CatanRoguelike.Core.UI
{
    public static class BuildCostFormatter
    {
        public static string FormatShort(ResourceBundle cost)
        {
            var sb = new StringBuilder();
            AppendPart(sb, cost.Wood, ResourceType.Wood);
            AppendPart(sb, cost.Brick, ResourceType.Brick);
            AppendPart(sb, cost.Wheat, ResourceType.Wheat);
            AppendPart(sb, cost.Sheep, ResourceType.Sheep);
            AppendPart(sb, cost.Stone, ResourceType.Stone);
            return sb.Length == 0 ? "Gratis" : sb.ToString().TrimEnd();
        }

        public static string FormatButtonLabel(string action, ResourceBundle cost, bool canAfford)
        {
            string suffix = canAfford ? string.Empty : " (mangler)";
            return $"{action} · {FormatShort(cost)}{suffix}";
        }

        private static void AppendPart(StringBuilder sb, int amount, ResourceType type)
        {
            if (amount <= 0)
                return;
            if (sb.Length > 0)
                sb.Append(' ');
            sb.Append(amount);
            sb.Append(' ');
            sb.Append(ResourceDisplayNames.GetDanish(type));
        }
    }
}
