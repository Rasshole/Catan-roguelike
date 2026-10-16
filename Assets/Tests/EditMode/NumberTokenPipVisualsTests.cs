using CatanRoguelike.Core.Yield;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests
{
    public class NumberTokenPipVisualsTests
    {
        [Test]
        public void GetExpectedPipCount_MatchesClassicWeights()
        {
            Assert.AreEqual(1, NumberTokenPipVisuals.GetExpectedPipCount(2));
            Assert.AreEqual(1, NumberTokenPipVisuals.GetExpectedPipCount(12));
            Assert.AreEqual(2, NumberTokenPipVisuals.GetExpectedPipCount(3));
            Assert.AreEqual(2, NumberTokenPipVisuals.GetExpectedPipCount(11));
            Assert.AreEqual(3, NumberTokenPipVisuals.GetExpectedPipCount(4));
            Assert.AreEqual(3, NumberTokenPipVisuals.GetExpectedPipCount(10));
            Assert.AreEqual(4, NumberTokenPipVisuals.GetExpectedPipCount(5));
            Assert.AreEqual(4, NumberTokenPipVisuals.GetExpectedPipCount(9));
            Assert.AreEqual(5, NumberTokenPipVisuals.GetExpectedPipCount(6));
            Assert.AreEqual(5, NumberTokenPipVisuals.GetExpectedPipCount(8));
        }

        [Test]
        public void PipOffsets_LengthMatchesPipCount()
        {
            for (int token = 2; token <= 12; token++)
            {
                int count = NumberTokenPipVisuals.GetExpectedPipCount(token);
                Assert.AreEqual(count, NumberTokenPipLayout.GetPipOffsetsX(count).Length);
            }
        }

        [Test]
        public void ComputeRowLayout_RowWidthStaysInsideFaceDiameter()
        {
            const float faceDiameter = 0.62f;
            const float labelScale = 0.08f;
            float maxRow = faceDiameter * NumberTokenPipVisuals.MaxRowWidthFaceFraction;

            for (int pipCount = 1; pipCount <= 5; pipCount++)
            {
                NumberTokenPipVisuals.ComputeRowLayout(
                    pipCount, faceDiameter, labelScale, out float pipScale, out float step);
                var offsets = NumberTokenPipLayout.GetPipOffsetsX(pipCount, step);
                float minLocalX = float.MaxValue;
                float maxLocalX = float.MinValue;
                for (int i = 0; i < offsets.Length; i++)
                {
                    float localX = offsets[i] * NumberTokenPipVisuals.LabelOffsetScale;
                    minLocalX = Mathf.Min(minLocalX, localX);
                    maxLocalX = Mathf.Max(maxLocalX, localX);
                }

                float rowWidthWorld = (maxLocalX - minLocalX + pipScale) * labelScale;
                Assert.LessOrEqual(rowWidthWorld, maxRow + 0.001f, $"pipCount={pipCount}");
            }
        }
    }
}
