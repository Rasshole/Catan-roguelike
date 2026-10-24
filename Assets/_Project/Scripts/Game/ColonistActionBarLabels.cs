namespace CatanRoguelike.Game
{
    /// <summary>Authoritative colonist action-bar strings for tests (survives deferred UI destroy).</summary>
    public static class ColonistActionBarLabels
    {
        public static string SettlementTitle { get; private set; } = ColonistHudCopy.Settlement;

        public static void SetSettlementTitle(string title) =>
            SettlementTitle = string.IsNullOrEmpty(title) ? ColonistHudCopy.Settlement : title;
    }
}
