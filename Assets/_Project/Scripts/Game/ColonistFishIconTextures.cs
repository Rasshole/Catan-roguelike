using UnityEngine;

namespace CatanRoguelike.Game
{
    internal static class ColonistFishIconTextures
    {
        private const int Size = 96;
        private static Texture2D _boldFish;
        private static Texture2D _darkHalo;

        /// <summary>High-contrast orange fish with dark outline for top-down readability.</summary>
        public static Texture2D BoldOrangeFish()
        {
            if (_boldFish != null)
                return _boldFish;

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "BoldFishIcon",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var clear = new Color(0f, 0f, 0f, 0f);
            var outline = new Color(0.12f, 0.10f, 0.08f, 1f);
            var body = new Color(0.98f, 0.62f, 0.22f, 1f);
            var belly = new Color(0.99f, 0.78f, 0.42f, 1f);
            var pixels = new Color32[Size * Size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;

            float cx = Size * 0.52f;
            float cy = Size * 0.5f;
            float bodyRx = Size * 0.34f;
            float bodyRy = Size * 0.16f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = (x - cx) / bodyRx;
                    float dy = (y - cy) / bodyRy;
                    float d = dx * dx + dy * dy;
                    if (d <= 1.15f)
                        pixels[y * Size + x] = y > cy ? belly : body;
                    if (d > 0.82f && d <= 1.22f)
                        pixels[y * Size + x] = outline;
                }
            }

            // Tail wedge
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size * 0.35f; x++)
                {
                    float t = x / (Size * 0.35f);
                    float halfH = Mathf.Lerp(Size * 0.02f, Size * 0.18f, t);
                    if (Mathf.Abs(y - cy) <= halfH)
                    {
                        int idx = y * Size + x;
                        pixels[idx] = t > 0.85f ? outline : body;
                    }
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            _boldFish = tex;
            return _boldFish;
        }

        /// <summary>Dark ring behind fish icons for contrast on the light-blue chevron.</summary>
        public static Texture2D DarkIconHalo()
        {
            if (_darkHalo != null)
                return _darkHalo;

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "FishIconHalo",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float cx = Size * 0.5f;
            float cy = Size * 0.5f;
            float outer = Size * 0.46f;
            float inner = Size * 0.30f;
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    float a = d <= outer && d >= inner ? 0.88f : (d < inner ? 0.35f : 0f);
                    pixels[y * Size + x] = new Color(0.08f, 0.14f, 0.22f, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            _darkHalo = tex;
            return _darkHalo;
        }
    }
}
