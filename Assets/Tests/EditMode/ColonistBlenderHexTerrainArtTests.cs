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
