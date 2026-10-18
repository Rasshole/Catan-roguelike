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
            var vertices = EnumerateLandVertices(board);
            var humanVerts = PickVerticesSpread(board, validator, vertices, 3, PlayerId.Human);
            if (humanVerts.Count < 3)
                return;

            board.VertexBuildings[humanVerts[0]] = (BuildingType.Settlement, PlayerId.Human);
            board.VertexBuildings[humanVerts[1]] = (BuildingType.Settlement, PlayerId.Human);
            board.VertexBuildings[humanVerts[2]] = (BuildingType.City, PlayerId.Human);

            var aiVerts = PickVerticesSpread(board, validator, vertices, 2, PlayerId.Ai);
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

        private static List<Vertex> PickVerticesSpread(
            BoardState board,
            PlacementValidator validator,
            List<Vertex> candidates,
            int count,
            PlayerId player)
        {
            var picked = new List<Vertex>(count);
            var valid = new List<(Vertex vertex, float angle)>();
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

            foreach (var vertex in candidates)
            {
                if (!validator.CanPlaceSettlement(board, vertex, player, setupPhase: true))
                    continue;
                var (vx, vz) = HexMath.VertexToWorldPosition(vertex, 1f);
                float angle = Mathf.Atan2(vz - centerZ, vx - centerX);
                valid.Add((vertex, angle));
            }

            valid = valid.OrderBy(v => v.angle).ToList();
            if (valid.Count == 0)
                return picked;

            int sectors = Mathf.Max(count * 2, 4);
            float sectorWidth = Mathf.PI * 2f / sectors;
            int sectorIndex = 0;
            while (picked.Count < count && sectorIndex < sectors * 4)
            {
                float targetAngle = (sectorIndex % sectors) * sectorWidth;
                Vertex? best = null;
                float bestScore = float.MaxValue;
                foreach (var (vertex, angle) in valid)
                {
                    if (!IsFarEnough(vertex, picked))
                        continue;
                    float delta = Mathf.Abs(NormalizeAngle(angle - targetAngle));
                    float score = delta + sectorIndex * 0.001f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = vertex;
                    }
                }

                if (best.HasValue)
                    picked.Add(best.Value);
                sectorIndex++;
            }

            if (picked.Count < count)
            {
                foreach (var (vertex, _) in valid)
                {
                    if (picked.Contains(vertex))
                        continue;
                    if (!IsFarEnough(vertex, picked))
                        continue;
                    picked.Add(vertex);
                    if (picked.Count >= count)
                        break;
                }
            }

            return picked;
        }

        private static float NormalizeAngle(float radians)
        {
            while (radians > Mathf.PI)
                radians -= Mathf.PI * 2f;
            while (radians < -Mathf.PI)
                radians += Mathf.PI * 2f;
            return Mathf.Abs(radians);
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
