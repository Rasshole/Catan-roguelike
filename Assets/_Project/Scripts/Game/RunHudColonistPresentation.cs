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
        private Text _colonistHumanNameText;
        private ColonistHudPlayerStatsView _colonistHumanStats;
        private Text _colonistAiNameText;
        private ColonistHudPlayerStatsView _colonistAiStats;
        private Text _colonistTurnTimerText;
        private Text _colonistChatText;
        private GameObject _colonistTurnBanner;
        private ColonistHudBankStrip.Widget _colonistBankStrip;
        private RectTransform _colonistBankStripRoot;
        private ColonistRoguelikeCardHandView _colonistRoguelikeCardHand;
        private GamePhase _turnCountdownLastPhase;
        private float _turnCountdownSeconds = 90f;

        private void EnsureColonistCanvasBuilt()
        {
            if (_colonistLayoutBuilt && _canvas != null)
                return;

            _canvas = RunHudUiFactory.CreateCanvas(transform);
            BuildActionBarChrome(_canvas.transform);
            BuildColonistBottomHand(_canvas.transform);
            _colonistRoguelikeCardHand = ColonistRoguelikeCardHandView.Build(_canvas.transform);
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
            float deckBottom = RunHudLayout.GetColonistDeckBandBottom();
            float actionBottom = RunHudLayout.GetActionBarBottomInset();
            float left = RunHudLayout.GetContentLeftInset();
            float right = RunHudLayout.GetContentRightInset();

            if (_handPanel != null)
            {
                _handPanel.offsetMin = new Vector2(left, deckBottom);
                _handPanel.offsetMax = new Vector2(
                    left + RunHudLayout.ColonistResourceHandWidth,
                    deckBottom + RunHudLayout.ColonistResourceHandHeight);
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
            ColonistKenneyHudArt.ApplyWoodBarImage(_handPanel.GetComponent<Image>());

            var layout = _handPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 6, 6);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandHeight = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;

            _resourceAmountTexts.Clear();
            _resourceSwatches.Clear();

            foreach (ResourceType resource in ResourceTypes.Productive)
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
            ColonistKenneyHudArt.ApplyColonistResourceCardBackground(bg, BoardResourceColors.GetHudSwatch(resource));

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(cardGo.transform, false);
            var iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.14f, 0.20f);
            iconRect.anchorMax = new Vector2(0.86f, 0.82f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconImg = iconGo.AddComponent<Image>();
            // Sit the glyph below the top-right count badge so the badge never covers it.
            ColonistHudIconGraphic.ApplyCardIcon(iconImg, ColonistHudResourceSprites.Get(resource), fill: 0.60f, centerY: 0.47f);
            _resourceSwatches.Add(iconImg);

            var badgeGo = new GameObject("Badge");
            badgeGo.transform.SetParent(cardGo.transform, false);
            var badgeRect = badgeGo.AddComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(1f, 1f);
            badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.pivot = new Vector2(1f, 1f);
            badgeRect.anchoredPosition = new Vector2(-4f, -4f);
            badgeRect.sizeDelta = new Vector2(28f, 24f);
            ColonistKenneyHudArt.ApplyColonistCountBadge(badgeGo.AddComponent<Image>());

            var count = RunHudUiFactory.CreateText(
                badgeGo.transform,
                "Count",
                "0",
                16,
                TextAnchor.MiddleCenter,
                ColonistUiKit.Cream,
                FontStyle.Bold);
            _resourceAmountTexts.Add(count);

            var name = RunHudUiFactory.CreateText(
                cardGo.transform,
                "Name",
                ResourceDisplayNames.GetEnglish(resource),
                11,
                TextAnchor.LowerCenter,
                ColonistUiKit.Ink,
                FontStyle.Bold);
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
                new Vector2(-160f, -(top + RunHudLayout.ColonistYourTurnBannerHeight)),
                new Vector2(160f, -top));
            banner.pivot = new Vector2(0.5f, 1f);
            banner.anchoredPosition = new Vector2((left - right) * 0.5f, -top);
            ColonistKenneyHudArt.ApplyTurnBannerImage(banner.GetComponent<Image>());
            var bannerLabel = RunHudUiFactory.CreateText(
                banner,
                "Label",
                ColonistHudCopy.YourTurn,
                21,
                TextAnchor.MiddleCenter,
                ColonistUiKit.Cream,
                FontStyle.Bold);
            ColonistUiKit.AddTextShadow(bannerLabel, 1.5f);
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
                new Vector2(-margin, -(RunHudLayout.GetColonistBottomChromeHeight() + margin * 2f)));
            // v132: the rail is one painted card (ornate frame), sections are parchment insets / rules.
            ColonistKenneyHudArt.ApplyOrnatePanelImage(_colonistRightRail.GetComponent<Image>());

            var layout = _colonistRightRail.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 20, 20);
            layout.spacing = 8f;
            layout.childForceExpandWidth = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            BuildColonistPlayerPanel(
                _colonistRightRail,
                "PlayerYou",
                ColonistHudCopy.You,
                new Color(0.22f, 0.55f, 0.95f, 1f),
                out _colonistHumanNameText,
                out _colonistHumanStats,
                out _colonistTurnTimerText);

            Text unusedAiTurnTimer;
            BuildColonistPlayerPanel(
                _colonistRightRail,
                "PlayerAi",
                ColonistHudCopy.Opponent,
                new Color(0.92f, 0.32f, 0.28f, 1f),
                out _colonistAiNameText,
                out _colonistAiStats,
                out unusedAiTurnTimer);

            var logPanel = RunHudUiFactory.CreatePanel(
                _colonistRightRail,
                "GameLogPanel",
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RunHudUiFactory.AddLayoutElement(logPanel.gameObject, 220f, 1f);
            MakeRailSection(logPanel);
            RunHudUiFactory.CreateRailSectionTitle(logPanel, ColonistHudCopy.GameLog);
            var logScrollGo = new GameObject("LogScroll");
            logScrollGo.transform.SetParent(logPanel, false);
            var logScrollRect = logScrollGo.AddComponent<RectTransform>();
            logScrollRect.anchorMin = Vector2.zero;
            logScrollRect.anchorMax = Vector2.one;
            logScrollRect.offsetMin = new Vector2(8f, 6f);
            logScrollRect.offsetMax = new Vector2(-8f, -34f);
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
                RunHudUiFactory.ColonistTextOnPanel);
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
            MakeRailSection(chatPanel);
            RunHudUiFactory.CreateRailSectionTitle(chatPanel, ColonistHudCopy.Chat);
            _colonistChatText = RunHudUiFactory.CreateText(
                chatPanel,
                "Placeholder",
                ColonistHudCopy.ChatPlaceholder,
                13,
                TextAnchor.UpperLeft,
                RunHudUiFactory.ColonistTextMutedOnPanel);
            var chatBody = _colonistChatText.rectTransform;
            chatBody.offsetMin = new Vector2(10f, 10f);
            chatBody.offsetMax = new Vector2(-10f, -36f);

            var bankStrip = RunHudUiFactory.CreatePanel(
                _colonistRightRail,
                "BankStrip",
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RunHudUiFactory.AddLayoutElement(bankStrip.gameObject, 64f);
            var bankImage = bankStrip.GetComponent<Image>();
            bankImage.color = new Color(1f, 1f, 1f, 0f);
            bankImage.raycastTarget = false;
            SetBankFlyTarget(bankStrip);
            _colonistBankStripRoot = bankStrip;
            _colonistBankStrip = ColonistHudBankStrip.Build(bankStrip);
        }

        /// <summary>Log / chat sections sit straight on the rail parchment under a thin brown rule.</summary>
        private static void MakeRailSection(RectTransform section)
        {
            var image = section.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = null;
                image.color = new Color(1f, 1f, 1f, 0f);
                image.raycastTarget = false;
            }

            var rule = ColonistUiKit.CreateRule(section);
            var rect = rule.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(4f, -2f);
            rect.offsetMax = new Vector2(-4f, 0f);
        }

        private void RefreshColonistPresentation(GameState state)
        {
            ApplyColonistHandAndBarInsets();
            PlaceholderHudLayout.SetActivePanelWidth(0f);

            var longestRoad = RouteCalculator.GetLongestRoadOwner(state.Board);

            if (_colonistAiNameText != null)
                _colonistAiNameText.text = ColonistHudCopy.Opponent;

            _colonistHumanStats?.RefreshHuman(
                state,
                longestRoad == PlayerId.Human,
                state.LargestArmyOwner == PlayerId.Human,
                state.FishermenOfCatanEnabled,
                state.PlayerFish);
            _colonistAiStats?.RefreshAi(
                state,
                longestRoad == PlayerId.Ai,
                state.LargestArmyOwner == PlayerId.Ai);

            if (_colonistBankStrip != null)
                ColonistHudBankStrip.RefreshCounts(_colonistBankStrip, state.Bank);

            RefreshColonistTurnTimer(state);
            RefreshColonistChrome(state);
            _colonistRoguelikeCardHand?.Refresh(state, OnColonistRoguelikeCardPlayRequested);
        }

        private void OnColonistRoguelikeCardPlayRequested(int handIndex)
        {
            _selectedCardIndex = handIndex;
            if (_controller.State.Phase == GamePhase.NightPlayCard)
            {
                PlaySelectedCard();
                return;
            }

            // TODO: support playing roguelike cards outside NightPlayCard when day rules allow it.
        }

        public ColonistRoguelikeCardHandView.LayoutProbe GetRoguelikeCardHandLayoutForTests() =>
            _colonistRoguelikeCardHand != null
                ? _colonistRoguelikeCardHand.GatherLayoutProbe(_actionBar)
                : default;

        public ColonistRoguelikeCardHandView GetRoguelikeCardHandViewForTests() => _colonistRoguelikeCardHand;

        private static RectTransform BuildColonistPlayerPanel(
            RectTransform parent,
            string panelName,
            string displayName,
            Color avatarColor,
            out Text nameText,
            out ColonistHudPlayerStatsView statsView,
            out Text turnTimerText)
        {
            var panel = RunHudUiFactory.CreatePanel(
                parent,
                panelName,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RunHudUiFactory.AddLayoutElement(panel.gameObject, 88f);

            var content = new GameObject("Content");
            content.transform.SetParent(panel, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = new Vector2(8f, 6f);
            contentRect.offsetMax = new Vector2(-8f, -6f);
            var contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 4f;
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childControlHeight = true;

            var nameRow = new GameObject("NameRow");
            nameRow.transform.SetParent(content.transform, false);
            var nameRowLayout = nameRow.AddComponent<HorizontalLayoutGroup>();
            nameRowLayout.spacing = 8f;
            nameRowLayout.childAlignment = TextAnchor.MiddleLeft;
            nameRowLayout.childForceExpandHeight = false;
            nameRowLayout.childControlHeight = true;
            nameRowLayout.childForceExpandWidth = false;
            nameRowLayout.childControlWidth = true;
            RunHudUiFactory.AddLayoutElement(nameRow, 22f);

            var swatchGo = new GameObject("Swatch");
            swatchGo.transform.SetParent(nameRow.transform, false);
            RunHudUiFactory.AddLayoutElement(swatchGo, 18f).preferredWidth = 18f;
            var swatch = swatchGo.AddComponent<Image>();
            if (!ColonistUiKit.Apply(swatch, UiKitPiece.Badge, 0.5f, Color.Lerp(avatarColor, Color.white, 0.25f)))
                swatch.color = avatarColor;

            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(nameRow.transform, false);
            var nameLayout = RunHudUiFactory.AddLayoutElement(nameGo, 22f, 1f);
            nameLayout.minWidth = 64f;
            nameText = RunHudUiFactory.CreateText(
                nameGo.transform,
                "Label",
                displayName,
                14,
                TextAnchor.MiddleLeft,
                RunHudUiFactory.ColonistTextOnPanel,
                FontStyle.Bold);
            nameText.horizontalOverflow = HorizontalWrapMode.Wrap;

            turnTimerText = null;
            var timerGo = new GameObject("TurnTimer");
            timerGo.transform.SetParent(nameRow.transform, false);
            RunHudUiFactory.AddLayoutElement(timerGo, 22f).preferredWidth = 52f;
            turnTimerText = RunHudUiFactory.CreateText(
                timerGo.transform,
                "Timer",
                string.Empty,
                13,
                TextAnchor.MiddleRight,
                RunHudUiFactory.ColonistTextOnPanel,
                FontStyle.Bold);
            turnTimerText.alignment = TextAnchor.MiddleRight;
            if (panelName != "PlayerYou")
            {
                timerGo.SetActive(false);
                turnTimerText = null;
            }

            var statsGo = new GameObject("Stats");
            statsGo.transform.SetParent(content.transform, false);
            var statsLayout = statsGo.AddComponent<LayoutElement>();
            statsLayout.flexibleHeight = 1f;
            statsLayout.minHeight = 44f;
            statsView = ColonistHudPlayerStatsView.Attach(statsGo.transform);

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
            foreach (ResourceType resource in ResourceTypes.Productive)
            {
                if (i >= _resourceAmountTexts.Count)
                    break;
                _resourceAmountTexts[i].text = bundle[resource].ToString();
                i++;
            }
        }
    }
}
