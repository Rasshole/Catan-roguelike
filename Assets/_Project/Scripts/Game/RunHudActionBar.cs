using CatanRoguelike.Core;
using CatanRoguelike.Core.Turn;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    public sealed partial class RunHudController
    {
        private RectTransform _actionBarBuildRoot;
        private RectTransform _actionBarCenterRoot;
        private RectTransform _actionBarEndRoot;
        private Text _diceWidgetValue;
        private Image _rollButtonGlow;

        private void BuildActionBarChrome(Transform root)
        {
            float margin = RunHudLayout.Margin;
            float bottom = RunHudLayout.GetActionBarBottomInset();
            float barHeight = RunHudLayout.GetBottomActionBarHeight();
            _actionBar = RunHudUiFactory.CreatePanel(
                root,
                "ActionBar",
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(RunHudLayout.GetContentLeftInset(), bottom),
                new Vector2(-RunHudLayout.GetContentRightInset(), bottom + barHeight));
            _actionBar.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.14f, 0.92f);

            var rootLayout = _actionBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            rootLayout.spacing = 10f;
            rootLayout.padding = new RectOffset(10, 10, 8, 8);
            rootLayout.childAlignment = TextAnchor.MiddleLeft;
            rootLayout.childForceExpandWidth = false;
            rootLayout.childForceExpandHeight = true;
            rootLayout.childControlHeight = true;

            _actionBarBuildRoot = CreateActionBarSlot("BuildSlot", flexibleWidth: 1f);
            _actionBarCenterRoot = CreateActionBarSlot("DiceRollSlot", flexibleWidth: 0f, minWidth: 220f);
            float endTurnMinWidth = ColonistPresentation.IsActive ? 200f : 148f;
            _actionBarEndRoot = CreateActionBarSlot("EndTurnSlot", flexibleWidth: 0f, minWidth: endTurnMinWidth);

            BuildDiceAndRollCluster(_actionBarCenterRoot);
            BuildEndTurnButton(_actionBarEndRoot);

            if (ColonistPresentation.IsActive && _openTradeDialogButton == null)
            {
                var tradeBtn = RunHudUiFactory.CreateButton(_actionBarBuildRoot, ColonistHudCopy.Trade, RunHudUiFactory.ButtonSecondary);
                RunHudUiFactory.AddLayoutElement(tradeBtn.gameObject, 48f).preferredWidth = 96f;
                tradeBtn.onClick.AddListener(() => SetTradeDialogVisible(true));
                _openTradeDialogButton = tradeBtn;
            }
        }

        private RectTransform CreateActionBarSlot(string name, float flexibleWidth, float minWidth = 0f)
        {
            var go = new GameObject(name);
            go.layer = _actionBar.gameObject.layer;
            go.transform.SetParent(_actionBar, false);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childControlHeight = true;
            var element = RunHudUiFactory.AddLayoutElement(go, RunHudLayout.GetBottomActionBarHeight() - 16f, flexibleWidth);
            if (minWidth > 0f)
                element.minWidth = minWidth;
            return go.GetComponent<RectTransform>();
        }

        private void BuildDiceAndRollCluster(RectTransform parent)
        {
            var diceGo = new GameObject("DiceWidget");
            diceGo.layer = parent.gameObject.layer;
            diceGo.transform.SetParent(parent, false);
            var diceRect = diceGo.AddComponent<RectTransform>();
            diceRect.sizeDelta = new Vector2(88f, 48f);
            RunHudUiFactory.AddLayoutElement(diceGo, 48f).preferredWidth = 88f;
            var diceBg = diceGo.AddComponent<Image>();
            diceBg.color = new Color(0.12f, 0.16f, 0.22f, 1f);

            _diceWidgetValue = RunHudUiFactory.CreateText(
                diceGo.transform,
                "Dice",
                "—",
                28,
                TextAnchor.MiddleCenter,
                RunHudUiFactory.TextPrimary,
                FontStyle.Bold);

            _rollPinned = RunHudUiFactory.CreateButton(parent, ColonistHudCopy.Roll, RunHudUiFactory.ButtonFill);
            RunHudUiFactory.AddLayoutElement(_rollPinned.gameObject, 48f).preferredWidth = 96f;
            _rollPinned.onClick.AddListener(() => _feel?.RequestMorningRoll());
            _rollButtonGlow = _rollPinned.GetComponent<Image>();
        }

        private void BuildEndTurnButton(RectTransform parent)
        {
            _endTurnPinned = RunHudUiFactory.CreateButton(parent, ColonistHudCopy.EndTurn, new Color(0.18f, 0.55f, 0.32f, 1f));
            float endHeight = ColonistPresentation.IsActive ? 60f : 52f;
            float endWidth = ColonistPresentation.IsActive ? 196f : 140f;
            RunHudUiFactory.AddLayoutElement(_endTurnPinned.gameObject, endHeight).preferredWidth = endWidth;
            var label = _endTurnPinned.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.fontSize = ColonistPresentation.IsActive ? 22 : 18;
                label.fontStyle = ColonistPresentation.IsActive ? FontStyle.Bold : FontStyle.Normal;
            }
            _endTurnPinned.onClick.AddListener(() =>
            {
                if (_controller != null)
                    _controller.EndPlayerDay();
            });
        }

        private void RefreshDiceWidget(GameState state)
        {
            if (_diceWidgetValue == null)
                return;

            var dice = state.IsNightPhase ? state.TomorrowDiceRolls : state.TodayDiceRolls;
            if (dice != null && dice.Count >= 2)
                _diceWidgetValue.text = $"{dice[0]}+{dice[1]}";
            else if (dice != null && dice.Count == 1)
                _diceWidgetValue.text = dice[0].ToString();
            else
                _diceWidgetValue.text = "—";
        }

        public void SetRollPromptActive(bool active)
        {
            if (_rollButtonGlow != null)
            {
                var baseColor = RunHudUiFactory.ButtonFill;
                _rollButtonGlow.color = active
                    ? Color.Lerp(baseColor, new Color(1f, 0.9f, 0.35f, 1f), 0.45f)
                    : baseColor;
            }
        }
    }
}
