using System.Collections.Generic;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Beach from slightly enlarged land hex caps (union forms a continuous sand skirt).</summary>
    public static class ColonistCoastSandMesh
    {
        /// <summary>Classic Catan frame: land hexes meet water directly (no beach skirt).</summary>
        public static bool SandSkirtEnabled { get; set; } = false;

        public const float BeachWidthFactor = 0.25f;
        public const float DefaultLandHexSandScale = 1.18f;

        public static int LastBuiltQuadCount { get; private set; }

        public static int LastLandHexCount { get; private set; }

        public static float LastTargetBeachWidth { get; private set; }

        public static float LastSandHexScale { get; private set; }

        public static Mesh Create(BoardState board, float hexScale, float beachWidth, float meshLocalY = 0f)
        {
            if (!SandSkirtEnabled)
            {
                LastTargetBeachWidth = beachWidth;
                LastSandHexScale = 0f;
                LastLandHexCount = 0;
                LastBuiltQuadCount = 0;
                return new Mesh { name = "ColonistCoastSand_Disabled" };
            }

            LastTargetBeachWidth = beachWidth;
            float sandScale = ComputeSandHexScale(hexScale, beachWidth);
            LastSandHexScale = sandScale;

            var tileSet = CollectLandCoords(board);
            LastLandHexCount = tileSet.Count;
            if (tileSet.Count == 0)
            {
                LastBuiltQuadCount = 0;
                return new Mesh { name = "ColonistCoastSand_Empty" };
            }

            var vertices = new List<Vector3>(tileSet.Count * 7);
            var triangles = new List<int>(tileSet.Count * 18);
            int quadCount = 0;

            foreach (var coord in tileSet)
            {
                AppendScaledHexCap(coord, hexScale, sandScale, meshLocalY, vertices, triangles);
                quadCount += HexPrismMesh.SideCount;
            }

            LastBuiltQuadCount = quadCount;
            var mesh = new Mesh { name = "ColonistCoastSand" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static float ComputeSandHexScale(float hexScale, float beachWidth)
        {
            float outer = hexScale * HexMath.OuterRadius;
            if (outer <= 0.001f)
                return DefaultLandHexSandScale;

            float fromWidth = 1f + beachWidth / outer;
            return Mathf.Clamp(fromWidth, 1.05f, 1.35f);
        }

        public static bool IsPointCoveredBySand(
            BoardState board,
            float hexScale,
            float beachWidth,
            Vector2 xz)
        {
            float sandScale = ComputeSandHexScale(hexScale, beachWidth);
            float coverRadius = hexScale * HexMath.OuterRadius * sandScale * 1.01f;
            var tileSet = CollectLandCoords(board);
            foreach (var coord in tileSet)
            {
                var (cx, cz) = HexMath.ToWorldPosition(coord, hexScale);
                if (Vector2.Distance(xz, new Vector2(cx, cz)) <= coverRadius)
                    return true;
            }

            return false;
        }

        private static void AppendScaledHexCap(
            HexCoord coord,
            float hexScale,
            float sandScale,
            float y,
            List<Vector3> vertices,
            List<int> triangles)
        {
            var (centerX, centerZ) = HexMath.ToWorldPosition(coord, hexScale);
            int centerIndex = vertices.Count;
            vertices.Add(new Vector3(centerX, y, centerZ));

            int firstCorner = vertices.Count;
            for (int corner = 0; corner < HexPrismMesh.SideCount; corner++)
            {
                var (vx, vz) = HexMath.VertexToWorldPosition(coord, corner, hexScale);
                float lx = (vx - centerX) * sandScale;
                float lz = (vz - centerZ) * sandScale;
                vertices.Add(new Vector3(centerX + lx, y, centerZ + lz));
            }

            for (int corner = 0; corner < HexPrismMesh.SideCount; corner++)
            {
                int next = (corner + 1) % HexPrismMesh.SideCount;
                triangles.Add(centerIndex);
                triangles.Add(firstCorner + corner);
                triangles.Add(firstCorner + next);
            }
        }

        private static HashSet<HexCoord> CollectLandCoords(BoardState board)
        {
            var tileSet = new HashSet<HexCoord>();
            foreach (var kvp in board.Tiles)
            {
                if (kvp.Value.IsFishTile)
                    continue;
                tileSet.Add(kvp.Key);
            }

            return tileSet;
        }

        public static Vector2 ComputeBoardCenterXZ(BoardState board, float hexScale)
        {
            if (board.Tiles.Count == 0)
                return Vector2.zero;

            float sumX = 0f;
            float sumZ = 0f;
            int count = 0;
            foreach (var kvp in board.Tiles)
            {
                if (kvp.Value.IsFishTile)
                    continue;
                var (x, z) = HexMath.ToWorldPosition(kvp.Key, hexScale);
                sumX += x;
                sumZ += z;
                count++;
            }

            if (count == 0)
                return Vector2.zero;

            float inv = 1f / count;
            return new Vector2(sumX * inv, sumZ * inv);
        }

        public static Vector2 ComputeCentroidXZ(IReadOnlyList<Vector3> vertices)
        {
            if (vertices == null || vertices.Count == 0)
                return Vector2.zero;

            float sumX = 0f;
            float sumZ = 0f;
            for (int i = 0; i < vertices.Count; i++)
            {
                sumX += vertices[i].x;
                sumZ += vertices[i].z;
            }

            float inv = 1f / vertices.Count;
            return new Vector2(sumX * inv, sumZ * inv);
        }

        public static int CountCoastalEdges(BoardState board)
        {
            var tileSet = CollectLandCoords(board);
            int count = 0;
            foreach (var coord in tileSet)
            {
                for (int direction = 0; direction < 6; direction++)
                {
                    if (!tileSet.Contains(HexMath.GetNeighbor(coord, direction)))
                        count++;
                }
            }

            return count;
        }

        public static bool TryMeasureEdgeQuadWidth(
            Vector3 inner0,
            Vector3 inner1,
            Vector3 outer0,
            Vector3 outer1,
            out float width)
        {
            var innerMid = new Vector2((inner0.x + inner1.x) * 0.5f, (inner0.z + inner1.z) * 0.5f);
            var outerMid = new Vector2((outer0.x + outer1.x) * 0.5f, (outer0.z + outer1.z) * 0.5f);
            width = Vector2.Distance(innerMid, outerMid);
            return width > 0.001f;
        }

        public static float ComputeOuterExtentRadius(BoardState board, float hexScale, float beachWidth)
        {
            float sandScale = ComputeSandHexScale(hexScale, beachWidth);
            float maxR = 0f;
            var tileSet = CollectLandCoords(board);
            foreach (var coord in tileSet)
            {
                var (cx, cz) = HexMath.ToWorldPosition(coord, hexScale);
                for (int corner = 0; corner < HexPrismMesh.SideCount; corner++)
                {
                    var (vx, vz) = HexMath.VertexToWorldPosition(coord, corner, hexScale);
                    float sx = cx + (vx - cx) * sandScale;
                    float sz = cz + (vz - cz) * sandScale;
                    maxR = Mathf.Max(maxR, new Vector2(sx, sz).magnitude);
                }
            }

            return maxR > 0f
                ? maxR
                : TableCameraFraming.ComputeBoardBoundingRadius(board, hexScale);
        }

        public static bool TryMeasureSandAtCoastalEdgeMidpoint(
            BoardState board,
            HexCoord coord,
            int direction,
            float hexScale,
            float beachWidth,
            out Vector2 samplePoint)
        {
            samplePoint = default;
            var tileSet = CollectLandCoords(board);
            if (!tileSet.Contains(coord) || tileSet.Contains(HexMath.GetNeighbor(coord, direction)))
                return false;

            int corner0 = direction;
            int corner1 = (direction + 1) % 6;
            var (hexCx, hexCz) = HexMath.ToWorldPosition(coord, hexScale);
            var hexCenter = new Vector2(hexCx, hexCz);
            var inner0 = CornerVertex(coord, corner0, hexScale, 0f);
            var inner1 = CornerVertex(coord, corner1, hexScale, 0f);
            var outward = ComputeEdgeOutwardNormal(inner0, inner1, hexCenter);
            var innerMid = new Vector3(
                (inner0.x + inner1.x) * 0.5f,
                0f,
                (inner0.z + inner1.z) * 0.5f);
            samplePoint = new Vector2(
                innerMid.x + outward.x * (beachWidth * 0.5f),
                innerMid.z + outward.y * (beachWidth * 0.5f));
            return IsPointCoveredBySand(board, hexScale, beachWidth, samplePoint);
        }

        public static void AppendLandHexTopCornerSamples(
            BoardState board,
            float hexScale,
            float sampleY,
            List<Vector3> samples)
        {
            if (board == null || samples == null)
                return;

            var tileSet = CollectLandCoords(board);
            foreach (var coord in tileSet)
            {
                for (int corner = 0; corner < HexPrismMesh.SideCount; corner++)
                {
                    var (vx, vz) = HexMath.VertexToWorldPosition(coord, corner, hexScale);
                    samples.Add(new Vector3(vx, sampleY, vz));
                }
            }
        }

        public static void AppendOuterFramingSamples(
            BoardState board,
            float hexScale,
            float beachWidth,
            float sampleY,
            List<Vector3> samples) =>
            AppendLandHexTopCornerSamples(board, hexScale, sampleY, samples);

        private static Vector3 CornerVertex(HexCoord coord, int cornerIndex, float hexScale, float y)
        {
            var (x, z) = HexMath.VertexToWorldPosition(coord, cornerIndex, hexScale);
            return new Vector3(x, y, z);
        }

        private static Vector2 ComputeEdgeOutwardNormal(Vector3 inner0, Vector3 inner1, Vector2 hexCenter)
        {
            var edge = new Vector2(inner1.x - inner0.x, inner1.z - inner0.z);
            if (edge.sqrMagnitude < 1e-8f)
                return Vector2.right;
            edge.Normalize();
            var n1 = new Vector2(-edge.y, edge.x);
            var n2 = new Vector2(edge.y, -edge.x);
            var mid = new Vector2((inner0.x + inner1.x) * 0.5f, (inner0.z + inner1.z) * 0.5f);
            var toMid = mid - hexCenter;
            return Vector2.Dot(n1, toMid) >= Vector2.Dot(n2, toMid) ? n1 : n2;
        }
    }
}
