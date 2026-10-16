using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Yield;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    public class ProductionFlySourcesTests
    {
        [Test]
        public void MapGainedResources_PicksHexMatchingResourceType()
        {
            var board = MapPresets.CreateBoard();
            var state = new GameState(board);
            var rolls = new Dictionary<ResourceType, int> { [ResourceType.Wood] = 2 };
            state.TodayDiceRolls = new List<int> { 6 };

            var vertex = VertexGraph.Canonicalize(new Vertex(new HexCoord(0, 0), 0));
            HexCoord? expectedWoodHex = null;
            foreach (var hex in VertexGraph.GetHexesForVertex(vertex))
            {
                if (!board.TryGetTile(hex, out var tile))
                    continue;
                if (tile.Resource != ResourceType.Wood)
                    continue;
                tile.NumberToken = 6;
                if (!expectedWoodHex.HasValue || CompareHexCoords(hex, expectedWoodHex.Value) < 0)
                    expectedWoodHex = hex;
            }

            Assert.IsTrue(expectedWoodHex.HasValue, "fixture needs a wood hex on the settlement corner");

            board.VertexBuildings[vertex] = (BuildingType.Settlement, PlayerId.Human);
            var gained = new ResourceBundle { Wood = 2 };

            var map = ProductionFlySources.MapGainedResourcesToHexes(
                state,
                PlayerId.Human,
                rolls,
                gained);

            Assert.IsTrue(map.ContainsKey(ResourceType.Wood));
            Assert.AreEqual(expectedWoodHex.Value, map[ResourceType.Wood]);
        }

        private static int CompareHexCoords(HexCoord a, HexCoord b)
        {
            int cmp = a.Q.CompareTo(b.Q);
            return cmp != 0 ? cmp : a.R.CompareTo(b.R);
        }
    }
}
