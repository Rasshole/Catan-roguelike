using System.Collections.Generic;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Procedural 2:1 / 3:1 port rate labels (bitmap font, no TextMesh).</summary>
    public static class ColonistPortRateLabelTextures
    {
        private const int GlyphWidth = 5;
        private const int GlyphHeight = 7;
        private const int PixelScale = 4;
        private const int CharGap = 2;

        private static readonly Dictionary<string, Texture2D> Cache = new();

        private static readonly Dictionary<char, string[]> Glyphs = new()
        {
            ['0'] = Rows(
                "01110",
                "10001",
                "10011",
                "10101",
                "11001",
                "10001",
                "01110"),
            ['1'] = Rows(
                "00100",
                "01100",
                "00100",
                "00100",
                "00100",
                "00100",
                "01110"),
            ['2'] = Rows(
                "01110",
                "10001",
                "00001",
                "00110",
                "01000",
                "10000",
                "11111"),
            ['3'] = Rows(
                "01110",
                "10001",
                "00001",
                "00110",
                "00001",
                "10001",
                "01110"),
            [':'] = Rows(
                "00000",
                "00100",
                "00100",
                "00000",
                "00100",
                "00100",
                "00000"),
        };

        public static Texture2D Get(bool isGeneric)
        {
            string key = isGeneric ? "3:1" : "2:1";
            if (Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var tex = Build(key);
            Cache[key] = tex;
            return tex;
        }

        public static bool HasOpaquePixels(Texture2D texture)
        {
            if (texture == null)
                return false;

            var pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a > 16)
                    return true;
            }

            return false;
        }

        private static Texture2D Build(string text)
        {
            int charCount = text.Length;
            int width = charCount * GlyphWidth * PixelScale + (charCount - 1) * CharGap * PixelScale;
            int height = GlyphHeight * PixelScale;

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = $"PortRate_{text}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0, 0, 0, 0);

            var ink = new Color32(28, 32, 40, 255);
            int cursorX = 0;
            foreach (char ch in text)
            {
                if (!Glyphs.TryGetValue(ch, out var rows))
                    continue;
                DrawGlyph(pixels, width, height, cursorX, 0, rows, ink);
                cursorX += GlyphWidth * PixelScale + CharGap * PixelScale;
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private static void DrawGlyph(
            Color32[] pixels,
            int texW,
            int texH,
            int originX,
            int originY,
            string[] rows,
            Color32 ink)
        {
            for (int y = 0; y < GlyphHeight; y++)
            {
                string row = rows[y];
                for (int x = 0; x < GlyphWidth; x++)
                {
                    if (row[x] != '1')
                        continue;
                    FillRect(
                        pixels,
                        texW,
                        texH,
                        originX + x * PixelScale,
                        originY + (GlyphHeight - 1 - y) * PixelScale,
                        PixelScale,
                        PixelScale,
                        ink);
                }
            }
        }

        private static void FillRect(
            Color32[] pixels,
            int texW,
            int texH,
            int x,
            int y,
            int w,
            int h,
            Color32 c)
        {
            for (int py = y; py < y + h; py++)
            {
                for (int px = x; px < x + w; px++)
                {
                    if (px < 0 || px >= texW || py < 0 || py >= texH)
                        continue;
                    pixels[py * texW + px] = c;
                }
            }
        }

        private static string[] Rows(params string[] rows) => rows;
    }
}
