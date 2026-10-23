using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    internal static class ColonistKenneyDiceUi
    {
        public static void ApplyFace(Image dieBackground, Image[] pipImages, int value)
        {
            var sprite = ColonistKenneyDiceSprites.TryGetFace(value);
            if (sprite == null || dieBackground == null)
            {
                ColonistDiceFaceUi.SetValue(pipImages, value);
                return;
            }

            dieBackground.sprite = sprite;
            // v132: ivory bone dice to sit on the parchment bar (the Kenney faces are pure white).
            dieBackground.color = ColonistPresentation.IsActive ? new Color(1f, 0.95f, 0.84f, 1f) : Color.white;
            if (pipImages != null)
            {
                foreach (var pip in pipImages)
                {
                    if (pip != null)
                        pip.enabled = false;
                }
            }
        }
    }
}
