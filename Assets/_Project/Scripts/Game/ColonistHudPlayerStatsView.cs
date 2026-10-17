using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Map;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>Icon + count rows for colonist player panels (VP, hand, roads, awards).</summary>
    internal sealed class ColonistHudPlayerStatsView : MonoBehaviour
    {
        private readonly List<(Image icon, Text value)> _rows = new();

        public static ColonistHudPlayerStatsView Attach(Transform statsParent)
        {
            foreach (Transform child in statsParent)
                ColonistSceneDestroy.SafeDestroy(child.gameObject);

            var view = statsParent.gameObject.AddComponent<ColonistHudPlayerStatsView>();
            view.Build();
            return view;
        }

        private void Build()
        {
            var layout = gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 3f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandWidth = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;

            AddRow(ColonistKenneyHudIcons.Victory);
            AddRow(ColonistKenneyHudIcons.Resources);
            AddRow(ColonistKenneyHudIcons.DevCard);
            AddRow(ColonistKenneyHudIcons.Road);
            AddRow(ColonistKenneyHudIcons.Knight);
            AddRow(ColonistKenneyHudIcons.Army);
        }

        private void AddRow(Sprite icon)
        {
            var row = new GameObject("StatRow");
            row.transform.SetParent(transform, false);
            var h = row.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childForceExpandWidth = false;
            h.childControlWidth = true;
            h.childForceExpandHeight = false;
            h.childControlHeight = true;
            RunHudUiFactory.AddLayoutElement(row, 20f);

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(row.transform, false);
            RunHudUiFactory.AddLayoutElement(iconGo, 18f).preferredWidth = 18f;
            var img = iconGo.AddComponent<Image>();
            img.preserveAspect = true;
            img.sprite = icon;
            img.color = Color.white;

            var text = RunHudUiFactory.CreateText(
                row.transform,
                "Value",
                "—",
                13,
                TextAnchor.MiddleLeft,
                RunHudUiFactory.ColonistTextOnPanel);
            var textLayout = text.gameObject.AddComponent<LayoutElement>();
            textLayout.flexibleWidth = 1f;
            _rows.Add((img, text));
        }

        public void RefreshHuman(GameState state, bool hasLongestRoad, bool hasLargestArmy, bool showFish, int fishCount)
        {
            if (_rows.Count < 6 || state == null)
                return;

            int roads = state.Board.CountRoads(PlayerId.Human);
            _rows[0].value.text = state.PlayerVictoryPoints.ToString();
            _rows[1].value.text = state.PlayerInventory.Total.ToString();
            _rows[2].value.text = state.PlayerHand.Count.ToString();
            _rows[3].value.text = showFish ? $"{roads} · Fish {fishCount}" : roads.ToString();
            _rows[4].value.text = hasLongestRoad ? "✓" : "—";
            _rows[5].value.text = hasLargestArmy ? "✓" : "—";
        }

        public void RefreshAi(GameState state, bool hasLongestRoad, bool hasLargestArmy)
        {
            if (_rows.Count < 6 || state == null)
                return;

            int roads = state.Board.CountRoads(PlayerId.Ai);
            _rows[0].value.text = state.AiVictoryPoints.ToString();
            _rows[1].value.text = "—";
            _rows[2].value.text = state.AiHand.Count.ToString();
            _rows[3].value.text = roads.ToString();
            _rows[4].value.text = hasLongestRoad ? "✓" : "—";
            _rows[5].value.text = hasLargestArmy ? "✓" : "—";
        }
    }
}
