using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    public class AiTokenStrategyTests
    {
        [Test]
        public void BanditRaidEvent_TargetsHighPipHumanTile()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var highPipHex = new HexCoord(1, 0);
            var lowPipHex = new HexCoord(0, 1);

            board.Tiles[highPipHex].NumberToken = 8;
            board.Tiles[lowPipHex].NumberToken = 2;

            var highVertex = VertexGraph.Canonicalize(new Vertex(highPipHex, 0));
            var lowVertex = VertexGraph.Canonicalize(new Vertex(lowPipHex, 0));
            board.VertexBuildings[highVertex] = (BuildingType.Settlement, PlayerId.Human);
            board.VertexBuildings[lowVertex] = (BuildingType.Settlement, PlayerId.Human);

            var state = new GameState(board);
            var engine = new EventEngine();
            engine.ApplyEvent(state, EventId.BanditRaid);

            Assert.AreEqual(highPipHex, state.Board.RobberTile);
        }
    }
}
