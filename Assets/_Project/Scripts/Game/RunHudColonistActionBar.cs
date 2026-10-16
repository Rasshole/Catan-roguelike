using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    public sealed partial class RunHudController
    {
        private RectTransform _actionBarTradeRoot;
        private Text _colonistDieFaceA;
        private Text _colonistDieFaceB;

        private void EnsureColonistTradeButton()
        {
            if (!ColonistPresentation.IsActive || _actionBarTradeRoot == null)
                return;

            if (_openTradeDialogButton != null)
                return;

            var tradeBtn = RunHudUiFactory.CreateButton(
                _actionBarTradeRoot,
                ColonistHudCopy.Trade,
                RunHudUiFactory.ButtonSecondary);
            RunHudUiFactory.AddLayoutElement(tradeBtn.gameObject, 48f).preferredWidth = 88f;
            tradeBtn.onClick.AddListener(() => SetTradeDialogVisible(true));
            _openTradeDialogButton = tradeBtn;
        }

        private void BuildColonistDieFace(Transform parent, string name, out Text faceLabel)
        {
            var dieGo = new GameObject(name);
            dieGo.layer = parent.gameObject.layer;
            dieGo.transform.SetParent(parent, false);
            RunHudUiFactory.AddLayoutElement(dieGo, 48f).preferredWidth = 48f;
            var bg = dieGo.AddComponent<Image>();
            bg.color = new Color(0.96f, 0.94f, 0.88f, 1f);

            faceLabel = RunHudUiFactory.CreateText(
                dieGo.transform,
                "Face",
                "·",
                22,
                TextAnchor.MiddleCenter,
                new Color(0.12f, 0.14f, 0.18f, 1f),
                FontStyle.Bold);
        }

        private void RefreshColonistDiceFaces(GameState state)
        {
            if (_colonistDieFaceA == null || _colonistDieFaceB == null)
                return;

            var dice = state.IsNightPhase ? state.TomorrowDiceRolls : state.TodayDiceRolls;
            if (dice == null || dice.Count == 0)
            {
                _colonistDieFaceA.text = "·";
                _colonistDieFaceB.text = "·";
                return;
            }

            if (dice.Count >= 2)
            {
                _colonistDieFaceA.text = dice[0].ToString();
                _colonistDieFaceB.text = dice[1].ToString();
            }
            else
            {
                _colonistDieFaceA.text = dice[0].ToString();
                _colonistDieFaceB.text = "·";
            }
        }
    }
}
