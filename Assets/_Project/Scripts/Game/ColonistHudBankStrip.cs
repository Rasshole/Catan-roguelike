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
        public const float ReferenceCardHeight = 48f;
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
            bg.color = BoardResourceColors.GetHudSwatch(resource);

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(cardGo.transform, false);
            var iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.12f, 0.18f);
            iconRect.anchorMax = new Vector2(0.88f, 0.88f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconImg = iconGo.AddComponent<Image>();
            ColonistHudIconGraphic.ApplyCardIcon(iconImg, ColonistHudResourceSprites.Get(resource));

            var badgeGo = new GameObject("Count");
            badgeGo.transform.SetParent(cardGo.transform, false);
            var badgeRect = badgeGo.AddComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(1f, 1f);
            badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.pivot = new Vector2(1f, 1f);
            badgeRect.anchoredPosition = new Vector2(-1f, -1f);
            badgeRect.sizeDelta = new Vector2(20f, 16f);
            var badgeImg = badgeGo.AddComponent<Image>();
            ColonistKenneyHudArt.ApplyColonistCountBadge(badgeImg);

            countLabel = RunHudUiFactory.CreateText(
                badgeGo.transform,
                "Label",
                "0",
                11,
                TextAnchor.MiddleCenter,
                RunHudUiFactory.ColonistTextOnPanel,
                FontStyle.Bold);
            countLabel.raycastTarget = false;
            return cardGo;
        }
    }
}
