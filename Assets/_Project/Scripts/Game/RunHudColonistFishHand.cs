using CatanRoguelike.Core;
using CatanRoguelike.Core.Map;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    public sealed partial class RunHudController
    {
        private GameObject _fishHandCard;
        private Text _fishHandCountText;
        private Text _fishBankCountText;
        private static readonly Color FishCardTint = new(0.18f, 0.42f, 0.62f);

        private void EnsureColonistFishHandCard()
        {
            if (_fishHandCard != null || _handPanel == null || !ColonistPresentation.IsActive)
                return;

            const float cardW = 72f;
            const float cardH = 96f;
            var cardGo = new GameObject("ResCard_Fish");
            cardGo.transform.SetParent(_handPanel, false);
            cardGo.AddComponent<RectTransform>();
            RunHudUiFactory.AddLayoutElement(cardGo, cardH).preferredWidth = cardW;

            var bg = cardGo.AddComponent<Image>();
            ColonistKenneyHudArt.ApplyColonistResourceCardBackground(bg, FishCardTint);

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(cardGo.transform, false);
            var iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.12f, 0.22f);
            iconRect.anchorMax = new Vector2(0.88f, 0.82f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconImg = iconGo.AddComponent<Image>();
            ColonistHudIconGraphic.ApplyCardIcon(iconImg, ColonistHudResourceSprites.GetFish(), fill: 0.56f, centerY: 0.52f);

            var countGo = new GameObject("Count");
            countGo.transform.SetParent(cardGo.transform, false);
            var countRect = countGo.AddComponent<RectTransform>();
            countRect.anchorMin = new Vector2(0f, 0f);
            countRect.anchorMax = new Vector2(1f, 0.28f);
            countRect.offsetMin = Vector2.zero;
            countRect.offsetMax = Vector2.zero;
            _fishHandCountText = RunHudUiFactory.CreateText(
                countGo.transform,
                "Value",
                "0",
                20,
                TextAnchor.MiddleCenter,
                Color.white,
                FontStyle.Bold);

            _fishHandCard = cardGo;
        }

        private void EnsureColonistFishBankLabel(RectTransform bankStrip)
        {
            if (_fishBankCountText != null || bankStrip == null)
                return;

            var cardGo = new GameObject("BankCard_Fish");
            cardGo.transform.SetParent(bankStrip, false);
            float height = ColonistHudBankStrip.ReferenceCardHeight;
            float width = height * ColonistHudBankStrip.CardAspectWidthOverHeight;
            RunHudUiFactory.AddLayoutElement(cardGo, height).preferredWidth = width;
            var bg = cardGo.AddComponent<Image>();
            // v114: same Kenney card frame, icon and count chip as the five resource bank cards
            // (was a flat blue rectangle with only a number).
            ColonistKenneyHudArt.ApplyColonistResourceCardBackground(bg, FishCardTint);

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(cardGo.transform, false);
            var iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.10f, 0.28f);
            iconRect.anchorMax = new Vector2(0.90f, 0.96f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconImg = iconGo.AddComponent<Image>();
            ColonistHudIconGraphic.ApplyCardIcon(iconImg, ColonistHudResourceSprites.GetFish(), fill: 0.70f, centerY: 0.62f);

            var badgeGo = new GameObject("CountBadge");
            badgeGo.transform.SetParent(cardGo.transform, false);
            var badgeRect = badgeGo.AddComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.5f, 0f);
            badgeRect.anchorMax = new Vector2(0.5f, 0f);
            badgeRect.pivot = new Vector2(0.5f, 0f);
            badgeRect.anchoredPosition = new Vector2(0f, 1f);
            badgeRect.sizeDelta = new Vector2(26f, 17f);
            var badgeImg = badgeGo.AddComponent<Image>();
            ColonistKenneyHudArt.ApplyColonistCountBadge(badgeImg);

            _fishBankCountText = RunHudUiFactory.CreateText(
                badgeGo.transform,
                "Count",
                "0",
                13,
                TextAnchor.MiddleCenter,
                RunHudUiFactory.ColonistTextOnPanel,
                FontStyle.Bold);
            _fishBankCountText.raycastTarget = false;
            _fishBankCountText.verticalOverflow = VerticalWrapMode.Overflow;
            _fishBankCountText.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private void RefreshColonistFishHud(GameState state)
        {
            if (!ColonistPresentation.IsActive || !state.FishermenOfCatanEnabled)
            {
                if (_fishHandCard != null)
                    _fishHandCard.SetActive(false);
                return;
            }

            EnsureColonistFishHandCard();
            if (_colonistBankStripRoot != null)
                EnsureColonistFishBankLabel(_colonistBankStripRoot);

            if (_fishHandCard != null)
                _fishHandCard.SetActive(true);
            if (_fishHandCountText != null)
                _fishHandCountText.text = state.PlayerFish.ToString();
            if (_fishBankCountText != null)
                _fishBankCountText.text = (state.FishBagRemaining?.Count ?? 0).ToString();
        }
    }
}
