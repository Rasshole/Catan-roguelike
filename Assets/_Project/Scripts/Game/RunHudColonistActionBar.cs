using CatanRoguelike.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    public sealed partial class RunHudController
    {
        private RectTransform _actionBarTradeRoot;
        private Image[] _colonistDiePipsA;
        private Image[] _colonistDiePipsB;

        public struct ColonistActionBarLayoutProbe
        {
            public bool EndTurnActiveInHierarchy;
            public Vector2 EndTurnSize;
            public bool RollActiveInHierarchy;
            public Vector2 RollSize;
            public bool RoadActiveInHierarchy;
            public Vector2 RoadSize;
            public bool SettlementActiveInHierarchy;
            public Vector2 SettlementSize;
            public bool CityActiveInHierarchy;
            public Vector2 CitySize;
            public bool DevActiveInHierarchy;
            public Vector2 DevSize;
            public string SettlementButtonLabel;

            public void LogToConsole()
            {
                Debug.Log(
                    "ColonistActionBarLayout: " +
                    $"endTurn active={EndTurnActiveInHierarchy} size={EndTurnSize} " +
                    $"roll active={RollActiveInHierarchy} size={RollSize} " +
                    $"road active={RoadActiveInHierarchy} size={RoadSize} " +
                    $"settlement active={SettlementActiveInHierarchy} size={SettlementSize} " +
                    $"city active={CityActiveInHierarchy} size={CitySize} " +
                    $"dev active={DevActiveInHierarchy} size={DevSize}");
            }
        }

        public ColonistActionBarLayoutProbe GetColonistActionBarLayoutForTests() =>
            ColonistPresentation.IsActive ? GatherColonistActionBarLayout() : default;

        private ColonistActionBarLayoutProbe GatherColonistActionBarLayout()
        {
            var probe = new ColonistActionBarLayoutProbe();
            SampleButton(_endTurnPinned, ref probe.EndTurnActiveInHierarchy, ref probe.EndTurnSize);
            SampleButton(_rollPinned, ref probe.RollActiveInHierarchy, ref probe.RollSize);
            SampleButton(_buildRoadButton, ref probe.RoadActiveInHierarchy, ref probe.RoadSize);
            SampleButton(_buildSettlementButton, ref probe.SettlementActiveInHierarchy, ref probe.SettlementSize);
            SampleButton(_buildCityButton, ref probe.CityActiveInHierarchy, ref probe.CitySize);
            SampleButton(_buildDevButton, ref probe.DevActiveInHierarchy, ref probe.DevSize);
            probe.SettlementButtonLabel = GetBuildButtonTitle(_buildSettlementButton);
            return probe;
        }

        public int GetColonistBankCardCountForTests() =>
            _colonistBankStrip != null ? ColonistHudBankStrip.ExpectedCardCount : 0;

        private static string GetBuildButtonTitle(Button button)
        {
            if (button == null)
                return string.Empty;

            var titleRow = button.transform.Find("TitleRow");
            if (titleRow == null)
                return string.Empty;

            var title = titleRow.Find("Title");
            if (title == null)
                return string.Empty;

            var text = title.GetComponent<Text>();
            return text != null ? text.text : string.Empty;
        }

        private static void SampleButton(Button button, ref bool activeInHierarchy, ref Vector2 size)
        {
            if (button == null)
                return;

            activeInHierarchy = button.gameObject.activeInHierarchy;
            var rect = button.transform as RectTransform;
            if (rect != null)
                size = rect.rect.size;
        }

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

        private void BuildColonistDieFace(Transform parent, string name, out Image[] pipImages)
        {
            var dieGo = new GameObject(name);
            dieGo.layer = parent.gameObject.layer;
            dieGo.transform.SetParent(parent, false);
            RunHudUiFactory.AddLayoutElement(dieGo, 48f).preferredWidth = 48f;
            dieGo.AddComponent<Image>().color = new Color(0.96f, 0.94f, 0.88f, 1f);
            ColonistDiceFaceUi.Build(dieGo.transform, out pipImages);
        }

        private void RefreshColonistDiceFaces(GameState state)
        {
            if (_colonistDiePipsA == null || _colonistDiePipsB == null)
                return;

            var dice = state.IsNightPhase ? state.TomorrowDiceRolls : state.TodayDiceRolls;
            if (dice == null || dice.Count == 0)
            {
                ColonistDiceFaceUi.SetValue(_colonistDiePipsA, 0);
                ColonistDiceFaceUi.SetValue(_colonistDiePipsB, 0);
                return;
            }

            int a = dice[0];
            int b = dice.Count >= 2 ? dice[1] : 0;
            ColonistDiceFaceUi.SetValue(_colonistDiePipsA, a);
            ColonistDiceFaceUi.SetValue(_colonistDiePipsB, b);
        }

        private void SetColonistDieValues(int a, int b)
        {
            if (_colonistDiePipsA != null)
                ColonistDiceFaceUi.SetValue(_colonistDiePipsA, a);
            if (_colonistDiePipsB != null)
                ColonistDiceFaceUi.SetValue(_colonistDiePipsB, b);
        }
    }
}
