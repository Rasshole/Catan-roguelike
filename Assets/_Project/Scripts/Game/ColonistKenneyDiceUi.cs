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
            dieBackground.color = Color.white;
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
