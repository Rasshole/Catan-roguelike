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

        // Six stacked rows overflowed the player panel and pushed the name out of view, and the
        // white Kenney glyphs vanished on the parchment. A 2x3 grid of tinted icons keeps every
        // stat visible inside the panel.
        private void Build()
        {
            var layout = gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandWidth = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;

            var row0 = AddRow();
            AddCell(row0, ColonistKenneyHudIcons.Victory);
            AddCell(row0, ColonistKenneyHudIcons.Resources);
            AddCell(row0, ColonistKenneyHudIcons.DevCard);
            var row1 = AddRow();
            AddCell(row1, ColonistKenneyHudIcons.Road);
            AddCell(row1, ColonistKenneyHudIcons.Knight);
            AddCell(row1, ColonistKenneyHudIcons.Army);
        }

        private Transform AddRow()
        {
            var row = new GameObject("StatRow");
            row.transform.SetParent(transform, false);
            var h = row.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childForceExpandWidth = false;
            h.childControlWidth = true;
            h.childForceExpandHeight = false;
            h.childControlHeight = true;
            RunHudUiFactory.AddLayoutElement(row, 24f);
            return row.transform;
        }

        private void AddCell(Transform row, Sprite icon)
        {
            var cell = new GameObject("StatCell");
            cell.transform.SetParent(row, false);
            var h = cell.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 4f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childForceExpandWidth = false;
            h.childControlWidth = true;
            h.childForceExpandHeight = false;
            h.childControlHeight = true;
            var cellLayout = RunHudUiFactory.AddLayoutElement(cell, 24f);
            cellLayout.minWidth = 64f;

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(cell.transform, false);
            var iconLayout = RunHudUiFactory.AddLayoutElement(iconGo, 22f);
            iconLayout.preferredWidth = 22f;
            iconLayout.minWidth = 22f;
            var img = iconGo.AddComponent<Image>();
            img.preserveAspect = true;
            img.sprite = icon;
            img.color = RunHudUiFactory.ColonistTextOnPanel;

            var text = RunHudUiFactory.CreateText(
                cell.transform,
                "Value",
                "\u2014",
                15,
                TextAnchor.MiddleLeft,
                RunHudUiFactory.ColonistTextOnPanel);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var textLayout = text.gameObject.AddComponent<LayoutElement>();
            textLayout.minWidth = 26f;
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
