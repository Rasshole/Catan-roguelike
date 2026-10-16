using System.Collections.Generic;
using System.Text;
using CatanRoguelike.Core.UI;
using UnityEngine;
using UnityEngine.Assertions;

namespace CatanRoguelike.Game
{
    /// <summary>PlayMode helpers for colonist board-only capture sanity checks.</summary>
    public static class ColonistBoardCaptureAssertions
    {
        public static readonly Color OceanReference = ColonistBoardSurfaces.OceanClearColor;
        public static readonly Color SandReference = new(0.82f, 0.74f, 0.52f, 1f);
        public static readonly Color CameraSkyReference = new(0.08f, 0.22f, 0.38f, 1f);

        public static void AssertCenterRegionShowsHexTilesAtBoard(
            Texture2D boardCapture,
            Camera camera,
            TableCamera tableCamera,
            BoardView boardView)
        {
            Assert.IsNotNull(boardCapture);
            Assert.IsNotNull(camera);
            Assert.IsNotNull(tableCamera);
            Assert.IsNotNull(boardView);

            var boardPixel = GameSceneCapture.WorldPointToCaptureTexturePixel(
                boardView.transform.position,
                camera,
                tableCamera,
                boardCapture.width,
                boardCapture.height);
            AssertCenterRegionShowsHexTiles(boardCapture, boardPixel.x, boardPixel.y);
        }

        public static void AssertCenterRegionShowsHexTiles(Texture2D boardCapture) =>
            AssertCenterRegionShowsHexTiles(
                boardCapture,
                boardCapture.width / 2,
                boardCapture.height / 2);

        public static void AssertCenterRegionShowsHexTiles(
            Texture2D boardCapture,
            int sampleCenterX,
            int sampleCenterY,
            float minHexFraction = 0.35f,
            float maxOceanFraction = 0.20f,
            float maxSandFraction = 0.25f)
        {
            int w = boardCapture.width;
            int h = boardCapture.height;
            int rx = Mathf.Max(8, (int)(w * 0.18f));
            int ry = Mathf.Max(8, (int)(h * 0.18f));

            int hexLike = 0;
            int oceanLike = 0;
            int sandLike = 0;
            int skyLike = 0;
            int other = 0;
            int samples = 0;
            var sampleColors = new List<Color>(32);

            for (int x = sampleCenterX - rx; x <= sampleCenterX + rx; x += 10)
            {
                for (int y = sampleCenterY - ry; y <= sampleCenterY + ry; y += 10)
                {
                    if (x < 0 || x >= w || y < 0 || y >= h)
                        continue;

                    var pixel = boardCapture.GetPixel(x, y);
                    samples++;
                    if (sampleColors.Count < 12)
                        sampleColors.Add(pixel);

                    if (IsOceanLike(pixel))
                        oceanLike++;
                    else if (IsSkyLike(pixel))
                        skyLike++;
                    else if (IsSandCoastLike(pixel))
                        sandLike++;
                    else if (IsHexTileLike(pixel))
                        hexLike++;
                    else
                        other++;
                }
            }

            Assert.IsTrue(samples > 0, "Board capture center had no samples.");
            float hexFrac = hexLike / (float)samples;
            float oceanFrac = oceanLike / (float)samples;
            float sandFrac = sandLike / (float)samples;

            if (hexFrac < minHexFraction || oceanFrac > maxOceanFraction || sandFrac > maxSandFraction)
                LogSampleFailure(
                    sampleCenterX,
                    sampleCenterY,
                    samples,
                    hexLike,
                    oceanLike,
                    sandLike,
                    skyLike,
                    other,
                    sampleColors);

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

        private static void LogSampleFailure(
            int centerX,
            int centerY,
            int samples,
            int hexLike,
            int oceanLike,
            int sandLike,
            int skyLike,
            int other,
            List<Color> sampleColors)
        {
            var sb = new StringBuilder();
            sb.AppendLine(
                $"ColonistBoardCaptureAssertions sample @ texture ({centerX},{centerY}) [origin bottom-left]: " +
                $"hex={hexLike} ocean={oceanLike} sand={sandLike} sky={skyLike} other={other} / {samples}");
            for (int i = 0; i < sampleColors.Count; i++)
            {
                var c = sampleColors[i];
                sb.AppendLine(
                    $"  sample[{i}] rgb=({c.r:F3},{c.g:F3},{c.b:F3})");
            }

            Debug.LogWarning(sb.ToString());
        }

        private static bool IsOceanLike(Color c)
        {
            if (ColorDistance(c, OceanReference) < 0.20f)
                return true;
            if (ColorDistance(c, CameraSkyReference) < 0.14f)
                return true;

            // Lit ocean / water in capture (e.g. ~0.22, 0.45, 0.68).
            return c.b >= 0.40f
                && c.b >= c.r
                && c.g >= 0.26f
                && c.g >= c.r * 0.85f;
        }

        private static bool IsSkyLike(Color c) =>
            !IsOceanLike(c) && ColorDistance(c, CameraSkyReference) < 0.10f;

        private static bool IsSandCoastLike(Color c) =>
            ColorDistance(c, SandReference) < 0.12f && !MatchesPalette(c, BoardResourcePalette.Desert, 0.10f);

        private static bool IsHexTileLike(Color c)
        {
            if (IsOceanLike(c) || IsSkyLike(c))
                return false;

            return MatchesPalette(c, BoardResourcePalette.Desert, 0.16f)
                || MatchesPalette(c, BoardResourcePalette.Wood, 0.18f)
                || MatchesPalette(c, BoardResourcePalette.Brick, 0.18f)
                || MatchesPalette(c, BoardResourcePalette.Wheat, 0.18f)
                || MatchesPalette(c, BoardResourcePalette.Sheep, 0.18f)
                || MatchesPalette(c, BoardResourcePalette.Stone, 0.18f);
        }

        private static bool MatchesPalette(Color c, BoardResourcePalette.Rgb rgb, float tolerance) =>
            Mathf.Abs(c.r - rgb.R) <= tolerance
            && Mathf.Abs(c.g - rgb.G) <= tolerance
            && Mathf.Abs(c.b - rgb.B) <= tolerance;

        private static float ColorDistance(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
    }
}
