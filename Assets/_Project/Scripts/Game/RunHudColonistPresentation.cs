using System.Text;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Core.UI;
using CatanRoguelike.Core.Victory;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>Colonist.io classic layout (right rail + bottom hand/action bar). Built when <see cref="ColonistPresentation.IsActive"/>.</summary>
    public sealed partial class RunHudController
    {
        private bool _colonistLayoutBuilt;
        private RectTransform _colonistRightRail;
        private Text _colonistHumanPanelText;
        private Text _colonistAiPanelText;
        private Text _colonistChatText;
        private Text _colonistDevCountText;
        private RectTransform _colonistDevCardSlot;

        private void EnsureColonistCanvasBuilt()
        {
            if (_colonistLayoutBuilt && _canvas != null)
                return;

            _canvas = RunHudUiFactory.CreateCanvas(transform);
            BuildColonistBottomHand(_canvas.transform);
            BuildActionBarChrome(_canvas.transform);
            BuildColonistRightRail(_canvas.transform);
            EnsureTradeDialogBuilt();
            BuildToastWidget(_canvas.transform);
            BuildIncomingTradePanel(_canvas.transform);
            EnsureFlyLayer();
            RunHudCostTooltip.EnsureShared(_canvas.transform);
            SetLayerRecursively(_canvas.gameObject, HudCaptureUiLayer);
            _colonistLayoutBuilt = true;
        }

        private void ApplyColonistHandAndBarInsets()
        {
            float margin = RunHudLayout.Margin;
            float right = margin + RunHudLayout.RightChromePanelWidth + margin;
            float handBottom = RunHudLayout.GetHandRowBottomInset();
            float actionBottom = RunHudLayout.GetActionBarBottomInset();

            if (_handPanel != null)
            {
                _handPanel.offsetMin = new Vector2(margin, handBottom);
                _handPanel.offsetMax = new Vector2(-right, handBottom + RunHudLayout.BottomHandHeight);
            }

            if (_actionBar != null)
            {
                _actionBar.offsetMin = new Vector2(margin, actionBottom);
                _actionBar.offsetMax = new Vector2(-right, actionBottom + RunHudLayout.BottomActionBarHeight);
            }
        }

        private void BuildColonistBottomHand(Transform root)
        {
            float margin = RunHudLayout.Margin;
            float right = margin + RunHudLayout.RightChromePanelWidth + margin;
            float bottom = RunHudLayout.GetHandRowBottomInset();
            _handPanel = RunHudUiFactory.CreatePanel(
                root,
                "ResourceHand",
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(margin, bottom),
                new Vector2(-right, bottom + RunHudLayout.BottomHandHeight));
            _handPanel.GetComponent<Image>().color = RunHudUiFactory.HandBackdrop;

            var layout = _handPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandHeight = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;

            _resourceAmountTexts.Clear();
            _resourceSwatches.Clear();

            foreach (ResourceType resource in System.Enum.GetValues(typeof(ResourceType)))
            {
                var card = CreateColonistResourceCard(_handPanel, resource);
                _handCardObjects.Add(card);
            }

            var devSlot = new GameObject("DevCardSlot");
            devSlot.transform.SetParent(_handPanel, false);
            _colonistDevCardSlot = devSlot.AddComponent<RectTransform>();
            RunHudUiFactory.AddLayoutElement(devSlot, RunHudLayout.CardHeight - 8f).preferredWidth = 88f;
            var devBg = devSlot.AddComponent<Image>();
            devBg.color = new Color(0.14f, 0.16f, 0.22f, 0.95f);
            RunHudUiFactory.CreateText(
                devSlot.transform,
                "Label",
                ColonistHudCopy.DevCards,
                12,
                TextAnchor.UpperCenter,
                RunHudUiFactory.TextMuted);
            _colonistDevCountText = RunHudUiFactory.CreateText(
                devSlot.transform,
                "Count",
                "0",
                28,
                TextAnchor.MiddleCenter,
                RunHudUiFactory.TextPrimary,
                FontStyle.Bold);

            _handRow = _handPanel;
        }

        private GameObject CreateColonistResourceCard(Transform parent, ResourceType resource)
        {
            var cardGo = new GameObject($"ResCard_{resource}");
            cardGo.transform.SetParent(parent, false);
            var rect = cardGo.AddComponent<RectTransform>();
            RunHudUiFactory.AddLayoutElement(cardGo, RunHudLayout.CardHeight - 8f).preferredWidth = 100f;

            var bg = cardGo.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.14f, 0.18f, 0.96f);

            var stripe = new GameObject("Stripe");
            stripe.transform.SetParent(cardGo.transform, false);
            var stripeRect = stripe.AddComponent<RectTransform>();
            stripeRect.anchorMin = new Vector2(0f, 1f);
            stripeRect.anchorMax = new Vector2(1f, 1f);
            stripeRect.pivot = new Vector2(0.5f, 1f);
            stripeRect.sizeDelta = new Vector2(0f, 36f);
            var stripeImg = stripe.AddComponent<Image>();
            stripeImg.color = BoardResourceColors.GetHudSwatch(resource);
            _resourceSwatches.Add(stripeImg);

            RunHudUiFactory.CreateText(
                cardGo.transform,
                "Name",
                ResourceDisplayNames.GetEnglish(resource),
                13,
                TextAnchor.UpperLeft,
                RunHudUiFactory.TextPrimary,
                FontStyle.Bold);

            var count = RunHudUiFactory.CreateText(
                cardGo.transform,
                "Count",
                "0",
                32,
                TextAnchor.MiddleCenter,
                RunHudUiFactory.TextPrimary,
                FontStyle.Bold);
            _resourceAmountTexts.Add(count);

            return cardGo;
        }

        private void BuildColonistRightRail(Transform root)
        {
            float margin = RunHudLayout.Margin;
            float width = RunHudLayout.RightChromePanelWidth;
            _colonistRightRail = RunHudUiFactory.CreatePanel(
                root,
                "RightRail",
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(-(width + margin), margin),
                new Vector2(-margin, -(RunHudLayout.BottomHandHeight + RunHudLayout.BottomActionBarHeight + margin * 2f)));

            var layout = _colonistRightRail.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 8f;
            layout.childForceExpandWidth = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            var humanPanel = RunHudUiFactory.CreatePanel(
                _colonistRightRail,
                "PlayerYou",
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RunHudUiFactory.AddLayoutElement(humanPanel.gameObject, 108f);
            _colonistHumanPanelText = RunHudUiFactory.CreateText(
                humanPanel,
                "Body",
                string.Empty,
                14,
                TextAnchor.UpperLeft,
                RunHudUiFactory.TextPrimary);

            var aiPanel = RunHudUiFactory.CreatePanel(
                _colonistRightRail,
                "PlayerAi",
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RunHudUiFactory.AddLayoutElement(aiPanel.gameObject, 108f);
            _colonistAiPanelText = RunHudUiFactory.CreateText(
                aiPanel,
                "Body",
                string.Empty,
                14,
                TextAnchor.UpperLeft,
                RunHudUiFactory.TextPrimary);

            var logPanel = RunHudUiFactory.CreatePanel(
                _colonistRightRail,
                "GameLogPanel",
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RunHudUiFactory.AddLayoutElement(logPanel.gameObject, 220f, 1f);
            RunHudUiFactory.CreateText(
                logPanel,
                "Title",
                ColonistHudCopy.GameLog,
                15,
                TextAnchor.UpperLeft,
                RunHudUiFactory.TextMuted,
                FontStyle.Bold);
            _gameLogText = RunHudUiFactory.CreateText(
                logPanel,
                "Log",
                string.Empty,
                13,
                TextAnchor.UpperLeft,
                RunHudUiFactory.TextMuted);
            var logBody = _gameLogText.rectTransform;
            logBody.offsetMin = new Vector2(0f, 0f);
            logBody.offsetMax = new Vector2(0f, -22f);

            var chatPanel = RunHudUiFactory.CreatePanel(
                _colonistRightRail,
                "ChatPanel",
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RunHudUiFactory.AddLayoutElement(chatPanel.gameObject, 120f);
            RunHudUiFactory.CreateText(
                chatPanel,
                "Title",
                ColonistHudCopy.Chat,
                15,
                TextAnchor.UpperLeft,
                RunHudUiFactory.TextMuted,
                FontStyle.Bold);
            _colonistChatText = RunHudUiFactory.CreateText(
                chatPanel,
                "Placeholder",
                ColonistHudCopy.ChatPlaceholder,
                13,
                TextAnchor.LowerLeft,
                RunHudUiFactory.TextMuted);
            var chatBody = _colonistChatText.rectTransform;
            chatBody.offsetMin = new Vector2(0f, 8f);
            chatBody.offsetMax = new Vector2(0f, -24f);

            var bankStrip = RunHudUiFactory.CreatePanel(
                _colonistRightRail,
                "BankStrip",
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RunHudUiFactory.AddLayoutElement(bankStrip.gameObject, 36f);
            SetBankFlyTarget(bankStrip);
            _bankStripText = RunHudUiFactory.CreateText(
                bankStrip,
                "Bank",
                string.Empty,
                12,
                TextAnchor.MiddleLeft,
                RunHudUiFactory.TextPrimary);
        }

        private void RefreshColonistPresentation(GameState state)
        {
            ApplyColonistHandAndBarInsets();
            PlaceholderHudLayout.SetActivePanelWidth(0f);

            if (_colonistDevCountText != null)
                _colonistDevCountText.text = state.PlayerHand.Count.ToString();

            var longestRoad = RouteCalculator.GetLongestRoadOwner(state.Board);

            if (_colonistHumanPanelText != null)
            {
                int roads = state.Board.CountRoads(PlayerId.Human);
                _colonistHumanPanelText.text =
                    $"{ColonistHudCopy.You} · VP {state.PlayerVictoryPoints}\n" +
                    $"{ColonistHudCopy.Cards} {state.PlayerInventory.Total} · {ColonistHudCopy.DevCards} {state.PlayerHand.Count}\n" +
                    $"{ColonistHudCopy.LongestRoad}: {(longestRoad == PlayerId.Human ? "✓" : "—")} · " +
                    $"{ColonistHudCopy.LargestArmy}: {(state.LargestArmyOwner == PlayerId.Human ? "✓" : "—")}\n" +
                    $"Roads {roads}";
            }

            if (_colonistAiPanelText != null)
            {
                int aiRoads = state.Board.CountRoads(PlayerId.Ai);
                _colonistAiPanelText.text =
                    $"{ColonistHudCopy.Opponent} · VP {state.AiVictoryPoints}\n" +
                    $"{ColonistHudCopy.Cards} ? · {ColonistHudCopy.DevCards} {state.AiHand.Count}\n" +
                    $"{ColonistHudCopy.LongestRoad}: {(longestRoad == PlayerId.Ai ? "✓" : "—")} · " +
                    $"{ColonistHudCopy.LargestArmy}: {(state.LargestArmyOwner == PlayerId.Ai ? "✓" : "—")}\n" +
                    $"Roads {aiRoads}";
            }

            RefreshColonistChrome(state);
        }

        private void RefreshColonistResources(ResourceBundle bundle)
        {
            int i = 0;
            foreach (ResourceType resource in System.Enum.GetValues(typeof(ResourceType)))
            {
                if (i >= _resourceAmountTexts.Count)
                    break;
                _resourceAmountTexts[i].text = bundle[resource].ToString();
                i++;
            }
        }
    }
}
