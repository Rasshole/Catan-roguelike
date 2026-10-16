using System.Collections.Generic;
using System.IO;
using System.Text;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Map;
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
        public static readonly Color SandReference = ColonistBoardSurfaces.SandAlbedo;
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
            foreach (var tile in Object.FindObjectsByType<HexTileView>(FindObjectsSortMode.None))
            {
                if (!tile.Data.IsFishTile || tile.Data.IsDesert)
                {
                    ColonistBoardTerrainDiagnostics.LogSampleLandHexRenderers(tile);
                    break;
                }
            }

            AssertCenterRegionShowsHexTiles(boardCapture, center.x, center.y);
        }

        public static void AssertLandHexCenterPixelsNotSandColor(
            Texture2D boardCapture,
            Camera camera,
            TableCamera tableCamera)
        {
            Assert.IsNotNull(boardCapture);
            Assert.IsNotNull(camera);
            Assert.IsNotNull(tableCamera);

            int w = boardCapture.width;
            int h = boardCapture.height;
            int checkedTiles = 0;
            foreach (var tile in Object.FindObjectsByType<HexTileView>(FindObjectsSortMode.None))
            {
                if (tile.Data.IsFishTile)
                    continue;

                var world = new Vector3(tile.transform.position.x, tile.HexTopWorldY, tile.transform.position.z);
                var px = ColonistCaptureProjection.WorldToTexturePixel(world, camera, tableCamera, w, h);
                Color pixel = boardCapture.GetPixel(px.x, px.y);
                Assert.IsFalse(
                    MatchesSandCoastColor(pixel),
                    $"Land hex {tile.Data.Coord} center should show terrain, not sand (rgb=({pixel.r:F3},{pixel.g:F3},{pixel.b:F3}) " +
                    $"tex=({px.x},{px.y}) world={world}).");
                checkedTiles++;
            }

            Assert.IsTrue(
                checkedTiles > 6,
                "Board capture should sample multiple land hex centers.");
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

        /// <summary>Labels must show classic dice values 2–12 (not pip weights); 6/8 use red ink.</summary>
        public static void AssertNumberTokenLabelsMatchBoardDiceValues(Camera camera)
        {
            Assert.IsNotNull(camera, "Camera required for token label checks.");
            NumberTokenLabelOrientation.RefreshAllLabelsForCamera(camera);

            var tiles = Object.FindObjectsByType<HexTileView>(FindObjectsSortMode.None);
            int checkedTiles = 0;
            bool sawRedSixOrEight = false;

            foreach (var tile in tiles)
            {
                if (tile.Data.IsDesert || !tile.Data.NumberToken.HasValue)
                    continue;

                int token = tile.Data.NumberToken.Value;
                Assert.IsTrue(token >= 2 && token <= 12 && token != 7,
                    $"Token on {tile.Data.Coord} should be a classic dice value 2–12 (not 7), got {token}.");
                Assert.AreEqual(token.ToString(), tile.TokenLabelText,
                    $"Label on {tile.Data.Coord} should display dice number {token}.");
                Assert.AreEqual(0, tile.PipChildCount,
                    $"Token {token} on {tile.Data.Coord} should not use pip dots.");

                if (NumberTokenLibrary.IsRedNumber(token))
                {
                    var expected = NumberTokenVisualStyle.GetLabel(token);
                    var c = tile.TokenLabelColor;
                    Assert.IsTrue(Mathf.Abs(c.r - expected.R) < 0.08f,
                        $"Token {token} label should use red dice ink.");
                    sawRedSixOrEight = true;
                }

                checkedTiles++;
            }

            Assert.IsTrue(checkedTiles > 12,
                "Base board capture should show many numbered tokens (19-hex / classic pool).");
            Assert.IsTrue(sawRedSixOrEight,
                "Classic board should render at least one red 6 or 8 token label.");
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
            ColonistSceneDestroy.SafeDestroy(patch);
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
            if (MatchesDesertLakeAccent(c))
                return false;

            if (ColorDistance(c, OceanReference) < 0.22f)
                return true;

            return c.b >= 0.48f
                && c.b > c.g + 0.08f
                && c.b >= c.r
                && c.g >= 0.26f;
        }

        private static bool MatchesDesertLakeAccent(Color c) =>
            c.r >= 0.30f && c.r <= 0.42f
            && c.g >= 0.48f && c.g <= 0.58f
            && c.b >= 0.52f && c.b <= 0.65f
            && c.g > c.r + 0.08f;

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

        public static void AssertPortRateLabelVisibleOnBoardCapture(
            Texture2D boardCapture,
            Camera camera,
            TableCamera tableCamera,
            int textureWidth,
            int textureHeight)
        {
            Assert.IsNotNull(boardCapture);
            Assert.IsNotNull(camera);
            Assert.IsNotNull(tableCamera);

            Transform labelTransform = null;
            Renderer discRenderer = null;
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer == null)
                    continue;
                if (renderer.gameObject.name == "RateLabel")
                    labelTransform = renderer.transform;
                if (renderer.gameObject.name == "BadgeDisc")
                    discRenderer = renderer;
            }

            Assert.IsNotNull(labelTransform, "Expected a port RateLabel quad on the board.");
            Assert.IsNotNull(discRenderer, "Expected a port BadgeDisc for reference height.");
            ColonistFlatBoardQuad.LogDiagnostics("PortRateLabelCapture", labelTransform, discRenderer);

            var robberIcon = FindRobberTopIconTransform();
            if (robberIcon != null)
            {
                Renderer pawnTop = null;
                foreach (var renderer in robberIcon.parent.GetComponentsInChildren<Renderer>())
                {
                    if (renderer.transform == robberIcon)
                        continue;
                    if (pawnTop == null || renderer.bounds.max.y > pawnTop.bounds.max.y)
                        pawnTop = renderer;
                }

                ColonistFlatBoardQuad.LogDiagnostics("RobberTopIconCapture", robberIcon, pawnTop);
            }

            var labelRenderer = labelTransform.GetComponent<Renderer>();
            Assert.IsNotNull(labelRenderer);
            Bounds labelBounds = labelRenderer.bounds;
            Vector3 sampleWorld = labelBounds.center;
            var pixel = ColonistCaptureProjection.WorldToTexturePixel(
                sampleWorld,
                camera,
                tableCamera,
                textureWidth,
                textureHeight);

            int darkSamples = 0;
            int totalSamples = 0;
            const int patchRadius = 10;
            for (int dx = -patchRadius; dx <= patchRadius; dx++)
            {
                for (int dy = -patchRadius; dy <= patchRadius; dy++)
                {
                    if (dx * dx + dy * dy > patchRadius * patchRadius)
                        continue;

                    int x = Mathf.Clamp(pixel.x + dx, 0, boardCapture.width - 1);
                    int y = Mathf.Clamp(pixel.y + dy, 0, boardCapture.height - 1);
                    Color c = boardCapture.GetPixel(x, y);
                    float luminance = c.r + c.g + c.b;
                    if (luminance < 0.62f)
                        darkSamples++;
                    totalSamples++;
                }
            }

            Assert.IsTrue(
                darkSamples >= 4,
                $"Port rate label patch should include dark glyph pixels (dark={darkSamples}/{totalSamples} at tex={pixel} world={sampleWorld}).");
        }

        private static Transform FindRobberTopIconTransform()
        {
            foreach (var tile in Object.FindObjectsByType<HexTileView>(FindObjectsSortMode.None))
            {
                if (!tile.Data.HasRobber)
                    continue;
                var icon = tile.transform.Find("RobberPawn/RobberTopIcon");
                if (icon != null)
                    return icon;
            }

            return null;
        }

        public static void AssertRobberPawnReadableOnBoardCapture(
            Camera camera,
            TableCamera tableCamera,
            int textureWidth,
            int textureHeight,
            float minScreenHeightPixels = 25f)
        {
            Assert.IsNotNull(camera);
            Assert.IsNotNull(tableCamera);

            HexTileView robberTile = null;
            foreach (var tile in Object.FindObjectsByType<HexTileView>(FindObjectsSortMode.None))
            {
                if (tile.Data.HasRobber)
                {
                    robberTile = tile;
                    break;
                }
            }

            Assert.IsNotNull(robberTile, "Board should have a robber tile for capture assertion.");
            Assert.IsNotNull(robberTile.transform.Find("RobberPawn"), "Robber hex should use RobberPawn root.");

            float screenHeight = ColonistCaptureProjection.MeasureBoundsVerticalScreenPixels(
                robberTile.RobberWorldBounds,
                camera,
                tableCamera,
                textureWidth,
                textureHeight);
            Assert.IsTrue(
                screenHeight > minScreenHeightPixels,
                $"Robber pawn should be readable on capture (screenHeight={screenHeight:F1}px bounds={robberTile.RobberWorldBounds.size}).");
        }

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
            Assert.IsTrue(tokenTile.Data.NumberToken.HasValue, "Token tile needs a number for rim palette.");

            int w = boardCapture.width;
            int h = boardCapture.height;
            int token = tokenTile.Data.NumberToken.Value;
            var faceRenderer = tokenTile.TokenFaceRenderer;
            var rimRenderer = tokenTile.TokenRimRenderer;
            Assert.IsNotNull(faceRenderer, "Token tile needs a face renderer for rim sampling.");
            Assert.IsNotNull(rimRenderer, "Token tile needs a rim renderer for rim sampling.");

            Vector3 center = faceRenderer.bounds.center;
            var centerPx = ColonistCaptureProjection.WorldToTexturePixel(center, camera, tableCamera, w, h);
            var centerPxV = new Vector2(centerPx.x, centerPx.y);

            Vector3 radialWorld = camera.transform.right;
            Vector3 faceEdgeWorld = center + radialWorld * tokenTile.TokenFaceWorldRadius;
            float midRimWorldRadius = tokenTile.TokenFaceWorldRadius + HexTileView.TokenRimBandWorldWidth * 0.5f;
            Vector3 rimProbeWorld = center + radialWorld * midRimWorldRadius;
            Vector3 rimOuterWorld = center + radialWorld * tokenTile.TokenRimOuterWorldRadius;
            Vector3 faceSampleWorld = center + radialWorld * (tokenTile.TokenFaceWorldRadius * 0.35f);

            var faceEdgePx = ColonistCaptureProjection.WorldToTexturePixel(
                faceEdgeWorld, camera, tableCamera, w, h);
            var rimProbePx = ColonistCaptureProjection.WorldToTexturePixel(
                rimProbeWorld, camera, tableCamera, w, h);
            var faceSamplePxV = ColonistCaptureProjection.WorldToTexturePixel(
                faceSampleWorld, camera, tableCamera, w, h);

            var screenRight = new Vector2(faceEdgePx.x - centerPx.x, faceEdgePx.y - centerPx.y);
            if (screenRight.sqrMagnitude < 1e-4f)
            {
                radialWorld = camera.transform.up;
                faceEdgeWorld = center + radialWorld * tokenTile.TokenFaceWorldRadius;
                rimProbeWorld = center + radialWorld * midRimWorldRadius;
                faceSampleWorld = center + radialWorld * (tokenTile.TokenFaceWorldRadius * 0.35f);
                faceEdgePx = ColonistCaptureProjection.WorldToTexturePixel(
                    faceEdgeWorld, camera, tableCamera, w, h);
                rimProbePx = ColonistCaptureProjection.WorldToTexturePixel(
                    rimProbeWorld, camera, tableCamera, w, h);
                faceSamplePxV = ColonistCaptureProjection.WorldToTexturePixel(
                    faceSampleWorld, camera, tableCamera, w, h);
                screenRight = new Vector2(faceEdgePx.x - centerPx.x, faceEdgePx.y - centerPx.y);
            }

            screenRight.Normalize();
            float faceRadiusPixels = Vector2.Distance(centerPxV, new Vector2(faceEdgePx.x, faceEdgePx.y));
            float rimRadiusPixels = ColonistBoardScreenMetrics.MeasureProjectedRadiusFromCenter(
                rimRenderer, camera, tableCamera, w, h, center);
            var rimOuterPx = ColonistCaptureProjection.WorldToTexturePixel(
                rimOuterWorld, camera, tableCamera, w, h);
            float rimOuterRadiusPixels = Vector2.Distance(
                centerPxV, new Vector2(rimOuterPx.x, rimOuterPx.y));

            float rimBandWidthPx = Mathf.Max(0.01f, rimOuterRadiusPixels - faceRadiusPixels);
            float rimProbeRadiusPixels = Vector2.Distance(
                centerPxV, new Vector2(rimProbePx.x, rimProbePx.y));
            var faceSamplePx = new Vector2(faceSamplePxV.x, faceSamplePxV.y);
            var rimSamplePx = new Vector2(rimProbePx.x, rimProbePx.y);
            int facePxX = Mathf.Clamp(Mathf.RoundToInt(faceSamplePx.x), 0, w - 1);
            int facePxY = Mathf.Clamp(Mathf.RoundToInt(faceSamplePx.y), 0, h - 1);
            int rimPxX = Mathf.Clamp(Mathf.RoundToInt(rimSamplePx.x), 0, w - 1);
            int rimPxY = Mathf.Clamp(Mathf.RoundToInt(rimSamplePx.y), 0, h - 1);
            float pixelSeparation = Vector2.Distance(faceSamplePx, rimSamplePx);
            float centerToRimSamplePx = Vector2.Distance(centerPxV, rimSamplePx);
            var faceWorld = faceRenderer.bounds.center;
            var rimWorld = rimRenderer.bounds.center;

            Color face = boardCapture.GetPixel(facePxX, facePxY);
            Color rim = boardCapture.GetPixel(rimPxX, rimPxY);
            float delta = ColorDistance(face, rim);
            Assert.IsTrue(
                delta > 0.06f,
                $"Token rim pixel should differ from face (delta={delta:F3} face={face} rim={rim} " +
                $"faceWorld={faceWorld} rimWorld={rimWorld} faceTex=({facePxX},{facePxY}) rimTex=({rimPxX},{rimPxY}) " +
                $"pixelSep={pixelSeparation:F1} rimRadiusPx~{rimRadiusPixels:F1})).");
            float expectedBandRatio = midRimWorldRadius / Mathf.Max(0.01f, tokenTile.TokenFaceWorldRadius);
            float rimSampleToFaceRatio = centerToRimSamplePx / Mathf.Max(0.01f, faceRadiusPixels);
            Assert.IsTrue(
                Mathf.Abs(rimSampleToFaceRatio - expectedBandRatio) < 0.08f,
                $"Rim sample should sit at mid annulus on screen " +
                $"(sample/face={rimSampleToFaceRatio:F3} expected~{expectedBandRatio:F3} " +
                $"probePx={rimProbeRadiusPixels:F1} bandPx={rimBandWidthPx:F1} faceRadiusPx={faceRadiusPixels:F1} " +
                $"rimRadiusPx={rimRadiusPixels:F1}).");
            Assert.IsTrue(
                rimRadiusPixels > 6f,
                $"Rim radius should project to visible pixels (rimRadiusPx={rimRadiusPixels:F1}).");
            Assert.IsTrue(
                pixelSeparation > 4f,
                $"Face and rim samples should be separated on screen (pixelSep={pixelSeparation:F1}).");
            Assert.IsTrue(
                MatchesTokenFaceColor(face, token),
                $"Face sample should match token face palette (rgb=({face.r:F3},{face.g:F3},{face.b:F3}) token={token} " +
                $"world={faceWorld} tex=({facePxX},{facePxY})).");
            if (!MatchesTokenRimColor(rim, token))
            {
                Debug.LogWarning(
                    $"TokenRimSample token={token} rgb=({rim.r:F3},{rim.g:F3},{rim.b:F3}) " +
                    $"centerPx=({centerPx.x},{centerPx.y}) rimSamplePx=({rimSamplePx.x:F1},{rimSamplePx.y:F1}) " +
                    $"rimRadiusPx={rimRadiusPixels:F1} probePx={rimProbeRadiusPixels:F1} " +
                    $"faceRadiusPx={faceRadiusPixels:F1}");
            }

            Assert.IsTrue(
                MatchesTokenRimColor(rim, token),
                $"Rim sample should match token rim palette (rgb=({rim.r:F3},{rim.g:F3},{rim.b:F3}) token={token} " +
                $"world={rimWorld} tex=({rimPxX},{rimPxY}) rimRadiusPx={rimRadiusPixels:F1}).");
        }

        private static bool MatchesTokenFaceColor(Color c, int token)
        {
            var rgb = NumberTokenVisualStyle.GetChipFace(token);
            return MatchesTokenRgb(c, rgb, 0.16f);
        }

        private static bool MatchesTokenRimColor(Color c, int token)
        {
            var rgb = NumberTokenVisualStyle.GetChipRim(token);
            return MatchesTokenRgb(c, rgb, 0.18f);
        }

        public static void AssertRenderedPortBadgeDiscCount(int expected = 9)
        {
            int count = 0;
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer != null && renderer.gameObject.name == "BadgeDisc")
                    count++;
            }

            Debug.Log($"ColonistBoardCaptureAssertions port BadgeDisc count={count} (expected {expected}).");
            Assert.AreEqual(expected, count, $"Colonist board should render {expected} port BadgeDisc meshes.");
        }

        public static void AssertColonistLandHexViewCount(int expectedLandHexes = 19)
        {
            int land = 0;
            foreach (var tile in Object.FindObjectsByType<HexTileView>(FindObjectsSortMode.None))
            {
                if (!tile.Data.IsFishTile || tile.Data.IsDesert)
                    land++;
            }

            Debug.Log($"ColonistBoardCaptureAssertions land HexTileView count={land} (expected {expectedLandHexes}).");
            Assert.AreEqual(
                expectedLandHexes,
                land,
                $"Colonist board should show {expectedLandHexes} land hex views (sea fish are separate).");
        }

        public static void AssertCoastalFishShoalsVisibleOnBoardCapture(
            Texture2D boardCapture,
            Camera camera,
            TableCamera tableCamera,
            BoardState board,
            float hexScale,
            int textureWidth,
            int textureHeight)
        {
            Assert.IsNotNull(boardCapture);
            Assert.IsNotNull(camera);
            Assert.IsNotNull(tableCamera);
            Assert.IsNotNull(board);

            int shoals = 0;
            int tealSamples = 0;
            int tealHits = 0;
            foreach (var tile in Object.FindObjectsByType<HexTileView>(FindObjectsSortMode.None))
            {
                if (!BoardState.IsSeaFishGround(tile.Data))
                    continue;

                shoals++;
                if (!SeaFrameSlotLayout.TryGetFishShoalPlacement(
                        board, tile.Data.Coord, hexScale, out FishShoalPlacement placement))
                    continue;

                float y = tile.ChipTopWorldY;
                var probeWorld = new Vector3(placement.SeaTip.X, y, placement.SeaTip.Z);
                var pixel = ColonistCaptureProjection.WorldToTexturePixel(
                    probeWorld, camera, tableCamera, textureWidth, textureHeight);

                for (int dx = -4; dx <= 4; dx++)
                {
                    for (int dy = -4; dy <= 4; dy++)
                    {
                        int x = Mathf.Clamp(pixel.x + dx, 0, boardCapture.width - 1);
                        int yPx = Mathf.Clamp(pixel.y + dy, 0, boardCapture.height - 1);
                        Color c = boardCapture.GetPixel(x, yPx);
                        tealSamples++;
                        if (IsFishShoalTeal(c))
                            tealHits++;
                    }
                }
            }

            Assert.IsTrue(shoals > 2, "Board should render multiple coastal fish shoals.");
            Assert.IsTrue(
                tealHits > Mathf.Max(12, tealSamples / 6),
                $"Fish shoal capture should include teal shoal pixels (teal={tealHits}/{tealSamples}).");
        }

        private static bool IsFishShoalTeal(Color c) =>
            c.g > c.r + 0.06f && c.b > c.r + 0.04f && c.g > 0.42f && c.b > 0.38f;

        public static void AssertFishermenHarnessBoardAndHud(GameState state)
        {
            Assert.IsTrue(state.FishermenOfCatanEnabled, "Harness should enable Fishermen of Catan.");

            int fishGrounds = 0;
            bool sawDesertLake = false;
            foreach (var tile in Object.FindObjectsByType<HexTileView>(FindObjectsSortMode.None))
            {
                if (!tile.Data.IsFishTile)
                    continue;

                if (tile.Data.IsDesert)
                    sawDesertLake = true;
                else
                    fishGrounds++;

                bool hasVisual = tile.transform.Find("FishGroundIcon") != null
                    || tile.transform.Find("ShoalMesh") != null
                    || tile.name.StartsWith(ColonistCoastalFishShoalVisual.ShoalRootName)
                    || tile.transform.Find("SeaFishGround") != null;
                Assert.IsTrue(
                    hasVisual,
                    $"Fish tile at {tile.Data.Coord} should spawn coastal shoal or fish icon.");
            }

            Assert.IsTrue(sawDesertLake, "Desert center should become a fishing lake when Fishermen is enabled.");
            Assert.IsTrue(
                fishGrounds >= 3,
                $"Board should show several sea fishing ground tiles (found {fishGrounds}).");

            var spendPanel = GameObject.Find("FishSpendPanel");
            Assert.IsNotNull(spendPanel, "Fish spend panel should exist on colonist HUD when Fishermen is enabled.");
            Assert.IsTrue(spendPanel.activeInHierarchy, "Fish spend panel should be visible during day actions.");
        }
    }
}
