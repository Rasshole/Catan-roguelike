using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    internal static class RunHudUiFactory
    {
        public static readonly Color PanelFill = new(0.92f, 0.86f, 0.72f, 0.94f);
        public static readonly Color PanelBorder = new(0.28f, 0.36f, 0.46f, 1f);
        public static readonly Color TextPrimary = new(0.95f, 0.96f, 0.98f, 1f);
        public static readonly Color TextMuted = new(0.72f, 0.78f, 0.86f, 1f);
        public static readonly Color ButtonFill = new(0.22f, 0.58f, 0.38f, 1f);
        public static readonly Color ButtonSecondary = new(0.30f, 0.40f, 0.52f, 1f);
        public static readonly Color HandBackdrop = new(0.10f, 0.12f, 0.16f, 0.82f);
        public static readonly Color ColonistWoodBarFill = new(0.84f, 0.72f, 0.54f, 0.94f);
        // v132: ink-brown text on the painted parchment kit (matches the civ-pick / event cards).
        public static readonly Color ColonistTextOnPanel = ColonistUiKit.Ink;
        public static readonly Color ColonistTextMutedOnPanel = ColonistUiKit.InkMuted;

        public static Canvas CreateCanvas(Transform parent)
        {
            var go = new GameObject("RunHudCanvas");
            go.transform.SetParent(parent, false);
            go.layer = RunHudController.HudCaptureUiLayer;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform CreatePanel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var image = go.AddComponent<Image>();
            image.color = PanelFill;
            ColonistKenneyHudArt.ApplyPanelImage(image);
            return rect;
        }

        public static Text CreateText(
            Transform parent,
            string name,
            string text,
            int fontSize,
            TextAnchor alignment,
            Color color,
            FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var label = go.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color;
            label.fontStyle = style;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        public static Button CreateButton(Transform parent, string label, Color fill)
        {
            var go = new GameObject("Button");
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = fill;
            if (ColonistPresentation.IsActive)
            {
                bool green = fill == ButtonFill || fill.g > fill.r + 0.08f;
                ColonistKenneyHudArt.ApplyColonistWarmButton(image, green);
            }
            else if (fill == ButtonFill)
                ColonistKenneyHudArt.ApplyPrimaryButtonImage(image);
            else
                ColonistKenneyHudArt.ApplySecondaryButtonImage(image);

            var button = go.AddComponent<Button>();
            go.AddComponent<ButtonJuice>(); // v146: hover/press scale feedback
            // v143: a soft wooden click on every HUD button (deduped, respects the volume/mute settings).
            button.onClick.AddListener(() => GameAudio.Play(Sfx.Click, 0.7f));
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.97f, 0.88f, 1f);
            colors.pressedColor = new Color(0.86f, 0.80f, 0.70f, 1f);
            colors.disabledColor = new Color(0.78f, 0.74f, 0.68f, 0.75f);
            button.colors = colors;

            bool onLacquer = image.sprite == ColonistUiKit.Get(UiKitPiece.ButtonGreen)
                             || image.sprite == ColonistUiKit.Get(UiKitPiece.ButtonWood)
                             || image.sprite == ColonistUiKit.Get(UiKitPiece.ButtonRed);
            var labelColor = onLacquer ? ColonistUiKit.Cream
                : ColonistPresentation.IsActive || image.sprite == ColonistUiKit.Get(UiKitPiece.ButtonParchment)
                    ? ColonistTextOnPanel
                    : TextPrimary;
            var text = CreateText(go.transform, "Label", label, 16, TextAnchor.MiddleCenter, labelColor, FontStyle.Bold);
            text.raycastTarget = false;
            if (onLacquer)
                ColonistUiKit.AddTextShadow(text);
            return button;
        }

        public static Button CreateColonistEndTurnButton(Transform parent, string label)
        {
            var button = CreateButton(parent, label, ButtonFill);
            var image = button.GetComponent<Image>();
            ColonistKenneyHudArt.ApplyColonistEndTurnButton(image);
            var colors = button.colors;
            colors.highlightedColor = Color.Lerp(image.color, Color.white, 0.15f);
            colors.pressedColor = Color.Lerp(image.color, Color.black, 0.12f);
            button.colors = colors;
            return button;
        }

        public static LayoutElement AddLayoutElement(
            GameObject go,
            float preferredHeight,
            float flexibleWidth = 0f)
        {
            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = preferredHeight;
            layout.flexibleWidth = flexibleWidth;
            return layout;
        }

        /// <summary>Section title inset from Kenney panel border (right rail log/chat).</summary>
        public static Text CreateRailSectionTitle(Transform panel, string title)
        {
            var go = new GameObject("Title");
            go.transform.SetParent(panel, false);
            go.layer = panel.gameObject.layer;
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.offsetMin = new Vector2(10f, -28f);
            rect.offsetMax = new Vector2(-10f, -8f);

            var label = go.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = title;
            label.fontSize = 14;
            label.alignment = TextAnchor.UpperLeft;
            label.color = ColonistPresentation.IsActive ? ColonistUiKit.InkGold : ColonistTextMutedOnPanel;
            label.fontStyle = FontStyle.Bold;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
    }
}
