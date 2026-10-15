namespace CatanRoguelike.Game
{
    /// <summary>Screen-space layout constants for colonist-style in-run HUD.</summary>
    public static class RunHudLayout
    {
        public const float Margin = 10f;
        public const float SidePanelWidth = 280f;
        public const float BottomHandHeight = 148f;
        public const float BottomActionBarHeight = 52f;
        public const float CardWidth = 132f;
        public const float CardHeight = 118f;
        public const float CardSpacing = 10f;

        public static float GetHandRowBottomInset() => Margin;

        public static float GetActionBarBottomInset() => Margin + BottomHandHeight + Margin * 0.5f;
    }
}
