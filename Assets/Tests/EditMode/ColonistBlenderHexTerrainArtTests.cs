using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests
{
    public class ColonistBlenderHexTerrainArtTests
    {
        [Test]
        public void BlenderTerrainTextures_ExistForLandSlots()
        {
            foreach (var slot in new[]
                     {
                         BoardTerrainSlot.Wood,
                         BoardTerrainSlot.Brick,
                         BoardTerrainSlot.Wheat,
                         BoardTerrainSlot.Sheep,
                         BoardTerrainSlot.Ore,
                         BoardTerrainSlot.Desert
                     })
            {
                var tex = Resources.Load<Texture2D>($"{ColonistBlenderHexTerrainArt.TextureFolder}/{slot}");
                Assert.IsNotNull(tex, $"Missing Blender terrain texture for {slot}.");
                Assert.GreaterOrEqual(tex.width, 1024, $"{slot} texture should be at least 1024px.");
            }
        }

        [Test]
        public void IsRotated180_VariesDeterministicallyAcrossTheBoard()
        {
            int rotated = 0, total = 0;
            for (int q = -2; q <= 2; q++)
            for (int r = -2; r <= 2; r++)
            {
                if (Mathf.Abs(q + r) > 2)
                    continue;
                var c = new CatanRoguelike.Core.Hex.HexCoord(q, r);
                Assert.AreEqual(ColonistBlenderHexTerrainArt.IsRotated180(c), ColonistBlenderHexTerrainArt.IsRotated180(c));
                total++;
                if (ColonistBlenderHexTerrainArt.IsRotated180(c))
                    rotated++;
            }

            Assert.That(rotated, Is.InRange(total / 4, total - total / 4),
                $"{rotated}/{total} tiles turned 180: repeated terrains need a real mix.");
        }

        [Test]
        public void ApplyRotation180_FlipsBothTextureAxes()
        {
            var mat = new Material(Shader.Find("Unlit/Texture"));
            ColonistBlenderHexTerrainArt.ApplyRotation180(mat);
            Assert.AreEqual(new Vector2(-1f, -1f), mat.mainTextureScale);
            Assert.AreEqual(new Vector2(1f, 1f), mat.mainTextureOffset);
            Object.DestroyImmediate(mat);
        }

        [Test]
        public void FishGroundWater_IsTheReadableAiPainting_WithFishAtEachSchool()
        {
            var water = Resources.Load<Texture2D>("Art/Board/FishGroundWater");
            Assert.IsNotNull(water);
            Assert.IsTrue(water.isReadable);
            Assert.GreaterOrEqual(water.width, 1024);
            foreach (var school in ColonistCoastalFishShoalVisual.FishSchoolsUv)
            {
                int fishy = 0;
                for (int i = -10; i <= 10; i++)
                for (int j = -5; j <= 5; j++)
                {
                    var c = water.GetPixelBilinear(school.x + i * 0.006f, school.y + j * 0.008f);
                    // Painted orange/yellow fish against turquoise water (open water scores 0).
                    if (c.r > c.b + 0.08f)
                        fishy++;
                }

                Assert.Greater(fishy, 6, $"No painted fish found around school {school}.");
            }
        }

        [Test]
        public void FishShoalPiece_SitsBelowTheHexTop_SoTokenRimsAreNotHidden()
        {
            Assert.Less(CoastalFishShoalMesh.TopLocalY, 1f);
        }

        [Test]
        public void Disabled_FallsBackToLegacyBoardTextures()
        {
            bool prev = ColonistBlenderHexTerrainArt.Enabled;
            ColonistBlenderHexTerrainArt.Enabled = false;
            ColonistBoardTextureLoader.ClearCache();
            try
            {
                var legacy = ColonistBoardTextureLoader.LoadBoardTexture(BoardTerrainSlot.Wood);
                Assert.IsNotNull(legacy);
                Assert.IsNull(ColonistBoardTextureLoader.TryLoadBlenderTerrainTexture(BoardTerrainSlot.Wood));
            }
            finally
            {
                ColonistBlenderHexTerrainArt.Enabled = prev;
                ColonistBoardTextureLoader.ClearCache();
            }
        }
    }
}
