using System.Collections.Generic;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// Fishermen fish-ground piece: the chevron band that hugs the coast, painted like the board
    /// tiles. Each piece gets a baked albedo: painted water with fish (Art/Board/FishGroundWater,
    /// tools/art/make_fish_water.py) inside the exact chevron outline, framed by the same thin sand
    /// bevel as the hex tiles (dark outer edge, light inner lip, soft inset shadow).
    /// </summary>
    public static class ColonistCoastalFishShoalVisual
    {
        public const string ShoalRootName = "CoastalFishShoal";
        public const string WaterTexturePath = "Art/Board/FishGroundWater";

        /// <summary>Fallback water tint when the painted texture is missing.</summary>
        public static readonly Color ChevronWaterBlue = new(0.36f, 0.69f, 0.80f, 1f);

        // Same frame palette as tools/art/make_ai_hex_tiles.py so pieces and tiles match.
        private static readonly Color Sand = new(226f / 255f, 196f / 255f, 122f / 255f);
        private static readonly Color SandDark = new(158f / 255f, 118f / 255f, 60f / 255f);
        private static readonly Color SandLight = new(247f / 255f, 230f / 255f, 172f / 255f);

        /// <summary>Frame width as a fraction of the hex apothem (tiles use 0.075).</summary>
        public const float FrameOfApothem = 0.06f;
        /// <summary>World size of one repeat of the painted water.</summary>
        public const float WaterWorldRepeat = 1.7f;
        private const int BakeResolution = 384;

        private static readonly Dictionary<string, Material> _materials = new();

        public static HexTileView Build(
            Transform boardRoot,
            HexTileData data,
            FishShoalPlacement placement,
            float hexHeightScale,
            float hexScale)
        {
            var root = new GameObject($"{ShoalRootName}_{data.Coord.Q}_{data.Coord.R}");
            root.transform.SetParent(boardRoot, false);
            root.transform.localPosition = new Vector3(
                placement.TokenCenter.X,
                0f,
                placement.TokenCenter.Z);
            root.transform.localScale = new Vector3(1f, hexHeightScale, 1f);

            var meshGo = new GameObject("ShoalMesh");
            meshGo.transform.SetParent(root.transform, false);
            var mesh = CoastalFishShoalMesh.CreateLocal(placement, placement.TokenCenter, hexScale);
            var outline = PolygonXZ(mesh.vertices);
            ComputeSquareBounds(outline, out Vector2 min, out float size);
            var uvs = new Vector2[mesh.vertexCount];
            var verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++)
                uvs[i] = new Vector2((verts[i].x - min.x) / size, (verts[i].z - min.y) / size);
            mesh.uv = uvs;

            meshGo.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = meshGo.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialFor(data, placement, outline, min, size, hexScale);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var view = root.AddComponent<HexTileView>();
            view.Initialize(data, renderer);
            view.RefreshNumberToken(data);
            return view;
        }

        public static void ClearCache() => _materials.Clear();

        private static Material MaterialFor(
            HexTileData data,
            FishShoalPlacement placement,
            Vector2[] outline,
            Vector2 min,
            float size,
            float hexScale)
        {
            string key = $"{data.Coord.Q}_{data.Coord.R}_{hexScale:F3}";
            if (_materials.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var water = Resources.Load<Texture2D>(WaterTexturePath);
            var tex = Bake(outline, min, size, hexScale, water, placement, data.Coord);
            var mat = BuiltInMaterials.CreateMatteOpaque(Color.white);
            mat.mainTexture = tex;
            mat.renderQueue = (int)RenderQueue.Geometry + 2;
            _materials[key] = mat;
            return mat;
        }

        /// <summary>Signed distance (negative inside) from <paramref name="p"/> to a simple polygon.</summary>
        internal static float SignedDistance(Vector2 p, Vector2[] poly)
        {
            float best = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j], b = poly[i];
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-12f));
                best = Mathf.Min(best, (p - (a + ab * t)).sqrMagnitude);
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }

            float d = Mathf.Sqrt(best);
            return inside ? -d : d;
        }

        private static Texture2D Bake(
            Vector2[] outline,
            Vector2 min,
            float size,
            float hexScale,
            Texture2D water,
            FishShoalPlacement placement,
            HexCoord coord)
        {
            int n = BakeResolution;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true)
            {
                name = $"FishGround_{coord.Q}_{coord.R}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };

            float apothem = hexScale * HexMath.OuterRadius * Mathf.Sqrt(3f) * 0.5f;
            float frame = FrameOfApothem * apothem;
            float px = size / n;
            // Water axes follow the coast so the fish swim along the band on every piece.
            var outward = new Vector2(placement.Outward.X, placement.Outward.Z).normalized;
            var along = new Vector2(outward.y, -outward.x);
            var offset = new Vector2((coord.Q * 0.37f) % 1f, (coord.R * 0.53f) % 1f);
            bool readable = water != null && water.isReadable;

            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2(min.x + (x + 0.5f) * px, min.y + (y + 0.5f) * px);
                    float d = SignedDistance(p, outline);
                    Color col;
                    if (d > 0f)
                    {
                        col = SandDark;
                    }
                    else
                    {
                        float t = Mathf.Clamp01(-d / frame);
                        col = Color.Lerp(SandDark, Sand, Smooth(0f, 0.32f, t));
                        col = Color.Lerp(col, SandLight, Smooth(0.45f, 0.78f, t) * (1f - Smooth(0.82f, 1f, t)) * 0.85f);

                        float dArt = d + frame; // <0 inside the painted water
                        if (dArt < px)
                        {
                            Color w = ChevronWaterBlue;
                            if (readable)
                            {
                                float u = Vector2.Dot(p, along) / WaterWorldRepeat + offset.x;
                                float v = Vector2.Dot(p, outward) / WaterWorldRepeat + offset.y;
                                w = water.GetPixelBilinear(u, v);
                            }

                            float rim = Smooth(-0.11f * apothem, 0f, dArt);
                            w *= 1f - 0.30f * rim * rim;
                            float a = Mathf.Clamp01(0.5f - dArt / px);
                            col = Color.Lerp(col, w, a);
                            float line = Mathf.Clamp01(1f - Mathf.Abs(dArt) / (0.9f * px)) * 0.35f;
                            col = Color.Lerp(col, SandDark * 0.7f, line);
                        }
                    }

                    col.a = 1f;
                    pixels[y * n + x] = col;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            return tex;
        }

        private static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        private static Vector2[] PolygonXZ(Vector3[] meshVertices)
        {
            var poly = new Vector2[meshVertices.Length];
            for (int i = 0; i < meshVertices.Length; i++)
                poly[i] = new Vector2(meshVertices[i].x, meshVertices[i].z);
            return poly;
        }

        private static void ComputeSquareBounds(Vector2[] poly, out Vector2 min, out float size)
        {
            var lo = new Vector2(float.MaxValue, float.MaxValue);
            var hi = new Vector2(float.MinValue, float.MinValue);
            foreach (var p in poly)
            {
                lo = Vector2.Min(lo, p);
                hi = Vector2.Max(hi, p);
            }

            size = Mathf.Max(hi.x - lo.x, hi.y - lo.y) * 1.02f;
            var centre = (lo + hi) * 0.5f;
            min = centre - Vector2.one * (size * 0.5f);
        }
    }
}
