using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Fit colonist board + sand ring inside the HUD play-area rect.</summary>
    public static class ColonistBoardFraming
    {
        public const float PlayAreaMarginPixels = 24f;

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
            return Mathf.Max(fromHeight, fromWidth);
        }

        public static ColonistPlayAreaLayout.Rect GetPlayArea(int screenWidth, int screenHeight) =>
            ColonistPlayAreaLayout.Compute(screenWidth, screenHeight);

        public static Vector2Int GetPlayAreaCenterTexturePixels(int screenWidth, int screenHeight) =>
            ColonistPlayAreaLayout.GetCenterTexturePixels(screenWidth, screenHeight);

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
