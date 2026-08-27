using CatanRoguelike.Core;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;

namespace CatanRoguelike.Core.Events
{
    /// <summary>
    /// Pure helpers for which hex tiles should show a night-event board overlay.
    /// Game layer applies color/marker; Core stays testable without Unity.
    /// </summary>
    public static class EventBoardVisual
    {
        public static bool TryGetOverlay(GameState state, HexCoord coord, out EventTileOverlayKind overlay)
        {
            overlay = EventTileOverlayKind.None;

            if (state.ActiveEvent == EventId.None)
                return false;

            if (!state.Board.TryGetTile(coord, out var tile))
                return false;

            switch (state.ActiveEvent)
            {
                case EventId.Storm:
                    if (state.EventStormTile == coord)
                    {
                        overlay = EventTileOverlayKind.Storm;
                        return true;
                    }
                    return false;

                case EventId.Famine:
                    if (tile.Resource == ResourceType.Wheat)
                    {
                        overlay = EventTileOverlayKind.Famine;
                        return true;
                    }
                    return false;

                case EventId.GoldRush:
                    if (tile.Resource == ResourceType.Stone)
                    {
                        overlay = EventTileOverlayKind.GoldRush;
                        return true;
                    }
                    return false;

                case EventId.GoodHarvest:
                    overlay = EventTileOverlayKind.GoodHarvest;
                    return true;

                case EventId.MarketDay:
                case EventId.BanditRaid:
                case EventId.ResourceLevy:
                    return false;

                case EventId.PortBlockade:
                    if (!state.EventBlockedPortVertex.HasValue)
                        return false;

                    foreach (var hex in VertexGraph.GetHexesForVertex(state.EventBlockedPortVertex.Value))
                    {
                        if (!hex.Equals(coord)) continue;
                        if (!tile.IsCoastal) continue;
                        overlay = EventTileOverlayKind.PortBlockade;
                        return true;
                    }
                    return false;

                default:
                    return false;
            }
        }
    }
}
