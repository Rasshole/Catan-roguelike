using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    internal static class ColonistHudIconGraphic
    {
        public static void ApplyCardIcon(Image target, Sprite sprite, float fill = 0.6f)
        {
            if (target == null)
                return;

            target.sprite = sprite;
            target.preserveAspect = true;
            target.color = Color.white;
            target.material = null;

            var rect = target.rectTransform;
            float margin = (1f - fill) * 0.5f;
            rect.anchorMin = new Vector2(margin, margin);
            rect.anchorMax = new Vector2(1f - margin, 1f - margin);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var outline = target.GetComponent<Outline>();
            if (outline == null)
                outline = target.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.05f, 0.06f, 0.08f, 0.95f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;
        }
    }
}
