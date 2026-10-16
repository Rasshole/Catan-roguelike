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
        private Text _colonistTurnTimerText;
        private Text _colonistChatText;
        private GameObject _colonistTurnBanner;
        private ColonistHudBankStrip.Widget _colonistBankStrip;
        private GamePhase _turnCountdownLastPhase;
        private float _turnCountdownSeconds = 90f;

        private void EnsureColonistCanvasBuilt()
        {
            if (_colonistLayoutBuilt && _canvas != null)
                return;

            _canvas = RunHudUiFactory.CreateCanvas(transform);
            BuildColonistBottomHand(_canvas.transform);
            BuildActionBarChrome(_canvas.transform);
            BuildColonistRightRail(_canvas.transform);
            BuildColonistTurnBanner(_canvas.transform);
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
            float handBottom = RunHudLayout.GetHandRowBottomInset();
            float actionBottom = RunHudLayout.GetActionBarBottomInset();
            float left = RunHudLayout.GetContentLeftInset();
            float right = RunHudLayout.GetContentRightInset();
            float handHeight = RunHudLayout.GetBottomHandHeight();

            if (_handPanel != null)
            {
                _handPanel.offsetMin = new Vector2(left, handBottom);
                _handPanel.offsetMax = new Vector2(left + RunHudLayout.ColonistResourceHandWidth, handBottom + handHeight);
            }

            if (_actionBar != null)
            {
                _actionBar.offsetMin = new Vector2(left, actionBottom);
                _actionBar.offsetMax = new Vector2(-right, actionBottom + RunHudLayout.GetBottomActionBarHeight());
            }
        }

        private void BuildColonistBottomHand(Transform root)
        {
            float bottom = RunHudLayout.GetHandRowBottomInset();
            float left = RunHudLayout.GetContentLeftInset();
            float handHeight = RunHudLayout.GetBottomHandHeight();
            _handPanel = RunHudUiFactory.CreatePanel(
                root,
                "ResourceHand",
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(left, bottom),
                new Vector2(left + RunHudLayout.ColonistResourceHandWidth, bottom + handHeight));
            _handPanel.GetComponent<Image>().color = RunHudUiFactory.HandBackdrop;

            var layout = _handPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 6, 6);
            layout.spacing = 8f;
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

            _handRow = _handPanel;
        }

        private GameObject CreateColonistResourceCard(Transform parent, ResourceType resource)
        {
            const float cardW = 72f;
            const float cardH = 96f;

            var cardGo = new GameObject($"ResCard_{resource}");
            cardGo.transform.SetParent(parent, false);
            cardGo.AddComponent<RectTransform>();
            RunHudUiFactory.AddLayoutElement(cardGo, cardH).preferredWidth = cardW;

            var bg = cardGo.AddComponent<Image>();
            bg.color = BoardResourceColors.GetHudSwatch(resource);

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(cardGo.transform, false);
            var iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.55f);
            iconRect.anchorMax = new Vector2(0.5f, 0.55f);
            iconRect.sizeDelta = new Vector2(40f, 40f);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite = ColonistHudResourceSprites.Get(resource);
            iconImg.preserveAspect = true;
            _resourceSwatches.Add(iconImg);

            var badgeGo = new GameObject("Badge");
            badgeGo.transform.SetParent(cardGo.transform, false);
            var badgeRect = badgeGo.AddComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(1f, 1f);
            badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.pivot = new Vector2(1f, 1f);
            badgeRect.anchoredPosition = new Vector2(-4f, -4f);
            badgeRect.sizeDelta = new Vector2(28f, 24f);
            badgeGo.AddComponent<Image>().color = new Color(0.08f, 0.10f, 0.14f, 0.92f);

            var count = RunHudUiFactory.CreateText(
                badgeGo.transform,
                "Count",
                "0",
                16,
                TextAnchor.MiddleCenter,
                RunHudUiFactory.TextPrimary,
                FontStyle.Bold);
            _resourceAmountTexts.Add(count);

            var name = RunHudUiFactory.CreateText(
                cardGo.transform,
                "Name",
                ResourceDisplayNames.GetEnglish(resource),
                11,
                TextAnchor.LowerCenter,
                RunHudUiFactory.TextMuted);
            var nameRect = name.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 0f);
            nameRect.pivot = new Vector2(0.5f, 0f);
            nameRect.anchoredPosition = new Vector2(0f, 6f);
            nameRect.sizeDelta = new Vector2(0f, 18f);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;

            return cardGo;
        }

        private void BuildColonistTurnBanner(Transform root)
        {
            float left = RunHudLayout.GetContentLeftInset();
            float right = RunHudLayout.GetContentRightInset();
            float top = RunHudLayout.Margin;

            var banner = RunHudUiFactory.CreatePanel(
                root,
                "YourTurnBanner",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(-150f, -(top + 48f)),
                new Vector2(150f, -top));
            banner.pivot = new Vector2(0.5f, 1f);
            banner.anchoredPosition = new Vector2((left - right) * 0.5f, -top);
            banner.GetComponent<Image>().color = new Color(0.12f, 0.42f, 0.28f, 0.94f);
            RunHudUiFactory.CreateText(
                banner,
                "Label",
                ColonistHudCopy.YourTurn,
                20,
                TextAnchor.MiddleCenter,
                RunHudUiFactory.TextPrimary,
                FontStyle.Bold);
            _colonistTurnBanner = banner.gameObject;
            _colonistTurnBanner.SetActive(false);
        }

        private void RefreshColonistTurnBanner(GameState state)
        {
            if (_colonistTurnBanner == null)
                return;

            bool show = state.Phase == GamePhase.DayPlayerActions && !state.Winner.HasValue;
            _colonistTurnBanner.SetActive(show);
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
                new Vector2(-margin, -(RunHudLayout.GetBottomHandHeight() + RunHudLayout.GetBottomActionBarHeight() + margin * 2f)));

            var layout = _colonistRightRail.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 8f;
            layout.childForceExpandWidth = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            var humanPanel = BuildColonistPlayerPanel(
                _colonistRightRail,
                "PlayerYou",
                new Color(0.22f, 0.55f, 0.95f, 1f),
                out _colonistHumanPanelText,
                out _colonistTurnTimerText);

            Text unusedTurnTimer;
            var aiPanel = BuildColonistPlayerPanel(
                _colonistRightRail,
                "PlayerAi",
                new Color(0.92f, 0.32f, 0.28f, 1f),
                out _colonistAiPanelText,
                out unusedTurnTimer);

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
            var logScrollGo = new GameObject("LogScroll");
            logScrollGo.transform.SetParent(logPanel, false);
            var logScrollRect = logScrollGo.AddComponent<RectTransform>();
            logScrollRect.anchorMin = Vector2.zero;
            logScrollRect.anchorMax = Vector2.one;
            logScrollRect.offsetMin = new Vector2(0f, 0f);
            logScrollRect.offsetMax = new Vector2(0f, -22f);
            var scroll = logScrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(logScrollGo.transform, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            _gameLogText = RunHudUiFactory.CreateText(
                viewport.transform,
                "Log",
                string.Empty,
                12,
                TextAnchor.UpperLeft,
                RunHudUiFactory.TextMuted);
            _gameLogText.horizontalOverflow = HorizontalWrapMode.Wrap;
            scroll.content = _gameLogText.rectTransform;
            scroll.viewport = viewportRect;

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
            RunHudUiFactory.AddLayoutElement(bankStrip.gameObject, 44f);
            SetBankFlyTarget(bankStrip);
            _colonistBankStrip = ColonistHudBankStrip.Build(bankStrip, 40f);
        }

        private void RefreshColonistPresentation(GameState state)
        {
            ApplyColonistHandAndBarInsets();
            PlaceholderHudLayout.SetActivePanelWidth(0f);

            var longestRoad = RouteCalculator.GetLongestRoadOwner(state.Board);

            if (_colonistHumanPanelText != null)
            {
                int roads = state.Board.CountRoads(PlayerId.Human);
                _colonistHumanPanelText.text =
                    $"{ColonistHudCopy.You}  VP {state.PlayerVictoryPoints}  ·  {ColonistHudCopy.Cards} {state.PlayerInventory.Total}\n" +
                    $"{ColonistHudCopy.DevCards} {state.PlayerHand.Count}  ·  Roads {roads}\n" +
                    $"LR {(longestRoad == PlayerId.Human ? "✓" : "—")}  LA {(state.LargestArmyOwner == PlayerId.Human ? "✓" : "—")}";
            }

            if (_colonistAiPanelText != null)
            {
                int aiRoads = state.Board.CountRoads(PlayerId.Ai);
                _colonistAiPanelText.text =
                    $"{ColonistHudCopy.Opponent}  VP {state.AiVictoryPoints}  ·  {ColonistHudCopy.Cards} ?\n" +
                    $"{ColonistHudCopy.DevCards} {state.AiHand.Count}  ·  Roads {aiRoads}\n" +
                    $"LR {(longestRoad == PlayerId.Ai ? "✓" : "—")}  LA {(state.LargestArmyOwner == PlayerId.Ai ? "✓" : "—")}";
            }

            if (_colonistBankStrip != null)
                ColonistHudBankStrip.RefreshCounts(_colonistBankStrip, state.Bank);

            RefreshColonistTurnTimer(state);
            RefreshColonistChrome(state);
        }

        private static RectTransform BuildColonistPlayerPanel(
            RectTransform parent,
            string name,
            Color avatarColor,
            out Text bodyText,
            out Text turnTimerText)
        {
            var panel = RunHudUiFactory.CreatePanel(
                parent,
                name,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RunHudUiFactory.AddLayoutElement(panel.gameObject, 96f);

            var row = new GameObject("Row");
            row.transform.SetParent(panel, false);
            var rowRect = row.AddComponent<RectTransform>();
            rowRect.anchorMin = Vector2.zero;
            rowRect.anchorMax = Vector2.one;
            rowRect.offsetMin = new Vector2(8f, 8f);
            rowRect.offsetMax = new Vector2(-8f, -8f);
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10f;
            rowLayout.childAlignment = TextAnchor.UpperLeft;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childControlHeight = true;

            var avatarGo = new GameObject("Avatar");
            avatarGo.transform.SetParent(row.transform, false);
            RunHudUiFactory.AddLayoutElement(avatarGo, 32f).preferredWidth = 32f;
            var avatarImg = avatarGo.AddComponent<Image>();
            avatarImg.color = avatarColor;

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(row.transform, false);
            var bodyLayout = bodyGo.AddComponent<LayoutElement>();
            bodyLayout.flexibleWidth = 1f;
            bodyText = RunHudUiFactory.CreateText(
                bodyGo.transform,
                "Text",
                string.Empty,
                12,
                TextAnchor.UpperLeft,
                RunHudUiFactory.TextPrimary);
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;

            turnTimerText = null;
            var timerGo = new GameObject("TurnTimer");
            timerGo.transform.SetParent(row.transform, false);
            RunHudUiFactory.AddLayoutElement(timerGo, 32f).preferredWidth = 52f;
            turnTimerText = RunHudUiFactory.CreateText(
                timerGo.transform,
                "Timer",
                string.Empty,
                14,
                TextAnchor.UpperRight,
                RunHudUiFactory.TextPrimary,
                FontStyle.Bold);
            turnTimerText.alignment = TextAnchor.UpperRight;
            if (name != "PlayerYou")
            {
                timerGo.SetActive(false);
                turnTimerText = null;
            }

            return panel;
        }

        private void RefreshColonistTurnTimer(GameState state)
        {
            if (_colonistTurnTimerText == null)
                return;

            if (_turnCountdownLastPhase != state.Phase)
            {
                if (state.Phase == GamePhase.DayPlayerActions)
                    _turnCountdownSeconds = 90f;
                _turnCountdownLastPhase = state.Phase;
            }

            if (state.Phase != GamePhase.DayPlayerActions || state.Winner.HasValue)
            {
                _colonistTurnTimerText.text = string.Empty;
                return;
            }

            int minutes = Mathf.Max(0, (int)(_turnCountdownSeconds / 60f));
            int seconds = Mathf.Max(0, (int)(_turnCountdownSeconds % 60f));
            _colonistTurnTimerText.text = $"{minutes:00}:{seconds:00}";
        }

        private void TickColonistTurnTimer()
        {
            if (_colonistTurnTimerText == null || _controller == null)
                return;

            var state = _controller.State;
            if (state == null || state.Phase != GamePhase.DayPlayerActions || state.Winner.HasValue)
                return;

            _turnCountdownSeconds = Mathf.Max(0f, _turnCountdownSeconds - Time.deltaTime);
            int minutes = (int)(_turnCountdownSeconds / 60f);
            int seconds = (int)(_turnCountdownSeconds % 60f);
            _colonistTurnTimerText.text = $"{minutes:00}:{seconds:00}";
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
