using CatanRoguelike.Core.UI;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests
{
    /// <summary>
    /// Kenney-derived board albedos should read as the physical Catan palette (BoardResourcePalette).
    /// </summary>
    public sealed class BoardTerrainTextureColorTests
    {
        private const float Tolerance = 0.14f;

        [Test]
        public void TerrainAlbedoAverageMatchesCatanPalette()
        {
            AssertTerrain("Wood", BoardResourcePalette.Wood);
            AssertTerrain("Sheep", BoardResourcePalette.Sheep);
            AssertTerrain("Wheat", BoardResourcePalette.Wheat);
            AssertTerrain("Brick", BoardResourcePalette.Brick);
            AssertTerrain("Ore", BoardResourcePalette.Stone);
            AssertTerrain("Desert", BoardResourcePalette.Desert);
        }

        private static void AssertTerrain(string resourceName, BoardResourcePalette.Rgb expected)
        {
            var tex = Resources.Load<Texture2D>($"Art/Board/{resourceName}");
            Assert.IsNotNull(tex, $"Missing Resources/Art/Board/{resourceName}.png");

            var pixels = tex.GetPixels32();
            double r = 0, g = 0, b = 0;
            int n = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                if (p.a < 128)
                    continue;
                r += p.r / 255.0;
                g += p.g / 255.0;
                b += p.b / 255.0;
                n++;
            }

            Assert.Greater(n, 100, $"{resourceName}: too few opaque pixels");
            var avgR = (float)(r / n);
            var avgG = (float)(g / n);
            var avgB = (float)(b / n);
            Assert.Less(Mathf.Abs(avgR - expected.R), Tolerance, $"{resourceName} R avg={avgR:F2} expected {expected.R:F2}");
            Assert.Less(Mathf.Abs(avgG - expected.G), Tolerance, $"{resourceName} G avg={avgG:F2} expected {expected.G:F2}");
            Assert.Less(Mathf.Abs(avgB - expected.B), Tolerance, $"{resourceName} B avg={avgB:F2} expected {expected.B:F2}");
        }
    }
}
