using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Screen-space layout constants for colonist-style in-run HUD.</summary>
    public static class RunHudLayout
    {
        public const float Margin = 10f;
        /// <summary>Height of the colonist "Your turn" banner below the top margin (matches RunHud UI).</summary>
        public const float ColonistYourTurnBannerHeight = 48f;
        public const float SidePanelWidth = 232f;
        public const float RightChromePanelWidth = 268f;
        public const float BottomHandHeight = 168f;
        public const float BottomActionBarHeight = 68f;
        public const float CardWidth = 136f;
        public const float CardHeight = 124f;
        public const float CardSpacing = 10f;

        public static float GetHandRowBottomInset() => Margin;

        public static float GetActionBarBottomInset() =>
            Margin + GetBottomHandHeight() + (ColonistPresentation.IsActive ? Margin * 0.25f : Margin * 0.5f);

        public static float GetBottomHandHeight() =>
            ColonistPresentation.IsActive ? 96f : BottomHandHeight;

        public const float ColonistResourceHandWidth = 500f;

        public static float GetBottomActionBarHeight() =>
            ColonistPresentation.IsActive ? 68f : BottomActionBarHeight;

        public static float GetContentLeftInset() =>
            ColonistPresentation.IsActive ? Margin : Margin + SidePanelWidth + Margin;

        public static float GetContentRightInset() =>
            Margin + RightChromePanelWidth + Margin;

        /// <summary>Play-area centre in screen/texture pixels (origin bottom-left).</summary>
        public static Vector2 GetColonistPlayAreaCenterScreenPixels(int screenWidth, int screenHeight)
        {
            var center = ColonistPlayAreaLayout.GetCenterTexturePixels(screenWidth, screenHeight);
            return new Vector2(center.x, center.y);
        }
    }
}
