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

        /// <summary>Fixed chrome insets in reference pixels (v136: the pass-and-play table HUD).</summary>
        public struct Insets
        {
            public float Left;
            public float Right;
            public float Top;
            public float Bottom;
        }

        /// <summary>When set, replaces the run HUD insets (set by the pass-and-play table, cleared on exit).</summary>
        public static Insets? Override { get; set; }

        public static Rect Compute(int screenWidth, int screenHeight)
        {
            if (Override.HasValue)
            {
                var o = Override.Value;
                float l = ColonistUiScale.ScaleReferencePixels(o.Left, screenWidth, screenHeight);
                float r = ColonistUiScale.ScaleReferencePixels(o.Right, screenWidth, screenHeight);
                float t = ColonistUiScale.ScaleReferencePixels(o.Top, screenWidth, screenHeight);
                float b = ColonistUiScale.ScaleReferencePixels(o.Bottom, screenWidth, screenHeight);
                return new Rect
                {
                    Left = l,
                    Bottom = b,
                    Top = t,
                    PlayWidth = Mathf.Max(64f, screenWidth - l - r),
                    PlayHeight = Mathf.Max(64f, screenHeight - b - t)
                };
            }

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
