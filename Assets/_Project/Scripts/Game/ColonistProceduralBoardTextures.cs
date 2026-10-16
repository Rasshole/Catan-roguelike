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

            var fallback = ColonistBoardTextureLoader.GetFallbackColorForSlot(slot);
            if (slot == BoardTerrainSlot.Ore)
                fallback = new Color(0.49f, 0.50f, 0.53f);
            var baseColor = BoostBaseColor(fallback, slot);
            var pixels = new Color32[Size * Size];
            var rng = new System.Random((int)slot * 7919 + 42);
            float strokeAngle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float strokePhase = (float)rng.NextDouble() * 12f;
            float noisePhaseU = (float)rng.NextDouble() * 64f;
            float noisePhaseV = (float)rng.NextDouble() * 64f;
            float cosA = Mathf.Cos(strokeAngle);
            float sinA = Mathf.Sin(strokeAngle);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = x / (float)Size;
                    float v = y / (float)Size;
                    float ru = u - 0.5f;
                    float rv = v - 0.5f;
                    float su = ru * cosA - rv * sinA + 0.5f;
                    float sv = ru * sinA + rv * cosA + 0.5f;

                    float n = Mathf.PerlinNoise(su * 5.5f + noisePhaseU, sv * 5.5f + noisePhaseV);
                    float streak = Mathf.Sin((su + sv) * 14f + strokePhase) * 0.5f + 0.5f;
                    float micro = ((float)rng.NextDouble() - 0.5f) * 0.04f;

                    float valueMul = 1f + (n - 0.5f) * 0.14f + (streak - 0.5f) * 0.08f + micro;
                    valueMul = Mathf.Clamp(valueMul, 0.90f, 1.10f);

                    var c = baseColor;
                    c.r = Mathf.Clamp01(c.r * valueMul);
                    c.g = Mathf.Clamp01(c.g * valueMul);
                    c.b = Mathf.Clamp01(c.b * valueMul);
                    pixels[y * Size + x] = c;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static Color BoostBaseColor(Color c, BoardTerrainSlot slot)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            float satBoost = slot switch
            {
                BoardTerrainSlot.Sheep => 1.20f,
                BoardTerrainSlot.Ore => 1.05f,
                BoardTerrainSlot.Wood => 1.08f,
                BoardTerrainSlot.Brick => 1.06f,
                BoardTerrainSlot.Wheat => 1.04f,
                _ => 1f
            };
            s = Mathf.Clamp01(s * satBoost);
            v = Mathf.Clamp(v, slot == BoardTerrainSlot.Ore ? 0.42f : 0.52f, slot == BoardTerrainSlot.Ore ? 0.62f : 0.92f);
            return Color.HSVToRGB(h, s, v);
        }

        public static Texture2D CreateRoundPondMask()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Procedural_RoundPond",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            float center = size * 0.5f - 0.5f;
            float radius = center - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    byte alpha = dist <= radius ? (byte)255 : (byte)0;
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        public static Texture2D CreateRippleMask()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Procedural_LakeRipple",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            float center = size * 0.5f;
            float radius = center - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                    byte alpha = dist <= 1f ? (byte)(255 * (1f - dist * 0.65f)) : (byte)0;
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
