using System.Collections.Generic;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests
{
    /// <summary>Mesh checks shared by the player piece / placement ghost tests.</summary>
    internal static class PlayerPieceTestUtil
    {
        public static MeshFilter SingleMesh(GameObject piece, string partName)
        {
            var filters = piece.GetComponentsInChildren<MeshFilter>();
            Assert.AreEqual(1, filters.Length, $"{piece.name} should be one mesh");
            Assert.AreEqual(partName, filters[0].gameObject.name);
            Assert.NotNull(filters[0].sharedMesh);
            return filters[0];
        }

        /// <summary>Every edge (vertices welded by position) is shared by exactly two triangles.</summary>
        public static void AssertClosed(Mesh mesh)
        {
            var verts = mesh.vertices;
            var ids = new int[verts.Length];
            var reps = new List<Vector3>();
            var grid = new Dictionary<Vector3Int, List<int>>();
            const float cell = 0.001f, tolerance = 0.0002f;
            for (int i = 0; i < verts.Length; i++)
            {
                var c = Vector3Int.FloorToInt(verts[i] / cell);
                int found = -1;
                for (int dx = -1; dx <= 1 && found < 0; dx++)
                for (int dy = -1; dy <= 1 && found < 0; dy++)
                for (int dz = -1; dz <= 1 && found < 0; dz++)
                {
                    if (!grid.TryGetValue(c + new Vector3Int(dx, dy, dz), out var list))
                        continue;
                    foreach (int r in list)
                    {
                        if ((reps[r] - verts[i]).sqrMagnitude <= tolerance * tolerance)
                        {
                            found = r;
                            break;
                        }
                    }
                }

                if (found < 0)
                {
                    found = reps.Count;
                    reps.Add(verts[i]);
                    if (!grid.TryGetValue(c, out var bucket))
                        grid[c] = bucket = new List<int>();
                    bucket.Add(found);
                }

                ids[i] = found;
            }

            var edges = new Dictionary<(int, int), int>();
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var tris = mesh.GetTriangles(s);
                for (int t = 0; t < tris.Length; t += 3)
                {
                    for (int e = 0; e < 3; e++)
                    {
                        int a = ids[tris[t + e]], b = ids[tris[t + (e + 1) % 3]];
                        var key = a < b ? (a, b) : (b, a);
                        edges[key] = edges.TryGetValue(key, out int n) ? n + 1 : 1;
                    }
                }
            }

            int open = 0;
            foreach (var kv in edges)
                if (kv.Value != 2)
                    open++;
            Assert.AreEqual(0, open, $"{mesh.name} should be a closed mesh");
        }

        public static Material Slot(Renderer renderer, string kind)
        {
            foreach (var m in renderer.sharedMaterials)
                if (m != null && m.name == kind)
                    return m;
            Assert.Fail($"{renderer.name} has no {kind} material slot");
            return null;
        }

        /// <summary>Lowest / highest world Y over the actual mesh vertices (renderer AABBs are loose once leaned).</summary>
        public static Vector2 WorldYRange(MeshFilter filter)
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (var v in filter.sharedMesh.vertices)
            {
                float y = filter.transform.TransformPoint(v).y;
                min = Mathf.Min(min, y);
                max = Mathf.Max(max, y);
            }

            return new Vector2(min, max);
        }

        public static float MaxRgb(Color c) => Mathf.Max(c.r, Mathf.Max(c.g, c.b));

        /// <summary>Model-space size scaled by the part's local scale (unrotated).</summary>
        public static Vector3 ScaledModelSize(MeshFilter filter) =>
            Vector3.Scale(filter.sharedMesh.bounds.size, filter.transform.localScale);

        public static int RoofTriangles(Mesh mesh, Material[] slots)
        {
            int count = 0;
            var normals = mesh.normals;
            for (int s = 0; s < mesh.subMeshCount && s < slots.Length; s++)
            {
                if (slots[s].name != PlayerPieceVisuals.RoofMaterialName
                    && slots[s].name != PlayerPieceVisuals.RoofShadeMaterialName)
                    continue;
                var tris = mesh.GetTriangles(s);
                for (int t = 0; t < tris.Length; t += 3)
                {
                    float ny = normals[tris[t]].y;
                    if (ny > 0.2f && ny < 0.97f)
                        count++;
                }
            }

            return count;
        }
    }
}
