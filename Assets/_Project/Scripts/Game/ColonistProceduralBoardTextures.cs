using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Painted-style terrain albedo when no committed art texture exists.</summary>
    public static class ColonistProceduralBoardTextures
    {
        private const int Size = 256;

        public static Texture2D Create(BoardTerrainSlot slot)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                name = $"Procedural_{slot}"
            };

            var baseColor = ColonistBoardTextureLoader.GetFallbackColorForSlot(slot);
            var pixels = new Color32[Size * Size];
            var rng = new System.Random((int)slot * 7919 + 42);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = x / (float)Size;
                    float v = y / (float)Size;
                    float n = Mathf.PerlinNoise(u * 6f + slot.GetHashCode() * 0.01f, v * 6f) * 0.12f;
                    float streak = Mathf.Sin((u + v) * 18f) * 0.04f;
                    float brush = (rng.NextDouble() - 0.5f) * 0.06f;
                    var c = baseColor;
                    c.r = Mathf.Clamp01(c.r + n + streak + brush);
                    c.g = Mathf.Clamp01(c.g + n * 0.8f + brush);
                    c.b = Mathf.Clamp01(c.b + n * 0.6f - streak * 0.5f);
                    pixels[y * Size + x] = c;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
