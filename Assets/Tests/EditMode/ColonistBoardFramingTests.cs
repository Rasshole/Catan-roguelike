using CatanRoguelike.Game;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class ColonistBoardFramingTests
    {
        [Test]
        public void ComputeOrthographicSize_LargerBoardNeedsLargerOrtho()
        {
            const int w = 1920;
            const int h = 1080;
            float aspect = w / (float)h;
            float small = ColonistBoardFraming.ComputeOrthographicSize(4f, w, h, aspect);
            float large = ColonistBoardFraming.ComputeOrthographicSize(6f, w, h, aspect);
            Assert.Greater(large, small);
        }
    }
}
