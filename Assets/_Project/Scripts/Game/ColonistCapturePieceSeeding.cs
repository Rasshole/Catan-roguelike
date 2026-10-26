using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;
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
            var fishVertices = CollectFishShoalBorderVertices(board);
            var vertices = EnumerateLandVertices(board);
            var spread = PickVerticesQuadrants(board, validator, vertices, fishVertices, 5);
            if (spread.Count < 5)
                return;

            var humanVerts = new List<Vertex> { spread[0], spread[1], spread[2] };
            var aiVerts = new List<Vertex> { spread[3], spread[4] };

            board.VertexBuildings[humanVerts[0]] = (BuildingType.Settlement, PlayerId.Human);
            board.VertexBuildings[humanVerts[1]] = (BuildingType.Settlement, PlayerId.Human);
            board.VertexBuildings[humanVerts[2]] = (BuildingType.City, PlayerId.Human);
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

        private static HashSet<Vertex> CollectFishShoalBorderVertices(BoardState board)
        {
            var set = new HashSet<Vertex>();
            if (board?.SeaFishGrounds == null)
                return set;

            foreach (var sea in board.SeaFishGrounds.Keys)
            {
                if (!SeaFrameSlotLayout.TryGetFishShoalPlacement(board, sea, 1f, out var placement))
                    continue;

                set.Add(VertexGraph.Canonicalize(placement.BorderVertexInland));
                set.Add(VertexGraph.Canonicalize(placement.BorderVertexLeft));
                set.Add(VertexGraph.Canonicalize(placement.BorderVertexRight));
            }

            return set;
        }

        private static List<Vertex> PickVerticesQuadrants(
            BoardState board,
            PlacementValidator validator,
            List<Vertex> candidates,
            HashSet<Vertex> fishBorderVertices,
            int count)
        {
            float centerX = 0f;
            float centerZ = 0f;
            int landCount = 0;
            foreach (var coord in board.Tiles.Keys)
            {
                if (!board.TryGetTile(coord, out var tile) || BoardState.IsSeaFishGround(tile))
                    continue;
                var (x, z) = HexMath.ToWorldPosition(coord, 1f);
                centerX += x;
                centerZ += z;
                landCount++;
            }

            if (landCount > 0)
            {
                centerX /= landCount;
                centerZ /= landCount;
            }

            var valid = new List<(Vertex vertex, int quadrant, float radius)>();
            foreach (var vertex in candidates)
            {
                if (!IsValidCapturePieceVertex(board, vertex, fishBorderVertices))
                    continue;
                if (!validator.CanPlaceSettlement(board, vertex, PlayerId.Human, setupPhase: true))
                    continue;
                if (!validator.CanPlaceSettlement(board, vertex, PlayerId.Ai, setupPhase: true))
                    continue;

                var (vx, vz) = HexMath.VertexToWorldPosition(vertex, 1f);
                int quadrant = (vx >= centerX ? 1 : 0) | (vz >= centerZ ? 2 : 0);
                float radius = (vx - centerX) * (vx - centerX) + (vz - centerZ) * (vz - centerZ);
                valid.Add((vertex, quadrant, radius));
            }

            var picked = new List<Vertex>(count);
            for (int q = 0; q < 4; q++)
            {
                Vertex? choice = null;
                float bestR = -1f;
                foreach (var (vertex, quadrant, radius) in valid)
                {
                    if (quadrant != q || !IsFarEnough(vertex, picked))
                        continue;
                    if (radius > bestR)
                    {
                        bestR = radius;
                        choice = vertex;
                    }
                }

                if (choice.HasValue)
                    picked.Add(choice.Value);
            }

            foreach (var (vertex, _, radius) in valid
                         .OrderByDescending(v => CountLandTouches(board, v.vertex))
                         .ThenByDescending(v => v.radius))
            {
                if (picked.Count >= count)
                    break;
                if (picked.Contains(vertex))
                    continue;
                if (!IsFarEnough(vertex, picked))
                    continue;
                picked.Add(vertex);
            }

            return picked;
        }

        private static int CountLandTouches(BoardState board, Vertex vertex)
        {
            int landTouches = 0;
            foreach (var hex in VertexGraph.GetHexesForVertex(vertex))
            {
                if (!board.TryGetTile(hex, out var tile) || BoardState.IsSeaFishGround(tile))
                    continue;
                landTouches++;
            }

            return landTouches;
        }

        internal static bool IsValidCapturePieceVertex(
            BoardState board,
            Vertex vertex,
            HashSet<Vertex> fishBorderVertices)
        {
            vertex = VertexGraph.Canonicalize(vertex);
            if (board.PortVertexToSeaSlot.ContainsKey(vertex))
                return false;
            if (fishBorderVertices != null && fishBorderVertices.Contains(vertex))
                return false;

            return CountLandTouches(board, vertex) == 3;
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
