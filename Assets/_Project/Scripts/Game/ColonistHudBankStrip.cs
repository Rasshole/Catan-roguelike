using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Trade;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    public static class ColonistHudBankStrip
    {
        public const int ExpectedCardCount = 5;
        public const float ReferenceCardHeight = 58f;
        public const float CardAspectWidthOverHeight = 0.7f;

        public sealed class Widget
        {
            public readonly List<Text> CountLabels = new();
        }

        public static Widget Build(RectTransform parent, float cardHeight = ReferenceCardHeight)
        {
            var widget = new Widget();
            var layout = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.padding = new RectOffset(2, 2, 2, 2);

            float cardWidth = cardHeight * CardAspectWidthOverHeight;
            foreach (ResourceType resource in ResourceTypes.Productive)
            {
                var card = CreateMiniCard(parent, resource, cardHeight, cardWidth, out Text countLabel);
                widget.CountLabels.Add(countLabel);
            }

            return widget;
        }

        public static void RefreshCounts(Widget widget, BankStock bank)
        {
            int i = 0;
            foreach (ResourceType resource in ResourceTypes.Productive)
            {
                if (i >= widget.CountLabels.Count)
                    break;
                widget.CountLabels[i].text = bank.Get(resource).ToString();
                i++;
            }
        }

        private static GameObject CreateMiniCard(
            RectTransform parent,
            ResourceType resource,
            float height,
            float width,
            out Text countLabel)
        {
            var cardGo = new GameObject($"BankCard_{resource}");
            cardGo.transform.SetParent(parent, false);
            RunHudUiFactory.AddLayoutElement(cardGo, height).preferredWidth = width;

            var bg = cardGo.AddComponent<Image>();
            ColonistKenneyHudArt.ApplyColonistResourceCardBackground(bg, BoardResourceColors.GetHudSwatch(resource));

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(cardGo.transform, false);
            var iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.10f, 0.28f);
            iconRect.anchorMax = new Vector2(0.90f, 0.96f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconImg = iconGo.AddComponent<Image>();
            // Keep the glyph above the bottom count badge so the badge never covers it.
            ColonistHudIconGraphic.ApplyCardIcon(iconImg, ColonistHudResourceSprites.Get(resource), fill: 0.70f, centerY: 0.62f);

            var badgeGo = new GameObject("Count");
            badgeGo.transform.SetParent(cardGo.transform, false);
            var badgeRect = badgeGo.AddComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.5f, 0f);
            badgeRect.anchorMax = new Vector2(0.5f, 0f);
            badgeRect.pivot = new Vector2(0.5f, 0f);
            badgeRect.anchoredPosition = new Vector2(0f, 1f);
            badgeRect.sizeDelta = new Vector2(26f, 17f);
            var badgeImg = badgeGo.AddComponent<Image>();
            ColonistKenneyHudArt.ApplyColonistCountBadge(badgeImg);

            countLabel = RunHudUiFactory.CreateText(
                badgeGo.transform,
                "Label",
                "0",
                12,
                TextAnchor.MiddleCenter,
                ColonistUiKit.Cream,
                FontStyle.Bold);
            countLabel.raycastTarget = false;
            countLabel.verticalOverflow = VerticalWrapMode.Overflow;
            countLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            return cardGo;
        }
    }
}
