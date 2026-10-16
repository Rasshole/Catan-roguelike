using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Shared colonist play-area rect for HUD, camera framing, and capture tests.</summary>
    public static class ColonistPlayAreaLayout
    {
        public const float TurnBannerReserve = 56f;

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
            float left = RunHudLayout.GetContentLeftInset();
            float right = RunHudLayout.GetContentRightInset();
            float bottom = RunHudLayout.GetBottomHandHeight()
                + RunHudLayout.GetBottomActionBarHeight()
                + RunHudLayout.Margin * 2f;
            float top = RunHudLayout.Margin + TurnBannerReserve;
            float playWidth = Mathf.Max(64f, screenWidth - left - right);
            float playHeight = Mathf.Max(
                64f,
                screenHeight - bottom - top - ColonistBoardViewConstants.BoardBottomBreathingRoomPixels);

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
