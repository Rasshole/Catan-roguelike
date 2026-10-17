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

            // Neutralised sprite: tinting the blue HudPanel fill parchment produced slate panels.
            var sprite = NeutralPanelSprite();
            if (sprite == null)
                return;

            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = new Color(0.94f, 0.88f, 0.74f, 0.96f);
        }

        /// <summary>Warm wood/parchment tint for bottom hand and action bars.</summary>
        public static void ApplyWoodBarImage(Image image)
        {
            if (image == null)
                return;

            // HudPanel.png has a blue interior, so tinting it warm produced dark teal.
            // Use a neutralised copy (white fill, darker frame) so the tint reads as wood/parchment.
            var sprite = NeutralPanelSprite();
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }

            image.color = new Color(0.84f, 0.72f, 0.54f, 0.94f);
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
            image.color = new Color(0.42f, 0.76f, 0.48f, 1f);
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
            image.color = new Color(0.55f, 0.72f, 0.82f, 1f);
        }

        public static Sprite CardFrameSprite() => _cardFrameSprite ??= LoadSprite("Art/Boardgame/Cards/CardBack_blue");

        private static Sprite _neutralPanelSprite;

        /// <summary>HudPanel with its blue fill mapped to white and its white/grey frame darkened.</summary>
        private static Sprite NeutralPanelSprite()
        {
            if (_neutralPanelSprite != null)
                return _neutralPanelSprite;

            var src = Resources.Load<Texture2D>("Art/UI/HudPanel");
            if (src == null || !src.isReadable)
                return null;

            var pixels = src.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                bool blueFill = c.b - c.r > 0.15f;
                float v = blueFill ? 1f : Mathf.Lerp(0.42f, 0.62f, c.grayscale);
                pixels[i] = new Color(v, v, v, c.a);
            }

            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false)
            {
                name = "HudPanelNeutral",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            _neutralPanelSprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(8f, 8f, 8f, 8f));
            return _neutralPanelSprite;
        }

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
