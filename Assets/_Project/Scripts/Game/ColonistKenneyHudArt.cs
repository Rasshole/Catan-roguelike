using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>Kenney Boardgame Pack sprites for colonist HUD chrome.</summary>
    internal static class ColonistKenneyHudArt
    {
        private static Sprite _panelSprite;
        private static Sprite _buttonSprite;
        private static Sprite _cardFrameSprite;

        public static void ApplyPanelImage(Image image)
        {
            if (image == null)
                return;

            var sprite = PanelSprite();
            if (sprite == null)
                return;

            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = new Color(0.22f, 0.48f, 0.62f, 0.94f);
        }

        public static void ApplyPrimaryButtonImage(Image image)
        {
            if (image == null)
                return;

            var sprite = ButtonSprite();
            if (sprite == null)
                return;

            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = new Color(0.28f, 0.72f, 0.46f, 1f);
        }

        public static void ApplySecondaryButtonImage(Image image)
        {
            if (image == null)
                return;

            var sprite = ButtonSprite();
            if (sprite == null)
                return;

            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = new Color(0.34f, 0.52f, 0.68f, 1f);
        }

        public static Sprite CardFrameSprite() => _cardFrameSprite ??= LoadSprite("Art/Boardgame/Cards/CardBack_blue");

        private static Sprite PanelSprite() => _panelSprite ??= LoadSprite("Art/UI/HudPanel");

        private static Sprite ButtonSprite() => _buttonSprite ??= LoadSprite("Art/UI/HudButton");

        private static Sprite LoadSprite(string resourcesPath)
        {
            var tex = Resources.Load<Texture2D>(resourcesPath);
            if (tex == null)
                return null;

            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
