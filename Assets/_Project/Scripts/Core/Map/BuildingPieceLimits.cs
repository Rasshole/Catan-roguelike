namespace CatanRoguelike.Core.Map
{
    /// <summary>Classic Catan piece bank limits per player.</summary>
    public static class BuildingPieceLimits
    {
        public const int MaxRoads = 15;
        public const int MaxSettlements = 5;
        public const int MaxCities = 4;

        public static int GetRoadsRemaining(BoardState board, PlayerId player) =>
            MaxRoads - board.CountRoads(player);

        public static int GetSettlementsRemaining(BoardState board, PlayerId player) =>
            MaxSettlements - board.CountBuildings(player, BuildingType.Settlement);

        public static int GetCitiesRemaining(BoardState board, PlayerId player) =>
            MaxCities - board.CountBuildings(player, BuildingType.City);

        public static bool CanPlaceRoad(BoardState board, PlayerId player) =>
            GetRoadsRemaining(board, player) > 0;

        public static bool CanPlaceSettlement(BoardState board, PlayerId player) =>
            GetSettlementsRemaining(board, player) > 0;

        public static bool CanPlaceCity(BoardState board, PlayerId player) =>
            GetCitiesRemaining(board, player) > 0
            && board.CountBuildings(player, BuildingType.Settlement) > 0;
    }
}
