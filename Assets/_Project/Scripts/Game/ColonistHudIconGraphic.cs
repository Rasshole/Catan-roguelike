using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    internal static class ColonistHudIconGraphic
    {
        public const float IconOutlineAlpha = 0.28f;
        public const float IconOutlineDistance = 0.5f;

        public static void ApplyCardIcon(Image target, Sprite sprite, float fill = 0.6f, float centerY = 0.5f)
        {
            if (target == null)
                return;

            target.sprite = sprite;
            target.preserveAspect = true;
            target.color = Color.white;
            target.material = null;

            var rect = target.rectTransform;
            float margin = (1f - fill) * 0.5f;
            float shiftY = centerY - 0.5f;
            rect.anchorMin = new Vector2(margin, margin + shiftY);
            rect.anchorMax = new Vector2(1f - margin, 1f - margin + shiftY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var outline = target.GetComponent<Outline>();
            if (outline == null)
                outline = target.gameObject.AddComponent<Outline>();
            // v133: barely-there edge (the painted icons carry their own faint ink ring now).
            outline.effectColor = new Color(0.16f, 0.10f, 0.05f, IconOutlineAlpha);
            outline.effectDistance = new Vector2(IconOutlineDistance, -IconOutlineDistance);
            outline.useGraphicAlpha = true;
        }
    }
}
