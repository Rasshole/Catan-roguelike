using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.UI;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class HudToastBufferTests
    {
        [Test]
        public void Push_TryConsume_ClearsMessage()
        {
            var state = new GameState(MapPresets.CreateBoard());
            HudToastBuffer.Push(state, "Hej");
            Assert.IsTrue(HudToastBuffer.TryConsume(state, out var msg));
            Assert.AreEqual("Hej", msg);
            Assert.IsFalse(HudToastBuffer.TryConsume(state, out _));
        }

        [Test]
        public void Push_TrimsWhitespace()
        {
            var state = new GameState(MapPresets.CreateBoard());
            HudToastBuffer.Push(state, "  toast  ");
            Assert.IsTrue(HudToastBuffer.TryConsume(state, out var msg));
            Assert.AreEqual("toast", msg);
        }
    }
}
