using CatanRoguelike.Core.Yield;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class NumberTokenVisualStyleTests
    {
        [Test]
        public void GetChipFace_RedNumbers_UseWarmerAccent()
        {
            Assert.AreEqual(NumberTokenVisualStyle.ChipFaceRedAccent, NumberTokenVisualStyle.GetChipFace(6));
            Assert.AreEqual(NumberTokenVisualStyle.ChipFaceRedAccent, NumberTokenVisualStyle.GetChipFace(8));
        }

        [Test]
        public void GetChipFace_OtherNumbers_UseStandardCream()
        {
            Assert.AreEqual(NumberTokenVisualStyle.ChipFaceStandard, NumberTokenVisualStyle.GetChipFace(5));
            Assert.AreEqual(NumberTokenVisualStyle.ChipFaceStandard, NumberTokenVisualStyle.GetChipFace(12));
        }

        [Test]
        public void GetChipRim_RedNumbers_UseReddishRim()
        {
            Assert.AreEqual(NumberTokenVisualStyle.ChipRimRedAccent, NumberTokenVisualStyle.GetChipRim(6));
            Assert.AreEqual(NumberTokenVisualStyle.ChipRimRedAccent, NumberTokenVisualStyle.GetChipRim(8));
        }

        [Test]
        public void GetLabel_RedNumbers_UseRedTint()
        {
            Assert.AreEqual(NumberTokenVisualStyle.LabelRed, NumberTokenVisualStyle.GetLabel(6));
            Assert.AreEqual(NumberTokenVisualStyle.LabelRed, NumberTokenVisualStyle.GetLabel(8));
        }

        [Test]
        public void GetLabelCharacterSize_TwoDigitTokens_AreSmaller()
        {
            Assert.AreEqual(0.5f, NumberTokenVisualStyle.GetLabelCharacterSize(9));
            Assert.AreEqual(0.42f, NumberTokenVisualStyle.GetLabelCharacterSize(10));
            Assert.AreEqual(0.42f, NumberTokenVisualStyle.GetLabelCharacterSize(12));
        }

        [Test]
        public void PaletteConstants_MatchExpectedRgbValues()
        {
            Assert.AreEqual(new NumberTokenVisualStyle.Rgb(0.92f, 0.86f, 0.72f), NumberTokenVisualStyle.ChipFaceStandard);
            Assert.AreEqual(new NumberTokenVisualStyle.Rgb(0.94f, 0.84f, 0.72f), NumberTokenVisualStyle.ChipFaceRedAccent);
            Assert.AreEqual(new NumberTokenVisualStyle.Rgb(0.68f, 0.58f, 0.40f), NumberTokenVisualStyle.ChipRimStandard);
            Assert.AreEqual(new NumberTokenVisualStyle.Rgb(0.72f, 0.22f, 0.14f), NumberTokenVisualStyle.ChipRimRedAccent);
            Assert.AreEqual(new NumberTokenVisualStyle.Rgb(0.08f, 0.08f, 0.08f), NumberTokenVisualStyle.LabelStandard);
            Assert.AreEqual(new NumberTokenVisualStyle.Rgb(0.82f, 0.12f, 0.10f), NumberTokenVisualStyle.LabelRed);
        }
    }
}
