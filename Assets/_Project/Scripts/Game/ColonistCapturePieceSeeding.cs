using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.Game
{
    /// <summary>Deterministic board pieces for colonist captures (visible demo layout).</summary>
    public static class ColonistCapturePieceSeeding
    {
        public const int ExpectedHumanSettlements = 2;
        public const int ExpectedHumanCities = 1;
        public const int ExpectedHumanRoads = 4;
        public const int ExpectedAiSettlements = 2;
        public const int ExpectedAiRoads = 3;

        public static void ForceDemoBoardPiecesForCapture(GameState state)
        {
            if (!ColonistPresentation.IsActive || state?.Board == null)
                return;

            var board = state.Board;
            board.VertexBuildings.Clear();
            board.Roads.Clear();
            board.DisabledRoads.Clear();

            var validator = new PlacementValidator();
            var vertices = EnumerateLandVertices(board);
            var humanVerts = PickVertices(board, validator, vertices, 3, PlayerId.Human);
            if (humanVerts.Count < 3)
                return;

            board.VertexBuildings[humanVerts[0]] = (BuildingType.Settlement, PlayerId.Human);
            board.VertexBuildings[humanVerts[1]] = (BuildingType.Settlement, PlayerId.Human);
            board.VertexBuildings[humanVerts[2]] = (BuildingType.City, PlayerId.Human);

            var aiVerts = PickVertices(board, validator, vertices, 2, PlayerId.Ai);
            if (aiVerts.Count < 2)
                return;

            board.VertexBuildings[aiVerts[0]] = (BuildingType.Settlement, PlayerId.Ai);
            board.VertexBuildings[aiVerts[1]] = (BuildingType.Settlement, PlayerId.Ai);

            PlaceRoads(board, validator, humanVerts, PlayerId.Human, ExpectedHumanRoads);
            PlaceRoads(board, validator, aiVerts, PlayerId.Ai, ExpectedAiRoads);
        }

        public static bool MatchesExpectedDemoCounts(BoardState board)
        {
            if (board == null)
                return false;

            int humanSettle = 0;
            int humanCity = 0;
            int aiSettle = 0;
            int humanRoads = 0;
            int aiRoads = 0;
            foreach (var kvp in board.VertexBuildings)
            {
                if (kvp.Value.owner == PlayerId.Human)
                {
                    if (kvp.Value.type == BuildingType.City)
                        humanCity++;
                    else if (kvp.Value.type == BuildingType.Settlement)
                        humanSettle++;
                }
                else if (kvp.Value.owner == PlayerId.Ai && kvp.Value.type == BuildingType.Settlement)
                    aiSettle++;
            }

            foreach (var owner in board.Roads.Values)
            {
                if (owner == PlayerId.Human)
                    humanRoads++;
                else if (owner == PlayerId.Ai)
                    aiRoads++;
            }

            return humanSettle == ExpectedHumanSettlements
                && humanCity == ExpectedHumanCities
                && aiSettle == ExpectedAiSettlements
                && humanRoads == ExpectedHumanRoads
                && aiRoads == ExpectedAiRoads;
        }

        private static List<Vertex> EnumerateLandVertices(BoardState board)
        {
            var set = new HashSet<Vertex>();
            foreach (var coord in board.Tiles.Keys)
            {
                if (!board.TryGetTile(coord, out var tile) || BoardState.IsSeaFishGround(tile))
                    continue;
                for (int c = 0; c < 6; c++)
                    set.Add(VertexGraph.Canonicalize(new Vertex(coord, c)));
            }

            return set
                .OrderBy(v => v.Hex.Q)
                .ThenBy(v => v.Hex.R)
                .ThenBy(v => v.CornerIndex)
                .ToList();
        }

        private static List<Vertex> PickVertices(
            BoardState board,
            PlacementValidator validator,
            List<Vertex> candidates,
            int count,
            PlayerId player)
        {
            var picked = new List<Vertex>(count);
            foreach (var vertex in candidates)
            {
                if (!IsFarEnough(vertex, picked))
                    continue;
                if (!validator.CanPlaceSettlement(board, vertex, player, setupPhase: true))
                    continue;
                picked.Add(vertex);
                if (picked.Count >= count)
                    break;
            }

            return picked;
        }

        private static bool IsFarEnough(Vertex vertex, IReadOnlyList<Vertex> picked)
        {
            for (int i = 0; i < picked.Count; i++)
            {
                if (VertexGraph.VertexDistance(vertex, picked[i]) < 2)
                    return false;
            }

            return true;
        }

        private static void PlaceRoads(
            BoardState board,
            PlacementValidator validator,
            List<Vertex> hubs,
            PlayerId player,
            int targetCount)
        {
            int placed = 0;
            foreach (var hub in hubs)
            {
                foreach (var adjacent in VertexGraph.GetAdjacentVertices(hub))
                {
                    var edge = VertexGraph.GetEdgeBetween(hub, adjacent);
                    if (board.Roads.ContainsKey(edge))
                        continue;
                    if (!validator.CanPlaceRoad(board, edge, player, setupPhase: true))
                        continue;

                    board.Roads[edge] = player;
                    placed++;
                    if (placed >= targetCount)
                        return;
                }
            }
        }
    }
}
