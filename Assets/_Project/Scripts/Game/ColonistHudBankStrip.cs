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

        public sealed class Widget
        {
            public readonly List<Text> CountLabels = new();
        }

        public static Widget Build(RectTransform parent, float height)
        {
            var widget = new Widget();
            var layout = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childControlHeight = true;
            layout.padding = new RectOffset(4, 4, 2, 2);

            foreach (ResourceType resource in System.Enum.GetValues(typeof(ResourceType)))
            {
                var card = CreateMiniCard(parent, resource, height - 4f, out Text countLabel);
                widget.CountLabels.Add(countLabel);
            }

            return widget;
        }

        public static void RefreshCounts(Widget widget, BankStock bank)
        {
            int i = 0;
            foreach (ResourceType resource in System.Enum.GetValues(typeof(ResourceType)))
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
            out Text countLabel)
        {
            const float cardWidth = 52f;
            var cardGo = new GameObject($"BankCard_{resource}");
            cardGo.transform.SetParent(parent, false);
            RunHudUiFactory.AddLayoutElement(cardGo, height).preferredWidth = cardWidth;

            var bg = cardGo.AddComponent<Image>();
            bg.color = BoardResourceColors.GetHudSwatch(resource);

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(cardGo.transform, false);
            var iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.55f);
            iconRect.anchorMax = new Vector2(0.5f, 0.55f);
            iconRect.sizeDelta = new Vector2(30f, 30f);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite = ColonistHudResourceSprites.Get(resource);
            iconImg.preserveAspect = true;

            var badgeGo = new GameObject("Count");
            badgeGo.transform.SetParent(cardGo.transform, false);
            var badgeRect = badgeGo.AddComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(1f, 1f);
            badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.pivot = new Vector2(1f, 1f);
            badgeRect.anchoredPosition = new Vector2(-2f, -2f);
            badgeRect.sizeDelta = new Vector2(24f, 18f);
            badgeGo.AddComponent<Image>().color = new Color(0.08f, 0.10f, 0.14f, 0.92f);

            countLabel = RunHudUiFactory.CreateText(
                badgeGo.transform,
                "Label",
                "0",
                12,
                TextAnchor.MiddleCenter,
                RunHudUiFactory.TextPrimary,
                FontStyle.Bold);
            countLabel.raycastTarget = false;
            return cardGo;
        }
    }
}
