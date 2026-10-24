using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>Kenney Boardgame Pack sprites for colonist HUD chrome.</summary>
    internal static class ColonistKenneyHudArt
    {
        private static Sprite _cardFrameSprite;

        // v132: every Apply* below draws the painted UI kit (ColonistUiKit) first and only falls back to the
        // old Kenney-derived sprites when the kit textures are missing.

        /// <summary>Default HUD panel: parchment inside a thin painted-wood rim.</summary>
        public static void ApplyPanelImage(Image image)
        {
            if (image == null)
                return;
            if (ColonistUiKit.Apply(image, UiKitPiece.FrameThin))
                return;

            var sprite = PanelSpriteSliced();
            if (sprite == null)
                return;

            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = new Color(0.96f, 0.90f, 0.76f, 0.98f);
        }

        /// <summary>Large framed panel (rail, dialogs, menus): parchment in the ornate gold-cornered frame.</summary>
        public static void ApplyOrnatePanelImage(Image image)
        {
            if (!ColonistUiKit.Apply(image, UiKitPiece.FrameOrnate, 0.8f))
                ApplyPanelImage(image);
        }

        /// <summary>Bottom hand and action bars: parchment in a medium painted-wood frame.</summary>
        public static void ApplyWoodBarImage(Image image)
        {
            if (image == null)
                return;
            if (ColonistUiKit.Apply(image, UiKitPiece.FrameMedium))
                return;

            var sprite = PanelSpriteSliced();
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }

            image.color = new Color(0.92f, 0.80f, 0.58f, 0.98f);
        }

        public static void ApplyPrimaryButtonImage(Image image)
        {
            if (image == null)
                return;
            if (ColonistUiKit.Apply(image, UiKitPiece.ButtonGreen))
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
            if (ColonistUiKit.Apply(image, UiKitPiece.ButtonParchment))
                return;

            var sprite = ButtonSprite();
            if (sprite == null)
                return;

            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = new Color(0.55f, 0.72f, 0.82f, 1f);
        }

        /// <summary>Green lacquer (primary) or parchment (secondary) painted button.</summary>
        public static void ApplyColonistWarmButton(Image image, bool primaryGreen = false)
        {
            if (image == null)
                return;
            if (ColonistUiKit.Apply(image, primaryGreen ? UiKitPiece.ButtonGreen : UiKitPiece.ButtonParchment))
                return;

            var sprite = ButtonSpriteSliced();
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }

            image.color = primaryGreen
                ? new Color(0.52f, 0.74f, 0.40f, 1f)
                : new Color(0.82f, 0.70f, 0.52f, 1f);
        }

        public static void ApplyColonistEndTurnButton(Image image) => ApplyColonistWarmButton(image, primaryGreen: true);

        /// <summary>"Your turn" plaque: dark wood with a gold inlay (cream text).</summary>
        public static void ApplyTurnBannerImage(Image image)
        {
            if (image == null)
                return;
            if (ColonistUiKit.Apply(image, UiKitPiece.Banner))
                return;

            var sprite = PanelSpriteSliced();
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }

            image.color = new Color(0.98f, 0.84f, 0.36f, 0.98f);
        }

        /// <summary>Round dark-wood count badge with gold rim (cream digits).</summary>
        public static void ApplyColonistCountBadge(Image image)
        {
            if (image == null)
                return;
            if (ColonistUiKit.Apply(image, UiKitPiece.Badge, 0.7f))
                return;

            var sprite = PanelSpriteSliced();
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }

            image.color = new Color(0.94f, 0.86f, 0.70f, 0.98f);
        }

        /// <summary>Mini parchment card with a faint wash of the resource colour.</summary>
        public static void ApplyColonistResourceCardBackground(Image image, Color resourceTint)
        {
            if (image == null)
                return;
            var wash = Color.Lerp(Color.white, resourceTint, 0.30f);
            wash.a = 1f;
            if (ColonistUiKit.Apply(image, UiKitPiece.FrameThin, 1f, wash))
                return;

            var sprite = PanelSpriteSliced();
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }

            image.color = Color.Lerp(resourceTint, new Color(0.98f, 0.94f, 0.86f, 1f), 0.22f);
        }

        public static Sprite CardFrameSprite() => CardFrameSpriteSliced();

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

        private static Sprite PanelSpriteSliced() =>
            NeutralPanelSprite()
            ?? CreateSlicedSprite("Art/UI/HudPanel", new Vector4(14f, 14f, 14f, 14f));

        private static Sprite ButtonSpriteSliced() =>
            NeutralButtonSprite()
            ?? CreateSlicedSprite("Art/UI/HudButton", new Vector4(14f, 14f, 14f, 14f));

        private static Sprite ButtonSprite() =>
            CreateSlicedSprite("Art/UI/HudButton", new Vector4(14f, 14f, 14f, 14f));

        private static Sprite CardFrameSpriteSliced() =>
            _cardFrameSprite ??= CreateSlicedSprite("Art/Boardgame/Cards/CardBack_blue", new Vector4(12f, 12f, 12f, 12f));

        private static Sprite _neutralButtonSprite;

        private static Sprite NeutralButtonSprite()
        {
            if (_neutralButtonSprite != null)
                return _neutralButtonSprite;

            var src = Resources.Load<Texture2D>("Art/UI/HudButton");
            if (src == null || !src.isReadable)
                return CreateSlicedSprite("Art/UI/HudButton", new Vector4(14f, 14f, 14f, 14f));

            var pixels = src.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                bool blueFill = c.b - c.r > 0.12f;
                float v = blueFill ? 1f : Mathf.Lerp(0.40f, 0.58f, c.grayscale);
                pixels[i] = new Color(v, v, v, c.a);
            }

            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false)
            {
                name = "HudButtonNeutral",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            _neutralButtonSprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(8f, 8f, 8f, 8f));
            return _neutralButtonSprite;
        }

        private static Sprite CreateSlicedSprite(string resourcesPath, Vector4 border)
        {
            var tex = Resources.Load<Texture2D>(resourcesPath);
            if (tex == null)
                return null;

            return Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                border);
        }
    }
}
