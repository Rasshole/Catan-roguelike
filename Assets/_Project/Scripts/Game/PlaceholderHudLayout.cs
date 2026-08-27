using CatanRoguelike.Core.Turn;
using UnityEngine;

namespace CatanRoguelike.Game
{
    public static class PlaceholderHudLayout
    {
        public const float Margin = 10f;
        public const float PreGamePanelWidth = 400f;
        public const float InRunPanelWidth = 260f;
        public const float MaxInRunPanelHeight = 580f;

        public static float LastPanelWidth { get; private set; } = InRunPanelWidth;

        public static bool IsPreGamePhase(GamePhase phase)
        {
            return phase == GamePhase.RunSelectMap
                || phase == GamePhase.RunSelectLeader
                || phase == GamePhase.RunSelectDraft;
        }

        public static float GetPanelWidth(GamePhase phase)
        {
            return IsPreGamePhase(phase) ? PreGamePanelWidth : InRunPanelWidth;
        }

        public static float GetPanelHeight(GamePhase phase, int screenHeight)
        {
            float available = screenHeight - Margin * 2f;
            if (IsPreGamePhase(phase))
                return available;

            return Mathf.Min(available, MaxInRunPanelHeight);
        }

        public static Rect GetPanelRect(GamePhase phase, int screenWidth, int screenHeight)
        {
            return new Rect(
                Margin,
                Margin,
                GetPanelWidth(phase),
                GetPanelHeight(phase, screenHeight));
        }

        public static void SetActivePanelWidth(float width)
        {
            LastPanelWidth = width;
        }

        /// <summary>
        /// Horizontal screen offset (pixels) from screen center where the board origin should appear.
        /// </summary>
        public static float GetBoardScreenOffsetX(float panelWidth, int screenWidth)
        {
            float hudRight = Margin + panelWidth + Margin;
            float unobstructedCenter = hudRight + (screenWidth - hudRight) * 0.5f;
            return unobstructedCenter - screenWidth * 0.5f;
        }
    }
}
