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
            return sb.Length == 0 ? "Free" : sb.ToString().TrimEnd();
        }

        public static string FormatButtonLabel(string action, ResourceBundle cost, bool canAfford)
        {
            string suffix = canAfford ? string.Empty : " (can't afford)";
            return $"{action} · {FormatShort(cost)}{suffix}";
        }

        public static string FormatTooltip(ResourceBundle cost) =>
            $"Cost: {FormatShort(cost)}";

        /// <summary>Compact cost chips (L/B/W/G/O resource letters).</summary>
        public static string FormatBankStyleChips(ResourceBundle cost)
        {
            var sb = new StringBuilder();
            AppendChip(sb, cost.Wood, 'L');
            AppendChip(sb, cost.Brick, 'B');
            AppendChip(sb, cost.Wheat, 'W');
            AppendChip(sb, cost.Sheep, 'G');
            AppendChip(sb, cost.Stone, 'O');
            return sb.Length == 0 ? "Free" : sb.ToString().TrimEnd();
        }

        private static void AppendChip(StringBuilder sb, int amount, char letter)
        {
            if (amount <= 0)
                return;
            if (sb.Length > 0)
                sb.Append(' ');
            sb.Append(letter);
            sb.Append(amount);
        }

        private static void AppendPart(StringBuilder sb, int amount, ResourceType type)
        {
            if (amount <= 0)
                return;
            if (sb.Length > 0)
                sb.Append(' ');
            sb.Append(amount);
            sb.Append(' ');
            sb.Append(ResourceDisplayNames.GetEnglish(type));
        }
    }
}
