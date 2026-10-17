using CatanRoguelike.Core;
using CatanRoguelike.Core.Yield;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    public sealed partial class RunHudController
    {
        private RectTransform _fishSpendRoot;
        private Text _fishBalanceText;

        private void EnsureFishSpendPanel()
        {
            if (_fishSpendRoot != null || _canvas == null || !ColonistPresentation.IsActive)
                return;

            var go = new GameObject("FishSpendPanel");
            go.transform.SetParent(_canvas.transform, false);
            _fishSpendRoot = go.AddComponent<RectTransform>();
            _fishSpendRoot.anchorMin = new Vector2(0f, 0.42f);
            _fishSpendRoot.anchorMax = new Vector2(0f, 0.42f);
            _fishSpendRoot.pivot = new Vector2(0f, 0.5f);
            _fishSpendRoot.anchoredPosition = new Vector2(12f, 0f);
            _fishSpendRoot.sizeDelta = new Vector2(188f, 248f);

            var bg = go.AddComponent<Image>();
            ColonistKenneyHudArt.ApplyPanelImage(bg);

            BuildFishSpendHeader(go.transform);

            float y = -52f;
            AddFishSpendButton("Robber off", 2, FishermenSpendAction.MoveRobberOffBoard, ref y);
            AddFishSpendButton("Steal", 3, FishermenSpendAction.StealFromOpponent, ref y);
            AddFishSpendButton("Resource", 4, FishermenSpendAction.DrawResource, ref y);
            AddFishSpendButton("Free road", 5, FishermenSpendAction.FreeRoad, ref y);
            AddFishSpendButton("Dev card", 7, FishermenSpendAction.DrawDevelopmentCard, ref y);
        }

        private void BuildFishSpendHeader(Transform parent)
        {
            var headerGo = new GameObject("Header");
            headerGo.transform.SetParent(parent, false);
            var headerRect = headerGo.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = new Vector2(0f, -10f);
            headerRect.sizeDelta = new Vector2(-16f, 36f);

            var row = headerGo.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 8f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childForceExpandWidth = false;
            row.childControlWidth = true;
            row.childForceExpandHeight = false;
            row.childControlHeight = true;
            row.padding = new RectOffset(4, 4, 0, 0);

            var iconGo = new GameObject("FishIcon");
            iconGo.transform.SetParent(headerGo.transform, false);
            RunHudUiFactory.AddLayoutElement(iconGo, 28f).preferredWidth = 28f;
            var iconImg = iconGo.AddComponent<Image>();
            ColonistHudIconGraphic.ApplyCardIcon(iconImg, ColonistHudResourceSprites.GetFish(), fill: 0.9f);
            iconImg.raycastTarget = false;

            _fishBalanceText = RunHudUiFactory.CreateText(
                headerGo.transform,
                "Balance",
                "Fish: 0",
                17,
                TextAnchor.MiddleLeft,
                RunHudUiFactory.ColonistTextOnPanel,
                FontStyle.Bold);
            _fishBalanceText.raycastTarget = false;
            var balanceLayout = _fishBalanceText.gameObject.AddComponent<LayoutElement>();
            balanceLayout.flexibleWidth = 1f;
        }

        private void RefreshFishSpendPanel(GameState state)
        {
            EnsureFishSpendPanel();
            if (_fishSpendRoot == null)
                return;

            bool show = state.FishermenOfCatanEnabled;
            _fishSpendRoot.gameObject.SetActive(show);
            if (!show)
                return;

            if (_fishBalanceText != null)
                _fishBalanceText.text = $"Fish: {state.PlayerFish}";
        }

        private void AddFishSpendButton(string title, int fishCost, FishermenSpendAction action, ref float y)
        {
            var buttonGo = new GameObject($"Fish_{title}");
            buttonGo.transform.SetParent(_fishSpendRoot, false);
            var rect = buttonGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(-16f, 36f);
            y -= 40f;

            var image = buttonGo.AddComponent<Image>();
            ColonistKenneyHudArt.ApplyColonistWarmButton(image, primaryGreen: true);
            var button = buttonGo.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                if (_controller != null && _controller.TrySpendFish(action))
                    Refresh(_controller.State);
            });

            var rowGo = new GameObject("Row");
            rowGo.transform.SetParent(buttonGo.transform, false);
            var rowRect = rowGo.AddComponent<RectTransform>();
            rowRect.anchorMin = Vector2.zero;
            rowRect.anchorMax = Vector2.one;
            rowRect.offsetMin = new Vector2(8f, 4f);
            rowRect.offsetMax = new Vector2(-8f, -4f);
            var row = rowGo.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 6f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childForceExpandWidth = false;
            row.childControlWidth = true;
            row.childForceExpandHeight = true;
            row.childControlHeight = true;

            var titleText = RunHudUiFactory.CreateText(
                rowGo.transform,
                "Title",
                title,
                13,
                TextAnchor.MiddleLeft,
                RunHudUiFactory.ColonistTextOnPanel,
                FontStyle.Bold);
            titleText.raycastTarget = false;
            var titleLayout = titleText.gameObject.AddComponent<LayoutElement>();
            titleLayout.flexibleWidth = 1f;

            var costGo = new GameObject("Cost");
            costGo.transform.SetParent(rowGo.transform, false);
            var costRow = costGo.AddComponent<HorizontalLayoutGroup>();
            costRow.spacing = 3f;
            costRow.childAlignment = TextAnchor.MiddleRight;
            costRow.childForceExpandWidth = false;
            costRow.childControlWidth = true;
            RunHudUiFactory.AddLayoutElement(costGo, 24f).preferredWidth = 44f;

            var fishIconGo = new GameObject("Fish");
            fishIconGo.transform.SetParent(costGo.transform, false);
            RunHudUiFactory.AddLayoutElement(fishIconGo, 22f).preferredWidth = 22f;
            var fishImg = fishIconGo.AddComponent<Image>();
            ColonistHudIconGraphic.ApplyCardIcon(fishImg, ColonistHudResourceSprites.GetFish(), fill: 0.92f);
            fishImg.raycastTarget = false;

            var costText = RunHudUiFactory.CreateText(
                costGo.transform,
                "Amount",
                fishCost.ToString(),
                14,
                TextAnchor.MiddleLeft,
                RunHudUiFactory.ColonistTextOnPanel,
                FontStyle.Bold);
            costText.raycastTarget = false;
            RunHudUiFactory.AddLayoutElement(costText.gameObject, 22f).preferredWidth = 18f;
        }
    }
}
