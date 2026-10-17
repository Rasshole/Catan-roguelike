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
            _fishSpendRoot.sizeDelta = new Vector2(168f, 220f);

            var bg = go.AddComponent<Image>();
            ColonistKenneyHudArt.ApplyPanelImage(bg);

            _fishBalanceText = CreateFishLabel(go.transform, "Fish: 0", 0, 28f);

            float y = -36f;
            AddFishSpendButton("Robber off (2)", FishermenSpendAction.MoveRobberOffBoard, ref y);
            AddFishSpendButton("Steal (3)", FishermenSpendAction.StealFromOpponent, ref y);
            AddFishSpendButton("Resource (4)", FishermenSpendAction.DrawResource, ref y);
            AddFishSpendButton("Free road (5)", FishermenSpendAction.FreeRoad, ref y);
            AddFishSpendButton("Dev card (7)", FishermenSpendAction.DrawDevelopmentCard, ref y);
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

        private void AddFishSpendButton(string label, FishermenSpendAction action, ref float y)
        {
            var buttonGo = new GameObject(label);
            buttonGo.transform.SetParent(_fishSpendRoot, false);
            var rect = buttonGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(-12f, 30f);
            y -= 34f;

            var image = buttonGo.AddComponent<Image>();
            ColonistKenneyHudArt.ApplyColonistWarmButton(image);
            var button = buttonGo.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                if (_controller != null && _controller.TrySpendFish(action))
                    Refresh(_controller.State);
            });

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(buttonGo.transform, false);
            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = label;
            text.fontSize = 13;
            text.color = RunHudUiFactory.ColonistTextOnPanel;
            text.alignment = TextAnchor.MiddleCenter;
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        private static Text CreateFishLabel(Transform parent, string text, int index, float height)
        {
            var go = new GameObject($"FishLabel{index}");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -8f);
            rect.sizeDelta = new Vector2(-8f, height);
            var label = go.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = 16;
            label.fontStyle = FontStyle.Bold;
            label.color = RunHudUiFactory.ColonistTextOnPanel;
            label.alignment = TextAnchor.MiddleLeft;
            return label;
        }
    }
}
