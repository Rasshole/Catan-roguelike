using System.Collections.Generic;
using CatanRoguelike.Core.Hex;

namespace CatanRoguelike.Core.Data
{
    public static class MapPresets
    {
        /// <summary>7-hex flower layout with all 5 resources + one duplicate for 7 tiles.</summary>
        public static IReadOnlyList<(HexCoord coord, ResourceType resource, bool coastal)> SmallSevenHex()
        {
            return new List<(HexCoord, ResourceType, bool)>
            {
                (new HexCoord(0, 0), ResourceType.Wood, false),
                (new HexCoord(1, 0), ResourceType.Brick, true),
                (new HexCoord(1, -1), ResourceType.Wheat, false),
                (new HexCoord(0, -1), ResourceType.Sheep, true),
                (new HexCoord(-1, 0), ResourceType.Stone, false),
                (new HexCoord(-1, 1), ResourceType.Wood, true),
                (new HexCoord(0, 1), ResourceType.Wheat, false),
            };
        }

        /// <summary>13-hex layout (radius 2) with all 5 resources distributed.</summary>
        public static IReadOnlyList<(HexCoord coord, ResourceType resource, bool coastal)> MediumThirteenHex()
        {
            var resources = new[]
            {
                ResourceType.Wood, ResourceType.Brick, ResourceType.Wheat,
                ResourceType.Sheep, ResourceType.Stone,
                ResourceType.Wood, ResourceType.Brick, ResourceType.Wheat,
                ResourceType.Sheep, ResourceType.Stone,
                ResourceType.Wheat, ResourceType.Sheep, ResourceType.Stone
            };

            var result = new List<(HexCoord, ResourceType, bool)>();
            int i = 0;
            foreach (var coord in HexMath.Spiral(new HexCoord(0, 0), 2))
            {
                bool coastal = coord.Q == 0 || coord.R == 0 || coord.S == 0
                    || System.Math.Abs(coord.Q) == 2 || System.Math.Abs(coord.R) == 2;
                result.Add((coord, resources[i++], coastal));
            }

            return result;
        }

        public static BoardState CreateBoard(bool useThirteenHex = false)
        {
            var board = new BoardState();
            var preset = useThirteenHex ? MediumThirteenHex() : SmallSevenHex();

            foreach (var (coord, resource, coastal) in preset)
            {
                board.Tiles[coord] = new HexTileData(coord, resource, coastal);
            }

            board.PlaceRobber(new HexCoord(0, 0));
            return board;
        }
    }
}
