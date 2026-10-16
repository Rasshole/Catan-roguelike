using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    public sealed partial class RunHudController
    {
        public bool IsHudCaptureReady(int minGraphics = 8)
        {
            if (_canvas == null || !_canvas.isActiveAndEnabled)
                return false;

            var graphics = _canvas.GetComponentsInChildren<Graphic>(true);
            return graphics != null && graphics.Length >= minGraphics;
        }

        public Canvas HudCanvas => _canvas;
    }
}
