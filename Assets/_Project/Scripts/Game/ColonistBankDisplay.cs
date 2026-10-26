using CatanRoguelike.Core.Trade;

namespace CatanRoguelike.Game
{
    internal static class ColonistBankDisplay
    {
        public static string FormatStrip(BankStock bank) =>
            $"Bank  L{bank.Wood} B{bank.Brick} W{bank.Wheat} G{bank.Sheep} O{bank.Stone}";
    }
}
