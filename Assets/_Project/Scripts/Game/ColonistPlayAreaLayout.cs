using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Shared colonist play-area rect for HUD, camera framing, and capture tests.</summary>
    public static class ColonistPlayAreaLayout
    {
        public struct Rect
        {
            public float Left;
            public float Bottom;
            public float Top;
            public float PlayWidth;
            public float PlayHeight;
        }

        public static Rect Compute(int screenWidth, int screenHeight)
        {
            float left = ColonistUiScale.ScaleReferencePixels(
                RunHudLayout.GetContentLeftInset(), screenWidth, screenHeight);
            float right = ColonistUiScale.ScaleReferencePixels(
                RunHudLayout.GetContentRightInset(), screenWidth, screenHeight);
            float bottomInset = ColonistPresentation.IsActive
                // Reserve only what the resting (RestScale) fan and resource hand occupy: the full
                // 158 px deck band left a ~100 px dead strip between board and cards (v113 review).
                // Hovered cards lift over the board on purpose.
                ? RunHudLayout.GetColonistDeckBandBottom()
                + ColonistRoguelikeCardHandLayout.RestingFanHeight
                + RunHudLayout.Margin * 0.75f
                : RunHudLayout.GetBottomHandHeight()
                + RunHudLayout.GetBottomActionBarHeight()
                + RunHudLayout.Margin * 2f;
            float bottom = ColonistUiScale.ScaleReferencePixels(bottomInset, screenWidth, screenHeight);
            float top = ColonistUiScale.ScaleReferencePixels(
                RunHudLayout.Margin + RunHudLayout.ColonistYourTurnBannerHeight + RunHudLayout.Margin * 0.25f,
                screenWidth,
                screenHeight);
            float breathing = ColonistUiScale.ScaleReferencePixels(
                ColonistBoardViewConstants.BoardBottomBreathingRoomPixels, screenWidth, screenHeight);
            float playWidth = Mathf.Max(64f, screenWidth - left - right);
            float playHeight = Mathf.Max(
                64f,
                screenHeight - bottom - top - breathing);

            return new Rect
            {
                Left = left,
                Bottom = bottom,
                Top = top,
                PlayWidth = playWidth,
                PlayHeight = playHeight
            };
        }

        /// <summary>Play-area centre in screen/texture pixels (origin bottom-left).</summary>
        public static Vector2Int GetCenterTexturePixels(int screenWidth, int screenHeight)
        {
            var area = Compute(screenWidth, screenHeight);
            float centerX = area.Left + area.PlayWidth * 0.5f;
            float centerY = area.Bottom + area.PlayHeight * 0.5f;
            return new Vector2Int(
                Mathf.RoundToInt(centerX),
                Mathf.RoundToInt(centerY));
        }
    }
}
