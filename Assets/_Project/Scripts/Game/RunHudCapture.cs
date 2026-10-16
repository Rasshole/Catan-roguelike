using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    public sealed partial class RunHudController
    {
        public int CountHudGraphics()
        {
            if (_canvas == null)
                return 0;
            var graphics = _canvas.GetComponentsInChildren<Graphic>(true);
            return graphics != null ? graphics.Length : 0;
        }

        public bool IsHudCaptureReady(int minGraphics = 8) =>
            _canvas != null && CountHudGraphics() >= minGraphics;

        public Canvas HudCanvas => _canvas;
    }
}
