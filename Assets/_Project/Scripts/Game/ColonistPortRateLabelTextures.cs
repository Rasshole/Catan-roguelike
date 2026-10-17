using System.Collections.Generic;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>2:1 / 3:1 port rate labels using Roboto Slab Bold (same family as number tokens).</summary>
    public static class ColonistPortRateLabelTextures
    {
        private const int FontSize = 52;
        private const int Padding = 10;
        private const int PaddingY = 4; // keep the quad inside the badge disc bounds

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
            var gpuAtlas = font.material.mainTexture as Texture2D;
            if (gpuAtlas == null)
                return null;
            // The dynamic font atlas is GPU-only (GetPixel threw "Font Texture is not readable" and
            // broke every board build); copy it through a RenderTexture first.
            var atlas = ReadableCopy(gpuAtlas);
            if (atlas == null)
                return null;

            // Size to the real glyph box: the baseline used to sit at the top edge (Padding + FontSize),
            // so only the descender-side sliver of each glyph landed inside the texture.
            int width = Padding * 2;
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (char ch in text)
            {
                if (!font.GetCharacterInfo(ch, out var ci, FontSize, FontStyle.Bold))
                    continue;
                width += ci.advance;
                minY = Mathf.Min(minY, ci.minY);
                maxY = Mathf.Max(maxY, ci.maxY);
            }

            if (minY > maxY)
            {
                DestroyTexture(atlas);
                return null;
            }

            int height = maxY - minY + PaddingY * 2;
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
            int baselineY = PaddingY - minY;
            var ink = new Color32(28, 32, 40, 255);
            foreach (char ch in text)
            {
                if (!font.GetCharacterInfo(ch, out var ci, FontSize, FontStyle.Bold))
                    continue;
                BlitGlyph(atlas, ci, pixels, width, height, cursorX, baselineY, ink);
                cursorX += ci.advance;
            }

            DestroyTexture(atlas);
            bool anyInk = false;
            for (int i = 0; i < pixels.Length && !anyInk; i++)
                anyInk = pixels[i].a > 0;
            if (!anyInk)
            {
                DestroyTexture(tex); // e.g. null graphics device in batch EditMode: use bitmap glyphs
                return null;
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private static Texture2D ReadableCopy(Texture2D source)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                return null;
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
                copy.Apply(false, false);
                return copy;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static void DestroyTexture(Texture2D tex)
        {
            if (tex == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(tex);
            else
                Object.DestroyImmediate(tex);
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
            int gw = ci.maxX - ci.minX;
            int gh = ci.maxY - ci.minY;
            if (gw <= 0 || gh <= 0)
                return;

            // Interpolate across all four UV corners: dynamic atlases store some glyphs rotated.
            Vector2 bl = ci.uvBottomLeft, br = ci.uvBottomRight, tl = ci.uvTopLeft;
            for (int py = 0; py < gh; py++)
            {
                int dy = destY + ci.minY + py;
                if (dy < 0 || dy >= destH)
                    continue;
                float t = (py + 0.5f) / gh;
                for (int px = 0; px < gw; px++)
                {
                    int dx = destX + ci.minX + px;
                    if (dx < 0 || dx >= destW)
                        continue;
                    float u = (px + 0.5f) / gw;
                    Vector2 uv = bl + (br - bl) * u + (tl - bl) * t;
                    float a = atlas.GetPixelBilinear(uv.x, uv.y).a;
                    if (a < 0.35f)
                        continue;
                    var c = ink;
                    c.a = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.InverseLerp(0.35f, 0.75f, a) * 255f), 0, 255);
                    if (c.a > dest[dy * destW + dx].a)
                        dest[dy * destW + dx] = c;
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
