using CatanRoguelike.Core.UI;
using UnityEngine;
using UnityEngine.Assertions;

namespace CatanRoguelike.Game
{
    /// <summary>PlayMode helpers for colonist board-only capture sanity checks.</summary>
    public static class ColonistBoardCaptureAssertions
    {
        public static readonly Color OceanReference = new(0.05f, 0.28f, 0.48f, 1f);
        public static readonly Color SandReference = new(0.82f, 0.74f, 0.52f, 1f);

        public static void AssertCenterRegionShowsHexTiles(
            Texture2D boardCapture,
            float minHexFraction = 0.35f,
            float maxOceanFraction = 0.20f,
            float maxSandFraction = 0.25f)
        {
            int w = boardCapture.width;
            int h = boardCapture.height;
            int cx = w / 2;
            int cy = h / 2;
            int rx = Mathf.Max(8, (int)(w * 0.18f));
            int ry = Mathf.Max(8, (int)(h * 0.18f));

            int hexLike = 0;
            int oceanLike = 0;
            int sandLike = 0;
            int samples = 0;

            for (int x = cx - rx; x <= cx + rx; x += 10)
            {
                for (int y = cy - ry; y <= cy + ry; y += 10)
                {
                    if (x < 0 || x >= w || y < 0 || y >= h)
                        continue;

                    var pixel = boardCapture.GetPixel(x, y);
                    samples++;
                    if (IsOceanLike(pixel))
                        oceanLike++;
                    else if (IsSandCoastLike(pixel))
                        sandLike++;
                    else if (IsHexTileLike(pixel))
                        hexLike++;
                }
            }

            Assert.IsTrue(samples > 0, "Board capture center had no samples.");
            float hexFrac = hexLike / (float)samples;
            float oceanFrac = oceanLike / (float)samples;
            float sandFrac = sandLike / (float)samples;

            Assert.IsTrue(
                hexFrac >= minHexFraction,
                $"Center board capture should show hex tiles (hexLike={hexFrac:P0}, need >={minHexFraction:P0}).");
            Assert.IsTrue(
                oceanFrac <= maxOceanFraction,
                $"Center board capture has too much ocean (oceanLike={oceanFrac:P0}).");
            Assert.IsTrue(
                sandFrac <= maxSandFraction,
                $"Center board capture has too much sand coast (sandLike={sandFrac:P0}).");
        }

        public static void AssertNumberTokenLabelsUprightForCamera(Camera camera, float minDot = 0.92f)
        {
            Assert.IsNotNull(camera, "Camera required for token label orientation check.");
            NumberTokenLabelOrientation.RefreshAllLabelsForCamera(camera);
            var facings = Object.FindObjectsByType<FlatTokenLabelFacing>(FindObjectsSortMode.None);
            Assert.IsTrue(facings.Length > 0, "Colonist board should spawn number token labels.");

            int upright = 0;
            for (int i = 0; i < facings.Length; i++)
            {
                if (NumberTokenLabelOrientation.IsLabelUprightForCamera(facings[i].transform, camera, minDot))
                    upright++;
            }

            Assert.IsTrue(
                upright > facings.Length / 2,
                $"Expected most token labels upright vs camera.up (upright={upright}/{facings.Length}).");
        }

        private static bool IsOceanLike(Color c) => ColorDistance(c, OceanReference) < 0.12f;

        private static bool IsSandCoastLike(Color c) =>
            ColorDistance(c, SandReference) < 0.10f && !MatchesPalette(c, BoardResourcePalette.Desert, 0.08f);

        private static bool IsHexTileLike(Color c)
        {
            if (IsOceanLike(c))
                return false;

            return MatchesPalette(c, BoardResourcePalette.Desert, 0.14f)
                || MatchesPalette(c, BoardResourcePalette.Wood, 0.16f)
                || MatchesPalette(c, BoardResourcePalette.Brick, 0.16f)
                || MatchesPalette(c, BoardResourcePalette.Wheat, 0.16f)
                || MatchesPalette(c, BoardResourcePalette.Sheep, 0.16f)
                || MatchesPalette(c, BoardResourcePalette.Stone, 0.16f);
        }

        private static bool MatchesPalette(Color c, BoardResourcePalette.Rgb rgb, float tolerance) =>
            Mathf.Abs(c.r - rgb.R) <= tolerance
            && Mathf.Abs(c.g - rgb.G) <= tolerance
            && Mathf.Abs(c.b - rgb.B) <= tolerance;

        private static float ColorDistance(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
    }
}
