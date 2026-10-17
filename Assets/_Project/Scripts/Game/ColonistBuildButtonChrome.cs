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
            RunHudUiFactory.AddLayoutElement(iconGo, 18f).preferredWidth = 18f;
            iconGo.AddComponent<Image>().color = iconTint;

            var title = RunHudUiFactory.CreateText(
                titleRow.transform,
                "Title",
                shortTitle,
                11,
                TextAnchor.MiddleLeft,
                RunHudUiFactory.TextPrimary,
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
                RunHudUiFactory.TextMuted);
            cost.horizontalOverflow = HorizontalWrapMode.Overflow;
            RunHudUiFactory.AddLayoutElement(cost.gameObject, 14f);

            var chipGo = new GameObject("Chip");
            chipGo.transform.SetParent(button.transform, false);
            var chipRect = chipGo.AddComponent<RectTransform>();
            chipRect.anchorMin = new Vector2(1f, 1f);
            chipRect.anchorMax = new Vector2(1f, 1f);
            chipRect.pivot = new Vector2(1f, 1f);
            chipRect.anchoredPosition = new Vector2(-2f, -2f);
            chipRect.sizeDelta = new Vector2(22f, 18f);
            chipGo.AddComponent<Image>().color = new Color(0.1f, 0.12f, 0.16f, 0.9f);
            chipGo.AddComponent<LayoutElement>().ignoreLayout = true;
            pieceCountChip = RunHudUiFactory.CreateText(
                chipGo.transform,
                "Count",
                "0",
                12,
                TextAnchor.MiddleCenter,
                RunHudUiFactory.TextPrimary,
                FontStyle.Bold);
            pieceCountChip.raycastTarget = false;

            float buttonWidth = shortTitle == ColonistHudCopy.Settlement ? 116f : 94f;
            RunHudUiFactory.AddLayoutElement(button.gameObject, 56f).preferredWidth = buttonWidth;

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

            var costLabel = button.transform.Find("Cost")?.GetComponent<Text>();
            if (costLabel == null)
                return;

            costLabel.text = BuildCostFormatter.FormatBankStyleChips(cost);
            // Dark ink on the warm/green Kenney buttons; light pink was unreadable on green.
            costLabel.color = canAfford ? RunHudUiFactory.ColonistTextOnPanel : new Color(0.58f, 0.10f, 0.08f, 1f);
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
