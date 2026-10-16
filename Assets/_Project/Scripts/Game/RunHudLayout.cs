namespace CatanRoguelike.Game
{
    /// <summary>Screen-space layout constants for colonist-style in-run HUD.</summary>
    public static class RunHudLayout
    {
        public const float Margin = 10f;
        public const float SidePanelWidth = 232f;
        public const float RightChromePanelWidth = 300f;
        public const float BottomHandHeight = 168f;
        public const float BottomActionBarHeight = 68f;
        public const float CardWidth = 136f;
        public const float CardHeight = 124f;
        public const float CardSpacing = 10f;

        public static float GetHandRowBottomInset() => Margin;

        public static float GetActionBarBottomInset() =>
            Margin + GetBottomHandHeight() + Margin * 0.5f;

        public static float GetBottomHandHeight() =>
            ColonistPresentation.IsActive ? 172f : BottomHandHeight;

        public static float GetBottomActionBarHeight() =>
            ColonistPresentation.IsActive ? 84f : BottomActionBarHeight;

        public static float GetContentLeftInset() =>
            ColonistPresentation.IsActive ? Margin : Margin + SidePanelWidth + Margin;

        public static float GetContentRightInset() =>
            Margin + RightChromePanelWidth + Margin;
    }
}
