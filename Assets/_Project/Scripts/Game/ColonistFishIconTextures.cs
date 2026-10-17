using UnityEngine;

namespace CatanRoguelike.Game
{
    internal static class ColonistFishIconTextures
    {
        private const int Size = 96;
        private static Texture2D _boldFish;

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
    }
}
