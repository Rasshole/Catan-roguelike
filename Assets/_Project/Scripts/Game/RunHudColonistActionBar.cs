using CatanRoguelike.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    public sealed partial class RunHudController
    {
        private RectTransform _actionBarTradeRoot;
        private Image _colonistDieFaceA;
        private Image _colonistDieFaceB;
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
            probe.SettlementButtonLabel = ColonistActionBarLabels.SettlementTitle;
            if (string.IsNullOrEmpty(probe.SettlementButtonLabel))
                probe.SettlementButtonLabel = ColonistBuildButtonLabel.GetTitle(_buildSettlementButton);
            return probe;
        }

        public static string DescribeSettlementButtonTexts(Button button) =>
            ColonistBuildButtonLabel.DescribeAllTexts(button);

        public string DescribeSettlementButtonTextsForTests() =>
            DescribeSettlementButtonTexts(_buildSettlementButton);

        public int GetColonistBankCardCountForTests() =>
            _colonistBankStrip != null ? ColonistHudBankStrip.ExpectedCardCount : 0;

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

        private void BuildColonistDieFace(Transform parent, string name, out Image dieFace, out Image[] pipImages)
        {
            var dieGo = new GameObject(name);
            dieGo.layer = parent.gameObject.layer;
            dieGo.transform.SetParent(parent, false);
            RunHudUiFactory.AddLayoutElement(dieGo, 48f).preferredWidth = 48f;
            dieFace = dieGo.AddComponent<Image>();
            dieFace.color = new Color(0.96f, 0.94f, 0.88f, 1f);
            ColonistDiceFaceUi.Build(dieGo.transform, out pipImages);
        }

        private void RefreshColonistDiceFaces(GameState state)
        {
            if (_colonistDiePipsA == null || _colonistDiePipsB == null)
                return;

            var dice = state.IsNightPhase ? state.TomorrowDiceRolls : state.TodayDiceRolls;
            if (dice == null || dice.Count == 0)
            {
                ColonistKenneyDiceUi.ApplyFace(_colonistDieFaceA, _colonistDiePipsA, 0);
                ColonistKenneyDiceUi.ApplyFace(_colonistDieFaceB, _colonistDiePipsB, 0);
                return;
            }

            int a = dice[0];
            int b = dice.Count >= 2 ? dice[1] : 0;
            ColonistKenneyDiceUi.ApplyFace(_colonistDieFaceA, _colonistDiePipsA, a);
            ColonistKenneyDiceUi.ApplyFace(_colonistDieFaceB, _colonistDiePipsB, b);
        }

        private void SetColonistDieValues(int a, int b)
        {
            ColonistKenneyDiceUi.ApplyFace(_colonistDieFaceA, _colonistDiePipsA, a);
            ColonistKenneyDiceUi.ApplyFace(_colonistDieFaceB, _colonistDiePipsB, b);
        }
    }
}
