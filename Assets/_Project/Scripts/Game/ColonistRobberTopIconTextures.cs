using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Top-down hooded robber silhouette for colonist board markers.</summary>
    public static class ColonistRobberTopIconTextures
    {
        private const int Size = 64;
        private static Texture2D _cached;

        public static Texture2D Get()
        {
            if (_cached != null)
                return _cached;

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "ColonistRobberTopIcon",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[Size * Size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0, 0, 0, 0);

            var fill = new Color32(34, 36, 42, 255);
            var outline = new Color32(228, 232, 238, 255);

            FillCircle(pixels, 32, 36, 20, outline);
            FillCircle(pixels, 32, 36, 17, fill);
            FillRect(pixels, 22, 14, 20, 18, fill);
            FillRect(pixels, 20, 12, 24, 4, outline);
            FillCircle(pixels, 32, 44, 9, outline);
            FillCircle(pixels, 32, 44, 7, fill);

            RotatePixels90Clockwise(pixels);
            RotatePixels90Clockwise(pixels);

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            _cached = tex;
            return tex;
        }

        private static void FillCircle(Color32[] px, int cx, int cy, int r, Color32 c)
        {
            int r2 = r * r;
            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || x >= Size || y < 0 || y >= Size)
                        continue;
                    int dx = x - cx;
                    int dy = y - cy;
                    if (dx * dx + dy * dy <= r2)
                        px[y * Size + x] = c;
                }
            }
        }

        private static void RotatePixels90Clockwise(Color32[] pixels)
        {
            var rotated = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int src = y * Size + x;
                    int dstX = Size - 1 - y;
                    int dstY = x;
                    rotated[dstY * Size + dstX] = pixels[src];
                }
            }

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = rotated[i];
        }

        private static void FillRect(Color32[] px, int x, int y, int w, int h, Color32 c)
        {
            for (int py = y; py < y + h; py++)
            {
                for (int pxX = x; pxX < x + w; pxX++)
                {
                    if (pxX < 0 || pxX >= Size || py < 0 || py >= Size)
                        continue;
                    px[py * Size + pxX] = c;
                }
            }
        }
    }
}
