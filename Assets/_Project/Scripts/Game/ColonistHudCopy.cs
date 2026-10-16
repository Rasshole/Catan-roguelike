namespace CatanRoguelike.Game
{
    internal static class ColonistHudCopy
    {
        public const string Roll = "Roll";
        public const string EndTurn = "End Turn";
        public const string Trade = "Trade";
        public const string TradeDialogTitle = "Trade";
        public const string Bank = "Bank";
        public const string OfferAi = "Offer AI";
        public const string Close = "Close";
        public const string Accept = "Accept";
        public const string Road = "Road";
        public const string Settlement = "Settlement";
        public const string City = "City";
        public const string DevCard = "Dev Card";
        public const string CancelPlacement = "Cancel (Esc)";
        public const string Restart = "Restart";
        public const string GameLog = "Game log";
        public const string Chat = "Chat";
        public const string ChatPlaceholder = "Type a message…";
        public const string You = "You";
        public const string Opponent = "Opponent";
        public const string LongestRoad = "Longest road";
        public const string LargestArmy = "Largest army";
        public const string Cards = "Cards";
        public const string DevCards = "Dev cards";

        public static string TranslateStatus(string status)
        {
            if (string.IsNullOrEmpty(status))
                return status;

            return status
                .Replace("Tryk Rul (Space) for at slå terninger.", "Press Roll (Space) to roll the dice.")
                .Replace("Vælg kortstørrelse.", "Choose map size.")
                .Replace("Vælg din leader.", "Choose your leader.")
                .Replace("Setup: klik grønt hjørne (by) eller gul kant (vej).",
                    "Setup: click a green corner (settlement) or yellow edge (road).")
                .Replace("Du vandt!", "You win!")
                .Replace("AI vandt!", "AI wins!")
                .Replace("Genstart", Restart);
        }
    }
}
