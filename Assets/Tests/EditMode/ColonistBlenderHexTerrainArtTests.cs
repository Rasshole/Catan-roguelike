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
