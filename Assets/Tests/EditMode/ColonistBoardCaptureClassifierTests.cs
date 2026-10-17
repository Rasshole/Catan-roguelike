using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests
{
    public class ColonistBoardCaptureClassifierTests
    {
        [Test]
        public void ClassifyCapturePixel_DoesNotRecurse_AndMatchesSamples()
        {
            var greenHex = new Color(0.365f, 0.808f, 0.447f);
            Assert.AreEqual(
                ColonistBoardCaptureAssertions.CapturePixelKind.HexTile,
                ColonistBoardCaptureAssertions.ClassifyCapturePixel(greenHex));

            var sky = new Color(0.698f, 0.776f, 0.918f);
            var skyKind = ColonistBoardCaptureAssertions.ClassifyCapturePixel(sky);
            Assert.That(
                skyKind == ColonistBoardCaptureAssertions.CapturePixelKind.Sky
                || skyKind == ColonistBoardCaptureAssertions.CapturePixelKind.Ocean,
                $"Expected sky or ocean, got {skyKind}");

            var cream = new Color(0.92f, 0.86f, 0.72f);
            Assert.AreEqual(
                ColonistBoardCaptureAssertions.CapturePixelKind.TokenChip,
                ColonistBoardCaptureAssertions.ClassifyCapturePixel(cream));
        }
    }
}
