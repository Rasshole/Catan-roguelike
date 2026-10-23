using CatanRoguelike.Core;
using CatanRoguelike.Core.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    internal static class ColonistBuildButtonChrome
    {
        public static Button Create(Transform parent, string shortTitle, Color iconTint, out Text pieceCountChip)
        {
            var button = RunHudUiFactory.CreateButton(parent, string.Empty, RunHudUiFactory.ButtonSecondary);
            foreach (Transform child in button.transform)
                ColonistSceneDestroy.SafeDestroy(child.gameObject);

            var labelModel = button.gameObject.GetComponent<ColonistBuildButtonLabel>();
            if (labelModel == null)
                labelModel = button.gameObject.AddComponent<ColonistBuildButtonLabel>();
            labelModel.SetTitle(shortTitle);
            if (shortTitle == ColonistHudCopy.Settlement)
                ColonistActionBarLabels.SetSettlementTitle(shortTitle);

            var layout = button.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            var titleRow = new GameObject("TitleRow");
            titleRow.transform.SetParent(button.transform, false);
            var titleRowLayout = titleRow.AddComponent<HorizontalLayoutGroup>();
            titleRowLayout.spacing = 4f;
            titleRowLayout.childAlignment = TextAnchor.MiddleCenter;
            titleRowLayout.childForceExpandWidth = false;
            titleRowLayout.childForceExpandHeight = false;
            titleRowLayout.childControlWidth = true;
            titleRowLayout.childControlHeight = true;
            titleRowLayout.padding = new RectOffset(0, 26, 0, 0);
            RunHudUiFactory.AddLayoutElement(titleRow, 22f);

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(titleRow.transform, false);
            RunHudUiFactory.AddLayoutElement(iconGo, 22f).preferredWidth = 22f;
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.preserveAspect = true;
            var buildSprite = ColonistKenneyHudIcons.BuildIconFor(shortTitle);
            if (buildSprite != null)
            {
                iconImg.sprite = buildSprite;
                iconImg.color = Color.white;
            }
            else
                iconImg.color = iconTint;

            var title = RunHudUiFactory.CreateText(
                titleRow.transform,
                "Title",
                shortTitle,
                12,
                TextAnchor.MiddleLeft,
                RunHudUiFactory.ColonistTextOnPanel,
                FontStyle.Bold);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            var titleLayout = title.gameObject.AddComponent<LayoutElement>();
            titleLayout.flexibleWidth = 1f;
            titleLayout.preferredWidth = 64f;

            var cost = RunHudUiFactory.CreateText(
                button.transform,
                "Cost",
                string.Empty,
                10,
                TextAnchor.MiddleCenter,
                RunHudUiFactory.ColonistTextMutedOnPanel);
            cost.horizontalOverflow = HorizontalWrapMode.Overflow;
            cost.gameObject.SetActive(false);
            RunHudUiFactory.AddLayoutElement(cost.gameObject, 14f);

            var costIcons = new GameObject("CostIcons");
            costIcons.transform.SetParent(button.transform, false);
            var costRow = costIcons.AddComponent<HorizontalLayoutGroup>();
            costRow.spacing = 3f;
            costRow.childAlignment = TextAnchor.MiddleCenter;
            costRow.childForceExpandWidth = false;
            costRow.childControlWidth = true;
            costRow.childForceExpandHeight = false;
            costRow.childControlHeight = true;
            RunHudUiFactory.AddLayoutElement(costIcons, 40f);

            var chipGo = new GameObject("Chip");
            chipGo.transform.SetParent(button.transform, false);
            var chipRect = chipGo.AddComponent<RectTransform>();
            chipRect.anchorMin = new Vector2(1f, 1f);
            chipRect.anchorMax = new Vector2(1f, 1f);
            chipRect.pivot = new Vector2(1f, 1f);
            chipRect.anchoredPosition = new Vector2(-2f, -2f);
            chipRect.sizeDelta = new Vector2(22f, 18f);
            ColonistKenneyHudArt.ApplyColonistCountBadge(chipGo.AddComponent<Image>());
            chipGo.AddComponent<LayoutElement>().ignoreLayout = true;
            pieceCountChip = RunHudUiFactory.CreateText(
                chipGo.transform,
                "Count",
                "0",
                12,
                TextAnchor.MiddleCenter,
                ColonistUiKit.Cream,
                FontStyle.Bold);
            pieceCountChip.raycastTarget = false;

            // Width follows the cost chip count (Settlement 4, Dev 3, Road/City 2): at 116/94 the
            // 34 px chips were squeezed and Settlement lost every count (v113 review).
            float buttonWidth = shortTitle == ColonistHudCopy.Settlement ? 184f
                : shortTitle == "Dev" ? 140f
                : 96f;
            RunHudUiFactory.AddLayoutElement(button.gameObject, 88f).preferredWidth = buttonWidth;

            labelModel.SetTitle(shortTitle);
            return button;
        }

        public static string GetTitleText(Button button)
        {
            if (button == null)
                return string.Empty;

            var fromModel = ColonistBuildButtonLabel.GetTitle(button);
            if (!string.IsNullOrEmpty(fromModel))
                return fromModel;

            var title = button.transform.Find("TitleRow/Title")?.GetComponent<Text>();
            if (title != null && !string.IsNullOrEmpty(title.text))
                return title.text;

            foreach (var text in button.GetComponentsInChildren<Text>(true))
            {
                if (text.gameObject.name == "Title")
                    return text.text;
            }

            return string.Empty;
        }

        public static void SetTitle(Button button, string shortTitle)
        {
            if (button == null)
                return;

            var model = button.GetComponent<ColonistBuildButtonLabel>();
            if (model != null)
                model.SetTitle(shortTitle);
            else
            {
                var title = button.transform.Find("TitleRow/Title")?.GetComponent<Text>();
                if (title != null)
                    title.text = shortTitle;
            }

            if (shortTitle == ColonistHudCopy.Settlement)
                ColonistActionBarLabels.SetSettlementTitle(shortTitle);
        }

        public static void SetCost(Button button, ResourceBundle cost, bool canAfford)
        {
            if (button == null)
                return;

            var costIcons = button.transform.Find("CostIcons");
            if (costIcons == null)
                return;

            for (int i = costIcons.childCount - 1; i >= 0; i--)
                ColonistSceneDestroy.SafeDestroy(costIcons.GetChild(i).gameObject);

            void AddChip(ResourceType resource, int amount)
            {
                if (amount <= 0)
                    return;

                var chip = new GameObject($"Cost_{resource}");
                chip.transform.SetParent(costIcons, false);
                var row = chip.AddComponent<HorizontalLayoutGroup>();
                row.spacing = 1f;
                row.childAlignment = TextAnchor.MiddleCenter;
                row.childForceExpandWidth = false;
                row.childControlWidth = true;
                RunHudUiFactory.AddLayoutElement(chip, 32f);

                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(chip.transform, false);
                RunHudUiFactory.AddLayoutElement(iconGo, 28f).preferredWidth = 28f;
                var img = iconGo.AddComponent<Image>();
                ColonistHudIconGraphic.ApplyCardIcon(img, ColonistHudResourceSprites.Get(resource), fill: 0.94f);

                var amt = RunHudUiFactory.CreateText(
                    chip.transform,
                    "Amt",
                    amount.ToString(),
                    13,
                    TextAnchor.MiddleLeft,
                    canAfford ? RunHudUiFactory.ColonistTextOnPanel : ColonistUiKit.InkBad,
                    FontStyle.Bold);
                amt.raycastTarget = false;
                RunHudUiFactory.AddLayoutElement(amt.gameObject, 20f).preferredWidth = 11f;
            }

            AddChip(ResourceType.Wood, cost.Wood);
            AddChip(ResourceType.Brick, cost.Brick);
            AddChip(ResourceType.Wheat, cost.Wheat);
            AddChip(ResourceType.Sheep, cost.Sheep);
            AddChip(ResourceType.Stone, cost.Stone);
        }

        /// <summary>Standard dev-card purchase cost chips (1 wool, 1 grain, 1 ore).</summary>
        public static void SetDevPurchaseCost(Button button, bool canAfford)
        {
            if (button == null)
                return;

            var bundle = new ResourceBundle { Sheep = 1, Wheat = 1, Stone = 1 };
            SetCost(button, bundle, canAfford);
        }

        public static Color IconTintFor(string shortTitle) => shortTitle switch
        {
            _ when shortTitle == ColonistHudCopy.Road => new Color(0.72f, 0.58f, 0.38f, 1f),
            _ when shortTitle == ColonistHudCopy.SettlementShort || shortTitle == ColonistHudCopy.Settlement
                => new Color(0.94f, 0.92f, 0.86f, 1f),
            _ when shortTitle == ColonistHudCopy.City => new Color(0.88f, 0.62f, 0.28f, 1f),
            _ => new Color(0.55f, 0.62f, 0.92f, 1f)
        };
    }
}
