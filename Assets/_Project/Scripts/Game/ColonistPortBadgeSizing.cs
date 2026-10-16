using System.Collections.Generic;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>World badge disc size derived from measured number-token chip geometry.</summary>
    public static class ColonistPortBadgeSizing
    {
        public static float ActiveDiscWorldRadius { get; private set; } =
            HexTileView.TokenWorldFaceDiameter * 0.5f * ColonistBoardScreenMetrics.PortBadgeTargetTokenScreenDiameterRatio;

        private static bool _refinedScreenScaleThisBoard;

        public static void ResetBoardSession() => _refinedScreenScaleThisBoard = false;

        public static void ConfigureFromTiles(IEnumerable<HexTileView> tiles)
        {
            if (tiles == null)
                return;

            foreach (var tile in tiles)
            {
                if (tile == null || tile.Data.IsDesert || !tile.Data.NumberToken.HasValue)
                    continue;

                float chipDiameter = tile.ChipWorldDiameter;
                if (chipDiameter < 0.01f)
                    chipDiameter = HexTileView.TokenWorldFaceDiameter;

                ActiveDiscWorldRadius = chipDiameter * 0.5f
                    * ColonistBoardScreenMetrics.PortBadgeTargetTokenScreenDiameterRatio;
                return;
            }

            ActiveDiscWorldRadius = HexTileView.TokenWorldFaceDiameter * 0.5f
                * ColonistBoardScreenMetrics.PortBadgeTargetTokenScreenDiameterRatio;
        }

        public static void RefineDiscScreenScaleToTokens(Camera camera, BoardView boardView)
        {
            if (_refinedScreenScaleThisBoard || camera == null || boardView == null)
                return;

            HexTileView referenceToken = null;
            foreach (var tile in Object.FindObjectsByType<HexTileView>(FindObjectsSortMode.None))
            {
                if (tile.Data.IsDesert || !tile.Data.NumberToken.HasValue || tile.TokenFaceRenderer == null)
                    continue;
                referenceToken = tile;
                break;
            }

            if (referenceToken == null)
                return;

            float tokenScreen = ColonistBoardScreenMetrics.MeasureScreenDiameter(referenceToken.TokenFaceRenderer, camera);
            if (tokenScreen < 8f)
                return;

            float targetScreen = tokenScreen * ColonistBoardScreenMetrics.PortBadgeTargetTokenScreenDiameterRatio;
            bool adjusted = false;
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer == null || renderer.gameObject.name != "BadgeDisc")
                    continue;

                float discScreen = ColonistBoardScreenMetrics.MeasureScreenDiameter(renderer, camera);
                if (discScreen < 1f)
                    continue;

                float scale = targetScreen / discScreen;
                if (Mathf.Abs(scale - 1f) < 0.04f)
                    continue;

                var t = renderer.transform;
                var s = t.localScale;
                t.localScale = new Vector3(s.x * scale, s.y, s.z * scale);
                adjusted = true;
            }

            if (adjusted)
                _refinedScreenScaleThisBoard = true;
        }
    }
}
