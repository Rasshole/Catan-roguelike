using UnityEngine;

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
            ColonistPresentation.IsActive ? 118f : BottomHandHeight;

        public const float ColonistResourceHandWidth = 500f;

        public static float GetBottomActionBarHeight() =>
            ColonistPresentation.IsActive ? 84f : BottomActionBarHeight;

        public static float GetContentLeftInset() =>
            ColonistPresentation.IsActive ? Margin : Margin + SidePanelWidth + Margin;

        public static float GetContentRightInset() =>
            Margin + RightChromePanelWidth + Margin;

        /// <summary>Screen pixel center of the colonist play area (matches <see cref="TableCamera"/> framing).</summary>
        public static Vector2 GetColonistPlayAreaCenterScreenPixels(int screenWidth, int screenHeight)
        {
            float left = GetContentLeftInset();
            float right = GetContentRightInset();
            float bottom = GetBottomHandHeight()
                + GetBottomActionBarHeight()
                + Margin * 2f;
            float top = Margin;
            float playWidth = Mathf.Max(64f, screenWidth - left - right);
            float playHeight = Mathf.Max(64f, screenHeight - bottom - top);
            return new Vector2(left + playWidth * 0.5f, bottom + playHeight * 0.5f);
        }
    }
}
