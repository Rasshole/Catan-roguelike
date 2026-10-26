using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v145 Seafarers pieces: a small boat for a ship (hull in the player colour, cream sail) lying along its sea
    /// edge, and the black pirate ship on a sea hex. Built from tiny procedural meshes (shared, cached) so there is no
    /// new art to import; read from the near top-down table camera: a dark outline under the hull keeps the boat
    /// crisp on blue water, and the sail is a flat-ish triangle so it still shows from above.
    /// </summary>
    public static class ShipPieceVisuals
    {
        public const string ShipName = "Ship";
        public const string PirateName = "Pirate";
        public const string HullPartName = "Hull";
        public const string SailPartName = "Sail";

        /// <summary>Boat length as a fraction of the flat-to-flat hex width (the edge itself is ~0.58).</summary>
        public const float ShipLengthOfHex = 0.36f;
        public const float PirateLengthOfHex = 0.62f;

        private static Mesh _hull;
        private static Mesh _sail;
        private static readonly Dictionary<Color, Material> Mats = new();

        private static Material Mat(Color c)
        {
            c.a = 1f;
            if (!Mats.TryGetValue(c, out var m) || m == null)
            {
                m = BuiltInMaterials.CreateMatteOpaque(c);
                Mats[c] = m;
            }

            return m;
        }

        public static GameObject CreateShip(Transform parent, Vector3 midpoint, float angleY, Color color,
            float hexTopY, float hexScale = ColonistBoardVisualScale.ReferenceHexScale)
        {
            float len = PlayerPieceVisuals.FlatHexWidth(hexScale) * ShipLengthOfHex * PlayerPieceVisuals.RoadScaleMultiplier;
            return Build(parent, ShipName, midpoint, angleY, len, color,
                Color.Lerp(new Color(0.97f, 0.93f, 0.82f), color, 0.12f), hexTopY, skull: false);
        }

        public static GameObject CreatePirate(Transform parent, Vector3 hexCenter, float hexTopY,
            float hexScale = ColonistBoardVisualScale.ReferenceHexScale)
        {
            float len = PlayerPieceVisuals.FlatHexWidth(hexScale) * PirateLengthOfHex;
            return Build(parent, PirateName, hexCenter, 28f, len, new Color(0.13f, 0.11f, 0.10f),
                new Color(0.22f, 0.2f, 0.19f), hexTopY, skull: true);
        }

        private static GameObject Build(Transform parent, string name, Vector3 at, float angleY, float len, Color hull,
            Color sail, float hexTopY, bool skull)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(at.x, hexTopY + 0.02f, at.z);
            root.transform.rotation = Quaternion.Euler(0f, angleY, 0f);

            // Outline: the same hull, a touch larger, just below — dark for boats, a gold rim so the black pirate
            // ship reads on the dark sea.
            Part(root.transform, "Outline", HullMesh, new Vector3(len * 0.44f, len * 0.12f, len * 1.1f),
                new Vector3(0f, -0.004f, 0f), skull ? new Color(0.86f, 0.68f, 0.26f) : new Color(0.08f, 0.06f, 0.04f));
            Part(root.transform, HullPartName, HullMesh, new Vector3(len * 0.38f, len * 0.16f, len),
                Vector3.zero, hull);
            // Deck stripe in a darker shade so the hull reads as a boat, not a pill.
            Part(root.transform, "Deck", HullMesh, new Vector3(len * 0.24f, len * 0.165f, len * 0.7f),
                new Vector3(0f, 0.001f, -len * 0.04f), Color.Lerp(hull, Color.black, 0.35f));
            Part(root.transform, SailPartName, SailMesh, new Vector3(len * 0.62f, len * 0.5f, len * 0.62f),
                new Vector3(0f, len * 0.17f, len * 0.02f), sail);
            if (skull)
            {
                var dot = new GameObject("Skull");
                dot.transform.SetParent(root.transform, false);
                dot.transform.localPosition = new Vector3(len * 0.05f, len * 0.36f, len * 0.02f);
                dot.transform.localScale = new Vector3(len * 0.22f, 0.002f, len * 0.22f);
                dot.AddComponent<MeshFilter>().sharedMesh = ThinDiskMesh.Shared;
                var mr = dot.AddComponent<MeshRenderer>();
                mr.sharedMaterial = Mat(new Color(0.95f, 0.93f, 0.88f));
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }

            return root;
        }

        private static void Part(Transform root, string name, Mesh mesh, Vector3 scale, Vector3 pos, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mat(color);
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        /// <summary>Unit hull: pointed bow at +z, square-ish stern, tapered keel; 1 wide, 1 tall, 1 long.</summary>
        internal static Mesh HullMesh
        {
            get
            {
                if (_hull != null)
                    return _hull;
                var outline = new[]
                {
                    new Vector2(0f, 0.5f), new Vector2(0.32f, 0.26f), new Vector2(0.5f, 0f), new Vector2(0.48f, -0.3f),
                    new Vector2(0.36f, -0.5f), new Vector2(-0.36f, -0.5f), new Vector2(-0.48f, -0.3f),
                    new Vector2(-0.5f, 0f), new Vector2(-0.32f, 0.26f)
                };
                var verts = new List<Vector3>();
                var tris = new List<int>();
                int n = outline.Length;
                // Deck (top fan).
                verts.Add(new Vector3(0f, 1f, 0f));
                for (int i = 0; i < n; i++)
                    verts.Add(new Vector3(outline[i].x, 1f, outline[i].y));
                for (int i = 0; i < n; i++)
                {
                    tris.Add(0);
                    tris.Add(1 + i);
                    tris.Add(1 + (i + 1) % n);
                }

                // Sides down to a narrower keel line.
                for (int i = 0; i < n; i++)
                {
                    var a = outline[i];
                    var b = outline[(i + 1) % n];
                    int s = verts.Count;
                    verts.Add(new Vector3(a.x, 1f, a.y));
                    verts.Add(new Vector3(b.x, 1f, b.y));
                    verts.Add(new Vector3(b.x * 0.62f, 0f, b.y * 0.8f));
                    verts.Add(new Vector3(a.x * 0.62f, 0f, a.y * 0.8f));
                    tris.AddRange(new[] { s, s + 3, s + 2, s, s + 2, s + 1 });
                }

                _hull = new Mesh { name = "ShipHull" };
                _hull.SetVertices(verts);
                _hull.SetTriangles(tris, 0);
                _hull.RecalculateNormals();
                _hull.RecalculateBounds();
                return _hull;
            }
        }

        /// <summary>Unit sail: a triangle leaning back from the mast so it reads from above; double-sided.</summary>
        internal static Mesh SailMesh
        {
            get
            {
                if (_sail != null)
                    return _sail;
                var verts = new[]
                {
                    new Vector3(0f, 1f, 0.3f), // masthead
                    new Vector3(0.62f, 0f, 0.45f), // clew, out to the side
                    new Vector3(0.05f, 0f, -0.5f), // tack, aft
                };
                var all = new List<Vector3>(verts);
                all.AddRange(verts);
                _sail = new Mesh { name = "ShipSail" };
                _sail.SetVertices(all);
                _sail.SetTriangles(new[] { 0, 1, 2, 3, 5, 4 }, 0);
                _sail.RecalculateNormals();
                _sail.RecalculateBounds();
                return _sail;
            }
        }
    }
}
