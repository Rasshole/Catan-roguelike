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
            Assert.AreEqual(0.6f, NumberTokenVisualStyle.GetLabelCharacterSize(9), 1e-4f);
            Assert.AreEqual(0.504f, NumberTokenVisualStyle.GetLabelCharacterSize(10), 1e-4f);
            Assert.AreEqual(0.504f, NumberTokenVisualStyle.GetLabelCharacterSize(12), 1e-4f);
        }

        [Test]
        public void PaletteConstants_MatchExpectedRgbValues()
        {
            Assert.AreEqual(new NumberTokenVisualStyle.Rgb(0.92f, 0.86f, 0.72f), NumberTokenVisualStyle.ChipFaceStandard);
            Assert.AreEqual(new NumberTokenVisualStyle.Rgb(0.94f, 0.84f, 0.72f), NumberTokenVisualStyle.ChipFaceRedAccent);
            Assert.AreEqual(new NumberTokenVisualStyle.Rgb(0.48f, 0.38f, 0.24f), NumberTokenVisualStyle.ChipRimStandard);
            Assert.AreEqual(new NumberTokenVisualStyle.Rgb(0.78f, 0.18f, 0.12f), NumberTokenVisualStyle.ChipRimRedAccent);
            Assert.That(NumberTokenVisualStyle.ChipRimStandard.ToString(), Does.Contain("0.480"));
            Assert.AreEqual(new NumberTokenVisualStyle.Rgb(0.08f, 0.08f, 0.08f), NumberTokenVisualStyle.LabelStandard);
            Assert.AreEqual(new NumberTokenVisualStyle.Rgb(0.82f, 0.12f, 0.10f), NumberTokenVisualStyle.LabelRed);
        }
    }
}
