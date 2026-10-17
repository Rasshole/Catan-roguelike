using System;
using CatanRoguelike.Game;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class ColonistHexTileDecorTemplateTests
    {
        private const float TestHexScale = 1.2f;

        [Test]
        public void Templates_NoOverlappingFootprints_InsideCap_OutsideKeepOut()
        {
            float minRadiusHex = (HexTileView.TokenWorldFaceDiameter * 0.5f + TestHexScale * 0.08f) / TestHexScale;
            Validate(ColonistHexTileDecorTemplates.Wheat, minRadiusHex, "Wheat");
            Validate(ColonistHexTileDecorTemplates.Brick, minRadiusHex, "Brick");
            Validate(ColonistHexTileDecorTemplates.Stone, minRadiusHex, "Stone");
            Validate(ColonistHexTileDecorTemplates.Sheep, minRadiusHex, "Sheep");
            Validate(ColonistHexTileDecorTemplates.Desert, minRadiusHex, "Desert");
            Validate(ColonistHexTileDecorTemplates.DesertFishPond, minRadiusHex, "DesertFish");
        }

        [Test]
        public void Templates_MeetTerrainSlotCounts()
        {
            Assert.AreEqual(9, ColonistHexTileDecorTemplates.Wheat.Count);
            Assert.AreEqual(10, ColonistHexTileDecorTemplates.Brick.Count);
            Assert.AreEqual(10, ColonistHexTileDecorTemplates.Stone.Count);
            Assert.AreEqual(12, ColonistHexTileDecorTemplates.Sheep.Count);
            Assert.AreEqual(12, ColonistHexTileDecorTemplates.Desert.Count);
        }

        private static void Validate(
            System.Collections.Generic.IReadOnlyList<ColonistHexTileDecorTemplates.Slot> template,
            float minRadiusHex,
            string label)
        {
            try
            {
                ColonistHexTileDecorTemplates.ValidateTemplate(template, minRadiusHex, label);
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message);
            }
        }
    }
}
