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
            bg.color = new Color(0.18f, 0.42f, 0.62f);

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(cardGo.transform, false);
            var iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.12f, 0.22f);
            iconRect.anchorMax = new Vector2(0.88f, 0.82f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconImg = iconGo.AddComponent<Image>();
            ColonistHudIconGraphic.ApplyCardIcon(iconImg, ColonistHudResourceSprites.Get(ResourceType.Wheat));

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
            bg.color = new Color(0.18f, 0.42f, 0.62f);
            _fishBankCountText = RunHudUiFactory.CreateText(
                cardGo.transform,
                "Count",
                "0",
                14,
                TextAnchor.MiddleCenter,
                Color.white,
                FontStyle.Bold);
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
