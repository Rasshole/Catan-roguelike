using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Procedural fish chevron fill (waves + cardboard read from top-down).</summary>
    internal static class ColonistFishChevronTextures
    {
        private const int Size = 128;
        private static Texture2D _fill;

        public static Texture2D ChevronFill()
        {
            if (_fill != null)
                return _fill;

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "FishChevronFill",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            var baseBlue = new Color(0.58f, 0.78f, 0.91f);
            var waveBlue = new Color(0.42f, 0.62f, 0.78f);
            var highlight = new Color(0.72f, 0.86f, 0.96f);
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = x / (float)Size;
                    float v = y / (float)Size;
                    float wave = Mathf.Sin((u * 9f + v * 4f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                    float ripple = Mathf.PerlinNoise(u * 6f + 2f, v * 6f + 5f);
                    var c = Color.Lerp(baseBlue, waveBlue, wave * 0.55f);
                    c = Color.Lerp(c, highlight, ripple * 0.18f);
                    pixels[y * Size + x] = c;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            _fill = tex;
            return _fill;
        }
    }
}
