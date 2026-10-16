using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Fit colonist board + sand ring inside the HUD play-area rect.</summary>
    public static class ColonistBoardFraming
    {
        public const float PlayAreaMarginPixels = 24f;

        /// <summary>Multiply ortho size (&lt;1 fills more of the play area).</summary>
        public const float TargetBoardFillScale = 0.9f;

        public static float ComputeOrthographicSize(
            float boardExtentRadiusWorld,
            int screenWidth,
            int screenHeight,
            float cameraAspect)
        {
            var play = ColonistPlayAreaLayout.Compute(screenWidth, screenHeight);
            float usableWidth = Mathf.Max(8f, play.PlayWidth - PlayAreaMarginPixels * 2f);
            float usableHeight = Mathf.Max(8f, play.PlayHeight - PlayAreaMarginPixels * 2f);
            float diameter = Mathf.Max(0.01f, boardExtentRadiusWorld * 2f);

            float fromHeight = diameter * screenHeight / (2f * usableHeight);
            float fromWidth = diameter * screenWidth / (2f * usableWidth * Mathf.Max(0.01f, cameraAspect));
            return Mathf.Max(fromHeight, fromWidth) * TargetBoardFillScale;
        }

        public static void LogPlayAreaFitDiagnostics(
            Vector2[] screenPixelsFromBottom,
            int screenWidth,
            int screenHeight)
        {
            var play = ColonistPlayAreaLayout.Compute(screenWidth, screenHeight);
            float minX = play.Left + PlayAreaMarginPixels;
            float maxX = play.Left + play.PlayWidth - PlayAreaMarginPixels;
            float minY = play.Bottom + PlayAreaMarginPixels;
            float maxY = play.Bottom + play.PlayHeight - PlayAreaMarginPixels;

            for (int i = 0; i < screenPixelsFromBottom.Length; i++)
            {
                var p = screenPixelsFromBottom[i];
                Debug.LogWarning(
                    $"ColonistBoardFraming extent[{i}] screen=({p.x:F1},{p.y:F1}) " +
                    $"inside=({p.x >= minX && p.x <= maxX && p.y >= minY && p.y <= maxY})");
            }

            Debug.LogWarning(
                $"ColonistBoardFraming playRect margin=({minX:F1},{minY:F1})-({maxX:F1},{maxY:F1}) " +
                $"play=({play.Left:F1},{play.Bottom:F1}) {play.PlayWidth:F0}x{play.PlayHeight:F0}");
        }

        public static ColonistPlayAreaLayout.Rect GetPlayArea(int screenWidth, int screenHeight) =>
            ColonistPlayAreaLayout.Compute(screenWidth, screenHeight);

        public static Vector2Int GetPlayAreaCenterTexturePixels(int screenWidth, int screenHeight) =>
            ColonistPlayAreaLayout.GetCenterTexturePixels(screenWidth, screenHeight);

        /// <summary>Offset from play-area centre toward a hex corner (avoids centre token).</summary>
        public static Vector2Int GetHexColorSampleTexturePixels(int screenWidth, int screenHeight)
        {
            var center = GetPlayAreaCenterTexturePixels(screenWidth, screenHeight);
            return new Vector2Int(center.x + 72, center.y + 56);
        }

        /// <summary>Sample points on the board extent circle (world XZ).</summary>
        public static void GetExtentSamplePoints(Vector3 boardCenter, float extentRadius, Vector3[] buffer)
        {
            int count = buffer.Length;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                buffer[i] = new Vector3(
                    boardCenter.x + Mathf.Cos(angle) * extentRadius,
                    boardCenter.y,
                    boardCenter.z + Mathf.Sin(angle) * extentRadius);
            }
        }

        public static bool ArePointsInsidePlayArea(
            Vector2[] screenPixelsFromBottom,
            int screenWidth,
            int screenHeight,
            float marginPixels = PlayAreaMarginPixels)
        {
            var play = ColonistPlayAreaLayout.Compute(screenWidth, screenHeight);
            float minX = play.Left + marginPixels;
            float maxX = play.Left + play.PlayWidth - marginPixels;
            float minY = play.Bottom + marginPixels;
            float maxY = play.Bottom + play.PlayHeight - marginPixels;

            for (int i = 0; i < screenPixelsFromBottom.Length; i++)
            {
                var p = screenPixelsFromBottom[i];
                if (p.x < minX || p.x > maxX || p.y < minY || p.y > maxY)
                    return false;
            }

            return true;
        }

        public static Vector2 WorldToScreenPixelsFromBottom(Camera camera, Vector3 world)
        {
            var screen = camera.WorldToScreenPoint(world);
            return new Vector2(screen.x, screen.y);
        }
    }
}
