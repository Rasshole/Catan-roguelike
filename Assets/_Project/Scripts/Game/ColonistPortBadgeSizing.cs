using System.Collections.Generic;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Port badge disc world size from number-token rim geometry.</summary>
    public static class ColonistPortBadgeSizing
    {
        public static float ActiveDiscWorldRadius { get; private set; } =
            HexTileView.TokenRimWorldDiameter * 0.5f * HexTileView.PortBadgeDiameterFraction;

        public static void ResetBoardSession()
        {
        }

        public static void ConfigureFromTiles(IEnumerable<HexTileView> tiles)
        {
            if (tiles == null)
                return;

            foreach (var tile in tiles)
            {
                if (tile == null || tile.Data.IsDesert || !tile.Data.NumberToken.HasValue)
                    continue;

                float rimDiameter = tile.MeasuredTokenRimWorldDiameter;
                if (rimDiameter < 0.01f)
                    rimDiameter = HexTileView.TokenRimWorldDiameter;

                ActiveDiscWorldRadius = rimDiameter * 0.5f * HexTileView.PortBadgeDiameterFraction;
                return;
            }

            ActiveDiscWorldRadius = HexTileView.TokenRimWorldDiameter * 0.5f * HexTileView.PortBadgeDiameterFraction;
        }
    }
}
