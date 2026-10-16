using System.Collections.Generic;
using System.IO;
using System.Text;
using CatanRoguelike.Core.UI;
using CatanRoguelike.Core.Yield;
using UnityEngine;
using UnityEngine.Assertions;

namespace CatanRoguelike.Game
{
    /// <summary>PlayMode helpers for colonist board-only capture sanity checks.</summary>
    public static class ColonistBoardCaptureAssertions
    {
        public enum CapturePixelKind
        {
            TokenChip,
            HexTile,
            Ocean,
            Sky,
            SandCoast,
            Other
        }

        public static CapturePixelKind ClassifyCapturePixel(Color pixel) =>
            ClassifyPixelKind(pixel);

        public static readonly Color OceanReference = ColonistBoardSurfaces.OceanClearColor;
        public static readonly Color SandReference = new(0.82f, 0.74f, 0.52f, 1f);
        public static readonly Color CameraSkyReference = new(0.08f, 0.22f, 0.38f, 1f);

        public static void AssertColonistBoardCaptureShowsHexTiles(
            Texture2D boardCapture,
            int screenWidth,
            int screenHeight)
        {
            AssertColonistBoardCaptureShowsHexTiles(boardCapture, null, null, null, screenWidth, screenHeight);
        }

        public static void AssertColonistBoardCaptureShowsHexTiles(
            Texture2D boardCapture,
            Camera camera,
            TableCamera tableCamera,
            BoardView boardView,
            int screenWidth,
            int screenHeight)
        {
            Assert.IsNotNull(boardCapture);
            var center = ResolveCaptureSampleCenter(
                boardCapture,
                camera,
                tableCamera,
                boardView,
                screenWidth,
                screenHeight);

            Debug.Log(
                $"ColonistBoardCaptureAssertions sample texture {boardCapture.width}x{boardCapture.height} " +
                $"id={boardCapture.GetInstanceID()} center=({center.x},{center.y}) [bottom-left origin]");
            WriteSamplePatchDebugPng(boardCapture, center.x, center.y);
            AssertCenterRegionShowsHexTiles(boardCapture, center.x, center.y);
        }

        private static Vector2Int ResolveCaptureSampleCenter(
            Texture2D boardCapture,
            Camera camera,
            TableCamera tableCamera,
            BoardView boardView,
            int screenWidth,
            int screenHeight)
        {
            int texW = boardCapture.width;
            int texH = boardCapture.height;

            if (camera != null && tableCamera != null && boardView != null)
            {
                return ColonistCaptureProjection.WorldToTexturePixel(
                    boardView.GetCaptureHexSampleWorldPoint(),
                    camera,
                    tableCamera,
                    texW,
                    texH);
            }

            var fallback = ColonistBoardFraming.GetHexColorSampleTexturePixels(texW, texH);
            if (texW == screenWidth && texH == screenHeight)
                return fallback;

            float sx = texW / (float)Mathf.Max(1, screenWidth);
            float sy = texH / (float)Mathf.Max(1, screenHeight);
            return new Vector2Int(
                Mathf.RoundToInt(fallback.x * sx),
                Mathf.RoundToInt(fallback.y * sy));
        }

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
            const int sampleRadius = 20;
            int rx = sampleRadius;
            int ry = sampleRadius;

            int hexLike = 0;
            int oceanLike = 0;
            int sandLike = 0;
            int skyLike = 0;
            int other = 0;
            int samples = 0;
            var sampleColors = new List<Color>(32);

