using System.Collections.Generic;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>2:1 / 3:1 port rate labels using Roboto Slab Bold (same family as number tokens).</summary>
    public static class ColonistPortRateLabelTextures
    {
        private const int FontSize = 52;
        private const int Padding = 10;

        private static readonly Dictionary<string, Texture2D> Cache = new();

        public static Texture2D Get(bool isGeneric)
        {
            string key = isGeneric ? "3:1" : "2:1";
            if (Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var tex = BuildSerif(key) ?? BuildBitmapFallback(key);
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

        private static Texture2D BuildSerif(string text)
        {
            var font = ColonistNumberTokenFont.Get();
            if (font == null)
                return null;

            font.RequestCharactersInTexture(text, FontSize, FontStyle.Bold);
            var atlas = font.material.mainTexture as Texture2D;
            if (atlas == null)
                return null;

            int width = Padding * 2;
            int height = FontSize + Padding * 2;
            foreach (char ch in text)
            {
                if (!font.GetCharacterInfo(ch, out var ci, FontSize, FontStyle.Bold))
                    continue;
                width += ci.advance;
            }

            width = Mathf.Max(width, 48);
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = $"PortRate_{text}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0, 0, 0, 0);

            int cursorX = Padding;
            int baselineY = Padding + FontSize;
            var ink = new Color32(28, 32, 40, 255);
            foreach (char ch in text)
            {
                if (!font.GetCharacterInfo(ch, out var ci, FontSize, FontStyle.Bold))
                    continue;
                BlitGlyph(atlas, ci, pixels, width, height, cursorX, baselineY, ink);
                cursorX += ci.advance;
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private static void BlitGlyph(
            Texture2D atlas,
            CharacterInfo ci,
            Color32[] dest,
            int destW,
            int destH,
            int destX,
            int destY,
            Color32 ink)
        {
            int gw = ci.glyphWidth;
            int gh = ci.glyphHeight;
            if (gw <= 0 || gh <= 0)
                return;

            float u0 = ci.uvBottomLeft.x;
            float v0 = ci.uvBottomLeft.y;
            float u1 = ci.uvTopRight.x;
            float v1 = ci.uvTopRight.y;

            for (int py = 0; py < gh; py++)
            {
                float v = Mathf.Lerp(v0, v1, py / (float)Mathf.Max(gh - 1, 1));
                int atlasY = Mathf.Clamp((int)(v * atlas.height), 0, atlas.height - 1);
                int dy = destY + py + ci.minY;
                if (dy < 0 || dy >= destH)
                    continue;

                for (int px = 0; px < gw; px++)
                {
                    float u = Mathf.Lerp(u0, u1, px / (float)Mathf.Max(gw - 1, 1));
                    int atlasX = Mathf.Clamp((int)(u * atlas.width), 0, atlas.width - 1);
                    int dx = destX + px + ci.minX;
                    if (dx < 0 || dx >= destW)
                        continue;

                    Color src = atlas.GetPixel(atlasX, atlasY);
                    if (src.a < 0.08f)
                        continue;
                    dest[dy * destW + dx] = ink;
                }
            }
        }

        private static Texture2D BuildBitmapFallback(string text)
        {
            const int GlyphWidth = 5;
            const int GlyphHeight = 7;
            const int PixelScale = 7;
            const int StrokeExpand = 3;
            const int CharGap = 2;

            var glyphs = BitmapGlyphs();
            int charCount = text.Length;
            int width = charCount * GlyphWidth * PixelScale + (charCount - 1) * CharGap * PixelScale;
            int height = GlyphHeight * PixelScale;

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = $"PortRate_{text}_fallback",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[width * height];
            var ink = new Color32(28, 32, 40, 255);
            int cursorX = 0;
            foreach (char ch in text)
            {
                if (!glyphs.TryGetValue(ch, out var rows))
                    continue;
                DrawBitmapGlyph(pixels, width, height, cursorX, 0, rows, ink);
                cursorX += GlyphWidth * PixelScale + CharGap * PixelScale;
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private static Dictionary<char, string[]> BitmapGlyphs() => new()
        {
            ['0'] = Rows("01110", "10001", "10011", "10101", "11001", "10001", "01110"),
            ['1'] = Rows("00100", "01100", "00100", "00100", "00100", "00100", "01110"),
            ['2'] = Rows("01110", "10001", "00001", "00110", "01000", "10000", "11111"),
            ['3'] = Rows("01110", "10001", "00001", "00110", "00001", "10001", "01110"),
            [':'] = Rows("00000", "00100", "00000", "00000", "00000", "00100", "00000"),
        };

        private static void DrawBitmapGlyph(
            Color32[] pixels,
            int texW,
            int texH,
            int originX,
            int originY,
            string[] rows,
            Color32 ink)
        {
            const int GlyphWidth = 5;
            const int GlyphHeight = 7;
            const int PixelScale = 7;
            const int StrokeExpand = 3;

            for (int y = 0; y < GlyphHeight; y++)
            {
                string row = rows[y];
                for (int x = 0; x < GlyphWidth; x++)
                {
                    if (row[x] != '1')
                        continue;
                    int px = originX + x * PixelScale - StrokeExpand;
                    int py = originY + (GlyphHeight - 1 - y) * PixelScale - StrokeExpand;
                    int size = PixelScale + StrokeExpand * 2;
                    FillRect(pixels, texW, texH, px, py, size, size, ink);
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
