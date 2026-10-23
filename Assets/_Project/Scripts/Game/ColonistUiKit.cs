using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>Pieces of the v132 painted UI kit (Resources/Art/UIKit, built by tools/art/make_ui_kit.py).</summary>
    public enum UiKitPiece
    {
        FrameOrnate,
        FrameMedium,
        FrameThin,
        Chip,
        ButtonGreen,
        ButtonWood,
        ButtonRed,
        ButtonParchment,
        Badge,
        Banner
    }

    /// <summary>
    /// v132 painted parchment / dark-wood / gold UI kit shared by every HUD panel, button and badge so the
    /// whole HUD matches the v131 civilization-pick and event cards.
    /// </summary>
    public static class ColonistUiKit
    {
        public const string Folder = "Art/UIKit";

        public static readonly Color Ink = new(0.24f, 0.15f, 0.08f, 1f);
        public static readonly Color InkMuted = new(0.44f, 0.33f, 0.22f, 1f);
        public static readonly Color InkGood = new(0.16f, 0.36f, 0.14f, 1f);
        public static readonly Color InkBad = new(0.58f, 0.17f, 0.10f, 1f);
        public static readonly Color InkGold = new(0.56f, 0.40f, 0.14f, 1f);
        public static readonly Color Cream = new(0.98f, 0.92f, 0.76f, 1f);
        public static readonly Color Rule = new(0.55f, 0.40f, 0.22f, 0.55f);

        // Slice borders in texture pixels (keep in sync with BORDERS in tools/art/make_ui_kit.py).
        private static readonly Dictionary<UiKitPiece, float> Borders = new()
        {
            { UiKitPiece.FrameOrnate, 44f },
            { UiKitPiece.FrameMedium, 20f },
            { UiKitPiece.FrameThin, 10f },
            { UiKitPiece.Chip, 10f },
            { UiKitPiece.ButtonGreen, 14f },
            { UiKitPiece.ButtonWood, 14f },
            { UiKitPiece.ButtonRed, 14f },
            { UiKitPiece.ButtonParchment, 14f },
            { UiKitPiece.Badge, 15f },
            { UiKitPiece.Banner, 16f },
        };

        private static readonly Dictionary<UiKitPiece, Sprite> Cache = new();

        public static Sprite Get(UiKitPiece piece)
        {
            if (Cache.TryGetValue(piece, out var cached) && cached != null)
                return cached;
            var tex = Resources.Load<Texture2D>(Folder + "/" + piece);
            if (tex == null)
                return null;
            float b = Borders[piece];
            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(b, b, b, b));
            sprite.name = "UIKit/" + piece;
            Cache[piece] = sprite;
            return sprite;
        }

        /// <summary>Sliced kit sprite on <paramref name="image"/>; scale &lt; 1 draws the border thinner.</summary>
        public static bool Apply(Image image, UiKitPiece piece, float scale = 1f, Color? tint = null)
        {
            if (image == null)
                return false;
            var sprite = Get(piece);
            if (sprite == null)
                return false;
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.05f, scale);
            image.color = tint ?? Color.white;
            return true;
        }

        public static bool IsLightPiece(UiKitPiece piece) =>
            piece is UiKitPiece.FrameOrnate or UiKitPiece.FrameMedium or UiKitPiece.FrameThin or UiKitPiece.Chip
                or UiKitPiece.ButtonParchment;

        /// <summary>Text colour that reads on a kit piece (ink on parchment, cream on wood / lacquer).</summary>
        public static Color TextOn(UiKitPiece piece) => IsLightPiece(piece) ? Ink : Cream;

        /// <summary>Thin brown rule like the one under the civilization card name.</summary>
        public static Image CreateRule(Transform parent, string name = "Rule")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.color = Rule;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Soft drop shadow under a cream label on wood, matching the card titles.</summary>
        public static void AddTextShadow(Graphic graphic, float distance = 1.2f)
        {
            if (graphic == null || graphic.GetComponent<Shadow>() != null)
                return;
            var s = graphic.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.6f);
            s.effectDistance = new Vector2(distance, -distance);
        }
    }
}