            for (int x = sampleCenterX - rx; x <= sampleCenterX + rx; x += 8)
            {
                for (int y = sampleCenterY - ry; y <= sampleCenterY + ry; y += 8)
                {
                    if (x < 0 || x >= w || y < 0 || y >= h)
                        continue;

                    var pixel = boardCapture.GetPixel(x, y);
                    samples++;
                    if (sampleColors.Count < 12)
                        sampleColors.Add(pixel);

                    ClassifyPixel(pixel, ref hexLike, ref oceanLike, ref sandLike, ref skyLike, ref other);
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

        private static Vector2Int FindBestHexClusterCenter(Texture2D capture, int screenWidth, int screenHeight)
        {
            var area = ColonistPlayAreaLayout.Compute(screenWidth, screenHeight);
            int bestX = Mathf.RoundToInt(area.Left + area.PlayWidth * 0.5f);
            int bestY = Mathf.RoundToInt(area.Bottom + area.PlayHeight * 0.5f);
            float bestHex = 0f;

            int startX = Mathf.RoundToInt(area.Left);
            int endX = Mathf.RoundToInt(area.Left + area.PlayWidth);
            int startY = Mathf.RoundToInt(area.Bottom);
            int endY = Mathf.RoundToInt(area.Bottom + area.PlayHeight);

            for (int x = startX; x < endX; x += 48)
            {
                for (int y = startY; y < endY; y += 48)
                {
                    if (TryMeasureHexFraction(capture, x, y, out float hexFrac) && hexFrac > bestHex)
                    {
                        bestHex = hexFrac;
                        bestX = x;
                        bestY = y;
                    }
                }
            }

            return new Vector2Int(bestX, bestY);
        }

        private static bool TryMeasureHexFraction(Texture2D capture, int centerX, int centerY, out float hexFraction)
        {
            hexFraction = 0f;
            int rx = 24;
            int ry = 24;
            int hexLike = 0;
            int samples = 0;
            for (int x = centerX - rx; x <= centerX + rx; x += 8)
            {
                for (int y = centerY - ry; y <= centerY + ry; y += 8)
                {
                    if (x < 0 || x >= capture.width || y < 0 || y >= capture.height)
                        continue;
                    samples++;
                    if (IsHexTileLike(capture.GetPixel(x, y)))
                        hexLike++;
                }
            }

            if (samples == 0)
                return false;

            hexFraction = hexLike / (float)samples;
            return true;
        }

        private static CapturePixelKind ClassifyPixelKind(Color pixel)
        {
            if (MatchesTokenChipColor(pixel))
                return CapturePixelKind.TokenChip;
            if (MatchesHexPaletteColor(pixel))
                return CapturePixelKind.HexTile;
            if (MatchesOceanColor(pixel))
                return CapturePixelKind.Ocean;
            if (MatchesSkyColor(pixel))
                return CapturePixelKind.Sky;
            if (MatchesSandCoastColor(pixel))
                return CapturePixelKind.SandCoast;
            return CapturePixelKind.Other;
        }

        private static void ClassifyPixel(
            Color pixel,
            ref int hexLike,
            ref int oceanLike,
            ref int sandLike,
            ref int skyLike,
            ref int other)
        {
            switch (ClassifyPixelKind(pixel))
            {
                case CapturePixelKind.TokenChip:
                case CapturePixelKind.HexTile:
                    hexLike++;
                    break;
                case CapturePixelKind.Ocean:
                    oceanLike++;
                    break;
                case CapturePixelKind.Sky:
                    skyLike++;
                    break;
                case CapturePixelKind.SandCoast:
                    sandLike++;
                    break;
                default:
                    other++;
                    break;
            }
        }

        public static void WriteSamplePatchDebugPng(Texture2D boardCapture, int centerX, int centerY, int radius = 24)
        {
            if (boardCapture == null)
                return;

            int size = radius * 2 + 1;
            var patch = new Texture2D(size, size, TextureFormat.RGB24, false);
            int w = boardCapture.width;
            int h = boardCapture.height;
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    int sx = centerX - radius + px;
                    int sy = centerY - radius + py;
                    Color c = sx >= 0 && sx < w && sy >= 0 && sy < h
                        ? boardCapture.GetPixel(sx, sy)
                        : Color.magenta;
                    patch.SetPixel(px, py, c);
                }
            }

            patch.Apply();
            string path = Path.Combine(Path.GetTempPath(), "colonist-capture-sample-patch.png");
            File.WriteAllBytes(path, patch.EncodeToPNG());
            Debug.Log(
                $"ColonistBoardCaptureAssertions wrote sample patch ({centerX},{centerY}) r={radius} to {path} " +
                $"centerRgb={boardCapture.GetPixel(Mathf.Clamp(centerX,0,w-1), Mathf.Clamp(centerY,0,h-1))} " +
                $"yFlippedRgb={boardCapture.GetPixel(Mathf.Clamp(centerX,0,w-1), Mathf.Clamp(h-1-centerY,0,h-1))}");
            Object.Destroy(patch);
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

        private static bool MatchesOceanColor(Color c)
        {
            if (ColorDistance(c, OceanReference) < 0.22f)
                return true;

            return c.b >= 0.48f
                && c.b > c.g + 0.08f
                && c.b >= c.r
                && c.g >= 0.26f;
        }

        private static bool MatchesSkyColor(Color c) =>
            ColorDistance(c, CameraSkyReference) < 0.10f;

        private static bool MatchesSandCoastColor(Color c) =>
            ColorDistance(c, SandReference) < 0.12f && !MatchesPalette(c, BoardResourcePalette.Desert, 0.10f);

        private static bool MatchesTokenChipColor(Color c)
        {
            if (c.a < 0.5f)
                return false;

            return MatchesTokenRgb(c, NumberTokenVisualStyle.ChipFaceStandard, 0.14f)
                || MatchesTokenRgb(c, NumberTokenVisualStyle.ChipFaceRedAccent, 0.14f)
                || MatchesTokenRgb(c, NumberTokenVisualStyle.ChipRimStandard, 0.16f)
                || MatchesTokenRgb(c, NumberTokenVisualStyle.ChipRimRedAccent, 0.16f);
        }

        private static bool MatchesTokenRgb(Color c, NumberTokenVisualStyle.Rgb rgb, float tolerance) =>
            Mathf.Abs(c.r - rgb.R) <= tolerance
            && Mathf.Abs(c.g - rgb.G) <= tolerance
            && Mathf.Abs(c.b - rgb.B) <= tolerance;

        private static bool MatchesHexPaletteColor(Color c) =>
            MatchesPalette(c, BoardResourcePalette.Desert, 0.16f)
            || MatchesPalette(c, BoardResourcePalette.Wood, 0.18f)
            || MatchesPalette(c, BoardResourcePalette.Brick, 0.18f)
            || MatchesPalette(c, BoardResourcePalette.Wheat, 0.18f)
            || MatchesPalette(c, BoardResourcePalette.Sheep, 0.18f)
            || MatchesPalette(c, BoardResourcePalette.Stone, 0.18f)
            || MatchesLitResourceHex(c);

        private static bool MatchesLitResourceHex(Color c) =>
            c.g >= 0.55f && c.g > c.r + 0.12f && c.g >= c.b + 0.05f;

        private static bool IsHexTileLike(Color c) => MatchesHexPaletteColor(c);

        private static bool IsTokenChipLike(Color c) => MatchesTokenChipColor(c);

        private static bool MatchesPalette(Color c, BoardResourcePalette.Rgb rgb, float tolerance) =>
            Mathf.Abs(c.r - rgb.R) <= tolerance
            && Mathf.Abs(c.g - rgb.G) <= tolerance
            && Mathf.Abs(c.b - rgb.B) <= tolerance;

        private static float ColorDistance(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        public static void AssertTokenRimRingVisibleOnBoardCapture(
            Texture2D boardCapture,
            Camera camera,
            TableCamera tableCamera,
            HexTileView tokenTile)
        {
            Assert.IsNotNull(boardCapture);
            Assert.IsNotNull(camera);
            Assert.IsNotNull(tableCamera);
            Assert.IsNotNull(tokenTile);

            int w = boardCapture.width;
            int h = boardCapture.height;
            Vector3 chipCenter = tokenTile.transform.position;
            chipCenter.y = tokenTile.ChipTopWorldY - 0.01f;
            var facePx = ColonistCaptureProjection.WorldToTexturePixel(chipCenter, camera, tableCamera, w, h);

            float rimOffsetWorld = tokenTile.TokenRimWorldRadius * 0.88f;
            var rimWorld = chipCenter + new Vector3(rimOffsetWorld, 0f, 0f);
            var rimPx = ColonistCaptureProjection.WorldToTexturePixel(rimWorld, camera, tableCamera, w, h);

            Color face = boardCapture.GetPixel(facePx.x, facePx.y);
            Color rim = boardCapture.GetPixel(rimPx.x, rimPx.y);
            float delta = ColorDistance(face, rim);
            Assert.Greater(
                delta,
                0.06f,
                $"Token rim pixel should differ from face (delta={delta:F3} face={face} rim={rim}).");
            Assert.IsTrue(
                MatchesTokenChipColor(face),
                "Face sample should match token chip palette.");
            Assert.IsTrue(
                MatchesTokenChipColor(rim),
                "Rim sample should match token chip palette.");
        }
    }
}
