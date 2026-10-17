using CatanRoguelike.Game;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class ColonistUiScaleTests
    {
        [Test]
        public void PlayAreaHeight_ScalesDownAt640x480()
        {
            var small = ColonistPlayAreaLayout.Compute(640, 480);
            var full = ColonistPlayAreaLayout.Compute(1920, 1080);
            Assert.Less(small.PlayHeight, full.PlayHeight);
            Assert.Greater(small.PlayHeight, 200f, "Scaled play height should remain usable at 640×480.");
        }
    }
}
