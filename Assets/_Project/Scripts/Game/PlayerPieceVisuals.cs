using System.Collections.Generic;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// Classic Catan wooden board pieces built from the Blender meshes in
    /// <c>Resources/Art/Models/ColonistPieces</c> (tools/blender/pieces.py): a pentagon-prism house,
    /// a city (long house joined with a tower house, ~1.5x the settlement footprint) and a bevelled
    /// plank road. Each piece is one closed mesh with three material slots: Wall (player colour),
    /// Roof (darker) and Edge (bevel faces, darkest).
    /// <para>
    /// The colonist table camera looks straight down and so do the pieces (v128): roofs are the main
    /// read, with only a small lean of <see cref="ColonistLeanDegrees"/> toward screen-top so a
    /// sliver of gable wall and the two lit roof planes give a hint of 3D.
    /// </para>
    /// </summary>
    public static class PlayerPieceVisuals
    {
        public const string SettlementName = "Settlement";
        public const string CityName = "City";
        public const string RoadName = "Road";
        /// <summary>Child holding the single house/city mesh.</summary>
        public const string BodyPartName = "Body";
        /// <summary>Child holding the single road plank mesh.</summary>
        public const string PlankPartName = "Plank";

        public const string WallMaterialName = "PieceWall";
        public const string RoofMaterialName = "PieceRoof";
        public const string EdgeMaterialName = "PieceEdge";
        /// <summary>The roof plane facing away from the (screen top-left) light: a darker second tone.</summary>
        public const string RoofShadeMaterialName = "PieceRoofShade";

        public static readonly Color HumanColor = new(0.2f, 0.4f, 0.9f);
        public static readonly Color AiColor = new(0.9f, 0.25f, 0.2f);

        /// <summary>Seen from above the roof is the main read, so its lit plane stays close to the player colour.</summary>
        public const float RoofColorMultiply = 0.84f;
        /// <summary>The shaded roof plane: the ridge reads as a clear two-tone split from above.</summary>
        public const float RoofShadeColorMultiply = 0.58f;
        /// <summary>Walls are lightened toward white so the darker roof separates (blue was murky).</summary>
        public const float WallLighten = 0.22f;
        public const float EdgeColorMultiply = 0.32f;

        /// <summary>Settlement wall width as a fraction of the flat-to-flat hex width (city is 1.5x).</summary>
        public const float SettlementFootprintOfHex = 0.18f;
        /// <summary>Road plank width as a fraction of the flat-to-flat hex width.</summary>
        public const float RoadWidthOfHex = 0.085f;
        /// <summary>
        /// Each plank runs vertex-centre to vertex-centre and then past it by this fraction of its
        /// width, so consecutive roads (and the settlement on the vertex) overlap with no gap.
        /// </summary>
        public const float RoadJointOverlapOfWidth = 0.5f;
        /// <summary>Small lean toward screen-top: near top-down, with just a hint of wall (v128; was 50).</summary>
        public const float ColonistLeanDegrees = 12f;
        /// <summary>Yaw of the gable end away from facing the viewer straight on.</summary>
        public const float ColonistYawDegrees = 32f;

        /// <summary>City turn relative to the settlement so its long wall and tower face the viewer.</summary>
        public const float CityExtraYawDegrees = 90f;

        private const string ModelRoot = "Art/Models/ColonistPieces";
        private const float BaseGapAboveHex = 0.02f;
        private const float RoadGapAboveHex = 0.012f;

        public static float FlatHexWidth(float hexScale) =>
            hexScale * Mathf.Sqrt(3f) * HexMath.OuterRadius;

        public static Color ColorForPlayer(PlayerId owner) =>
            owner == PlayerId.Human ? HumanColor : AiColor;

        public static Color DarkenForRoof(Color color) => Shade(color, RoofColorMultiply);

        public static Color DarkenForRoofShade(Color color) => Shade(color, RoofShadeColorMultiply);

        /// <summary>Wall tint: the player colour lifted toward white (keeps alpha).</summary>
        public static Color WallColor(Color color)
        {
            var c = Color.Lerp(color, Color.white, WallLighten);
            c.a = color.a;
            return c;
        }

        public static Color DarkenForEdge(Color color) => Shade(color, EdgeColorMultiply);

        private static Color Shade(Color c, float k) => new(c.r * k, c.g * k, c.b * k, c.a);

        public static GameObject CreateSettlement(
            Transform parent,
            Vector3 worldPosition,
            Color color,
            float hexTopY,
            float hexScale = ColonistBoardVisualScale.ReferenceHexScale) =>
            CreateBuilding(parent, SettlementName, "settlement", worldPosition, color, hexTopY, hexScale, 0f);

        public static GameObject CreateCity(
            Transform parent,
            Vector3 worldPosition,
            Color color,
            float hexTopY,
            float hexScale = ColonistBoardVisualScale.ReferenceHexScale) =>
            // Turned side-on: the long house faces the viewer with the tower rising at one end.
            CreateBuilding(parent, CityName, "city", worldPosition, color, hexTopY, hexScale, CityExtraYawDegrees);

        public static GameObject CreateRoad(
            Transform parent,
            Vector3 midpoint,
            float angleY,
            float length,
            Color color,
            float hexTopY,
            float hexScale = ColonistBoardVisualScale.ReferenceHexScale)
        {
            var root = new GameObject(RoadName);
            root.transform.SetParent(parent, false);
            root.transform.position = midpoint;
            root.transform.rotation = Quaternion.Euler(0f, angleY, 0f);

            var mesh = LoadMesh("road", out var slotNames);
            var b = mesh.bounds;
            float targetWidth = FlatHexWidth(hexScale) * RoadWidthOfHex;
            float targetLength = Mathf.Max(length + 2f * RoadJointOverlapOfWidth * targetWidth, 0.05f);
            float sx = targetLength / Mathf.Max(b.size.x, 1e-4f);
            float sz = targetWidth / Mathf.Max(b.size.z, 1e-4f);
            float sy = sz; // keep the bevel profile square-ish

            // Blender beam runs along model X; the road root's local Z runs along the edge. Same
            // small lean toward screen-top as the houses, so the beam shows a sliver of side.
            var localRot = Quaternion.Euler(0f, 90f, 0f);
            var worldRot = ColonistPresentation.IsActive
                ? RoadSideLean(root.transform.forward) * root.transform.rotation * localRot
                : root.transform.rotation * localRot;
            var instanceMesh = SplitRoofByLight(mesh, slotNames, worldRot, out var instanceSlots);
            AssignWoodGrainUvs(instanceMesh);
            var plank = CreateMeshChild(root.transform, PlankPartName, instanceMesh, instanceSlots, color);
            plank.gameObject.AddComponent<OwnedMeshCleanup>().Owned = instanceMesh;
            ApplyWoodGrain(plank.GetComponent<MeshRenderer>());
            plank.rotation = worldRot;
            plank.localScale = new Vector3(sx, sy, sz);
            plank.localPosition = Vector3.zero;
            float minY = LowestWorldY(plank, instanceMesh);
            plank.position += Vector3.up * (hexTopY + RoadGapAboveHex - minY);
            DisableShadows(root);
            return root;
        }

        private static GameObject CreateBuilding(
            Transform parent,
            string rootName,
            string modelName,
            Vector3 worldPosition,
            Color color,
            float hexTopY,
            float hexScale,
            float extraYaw)
        {
            var root = new GameObject(rootName);
            root.transform.SetParent(parent, false);
            root.transform.position = worldPosition;

            var mesh = LoadMesh(modelName, out var slotNames);
            var rotation = ComputeBuildingRotation(extraYaw + BuildingYawDegrees(worldPosition));
            var instanceMesh = SplitRoofByLight(mesh, slotNames, rotation, out var instanceSlots);
            var body = CreateMeshChild(root.transform, BodyPartName, instanceMesh, instanceSlots, color);
            body.gameObject.AddComponent<OwnedMeshCleanup>().Owned = instanceMesh;

            // One scale for every building (from the settlement width) keeps the city at 1.5x.
            var settlementMesh = modelName == "settlement" ? mesh : LoadMesh("settlement", out _);
            float scale = FlatHexWidth(hexScale) * SettlementFootprintOfHex
                / Mathf.Max(settlementMesh.bounds.size.x, 1e-4f);
            body.localScale = Vector3.one * scale;
            body.localRotation = rotation;

            // Pivot on the footprint centre, then drop the lowest (leaned) point onto the hex top.
            var c = mesh.bounds.center;
            var footprintCentre = new Vector3(c.x, mesh.bounds.min.y, c.z);
            body.localPosition = -(body.localRotation * (footprintCentre * scale));
            float minY = LowestWorldY(body, mesh);
            body.position += Vector3.up * (hexTopY + BaseGapAboveHex - minY);

            DisableShadows(root);
            return root;
        }

        /// <summary>
        /// Stable pseudo-random yaw for the vertex at <paramref name="worldPosition"/> (hash of the
        /// position rounded to 1 cm), so a piece keeps its turn across rebuilds and frames.
        /// </summary>
        public static float VertexYawDegrees(Vector3 worldPosition)
        {
            unchecked
            {
                int x = Mathf.RoundToInt(worldPosition.x * 100f);
                int z = Mathf.RoundToInt(worldPosition.z * 100f);
                uint h = (uint)(x * 73856093) ^ (uint)(z * 19349663);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h % 3600u) * 0.1f;
            }
        }

        /// <summary>
        /// Per-vertex turn that keeps the 3/4 read: a random 18-50 deg to the left or right of
        /// gable-on, optionally turned 180 deg (the house is symmetric end to end; the city's tower
        /// just swaps sides). Fully random yaws produced roof-only or tower-hides-wing views.
        /// </summary>
        public static float BuildingYawDegrees(Vector3 worldPosition)
        {
            float r = VertexYawDegrees(worldPosition) / 360f; // stable 0..1
            float side = r < 0.5f ? 1f : -1f;
            float flip = (r * 4f) % 1f < 0.5f ? 0f : 180f;
            float magnitude = 18f + 32f * ((r * 16f) % 1f);
            return flip + side * magnitude - ColonistYawDegrees;
        }

        /// <summary>Gable toward the viewer (yawed), leaned back toward screen-top in colonist mode.</summary>
        internal static Quaternion ComputeBuildingRotation(float extraYaw)
        {
            if (!ColonistPresentation.IsActive)
                return Quaternion.Euler(0f, extraYaw + ColonistYawDegrees, 0f);

            Vector3 up = ColonistFlatBoardQuad.GetColonistCameraUpOnBoard();
            // Model +X is the settlement gable end: face it toward the viewer (screen-bottom).
            var face = Quaternion.FromToRotation(Vector3.right, -up);
            if (Vector3.Dot(face * Vector3.up, Vector3.up) < 0.5f)
                face = Quaternion.LookRotation(Vector3.Cross(-up, Vector3.up), Vector3.up);
            var yaw = Quaternion.AngleAxis(ColonistYawDegrees + extraYaw, Vector3.up);
            return LeanTowardScreenTop() * yaw * face;
        }

        /// <summary>The colonist lean: tilt the vertical by <see cref="ColonistLeanDegrees"/> toward screen-top.</summary>
        internal static Quaternion LeanTowardScreenTop()
        {
            Vector3 up = ColonistFlatBoardQuad.GetColonistCameraUpOnBoard();
            var leanTarget = Vector3.up * Mathf.Cos(ColonistLeanDegrees * Mathf.Deg2Rad)
                + up * Mathf.Sin(ColonistLeanDegrees * Mathf.Deg2Rad);
            return Quaternion.FromToRotation(Vector3.up, leanTarget);
        }

        /// <summary>
        /// Roads only roll about their own length (a lean along the beam would lift one end and
        /// open a joint gap): the part of the screen-top lean perpendicular to the road.
        /// </summary>
        internal static Quaternion RoadSideLean(Vector3 along)
        {
            along.y = 0f;
            if (along.sqrMagnitude < 1e-8f)
                return Quaternion.identity;
            along.Normalize();
            Vector3 up = ColonistFlatBoardQuad.GetColonistCameraUpOnBoard();
            var side = up - along * Vector3.Dot(up, along);
            float amount = side.magnitude;
            if (amount < 1e-4f)
                return Quaternion.identity;
            side /= amount;
            float deg = ColonistLeanDegrees * amount;
            var target = Vector3.up * Mathf.Cos(deg * Mathf.Deg2Rad) + side * Mathf.Sin(deg * Mathf.Deg2Rad);
            return Quaternion.FromToRotation(Vector3.up, target);
        }

        private static Texture2D _woodGrain;

        /// <summary>Planar UVs for the beam: u along its length, v across (grain streaks run lengthwise).</summary>
        private static void AssignWoodGrainUvs(Mesh mesh)
        {
            var v = mesh.vertices;
            var b = mesh.bounds;
            var uv = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++)
                uv[i] = new Vector2(
                    (v[i].x - b.min.x) / Mathf.Max(b.size.x, 1e-4f),
                    ((v[i].z - b.min.z) + (v[i].y - b.min.y)) / Mathf.Max(b.size.z + b.size.y, 1e-4f));
            mesh.uv = uv;
        }

        /// <summary>Multiplies a soft lengthwise wood grain into the beam's colour slots (white = no change).</summary>
        private static void ApplyWoodGrain(MeshRenderer renderer)
        {
            if (_woodGrain == null)
            {
                const int w = 128, h = 64;
                _woodGrain = new Texture2D(w, h, TextureFormat.RGBA32, true)
                {
                    name = "RoadWoodGrain",
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Trilinear,
                    anisoLevel = 4
                };
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float fy = y / (float)h;
                    float wave = Mathf.Sin((fy * 9f + Mathf.PerlinNoise(x * 0.03f, fy * 3f) * 1.6f) * Mathf.PI * 2f);
                    float fine = Mathf.PerlinNoise(x * 0.25f, y * 1.7f);
                    // v132: stronger grain (was 0.88 +/- 0.13): broad waves, thin dark grain lines and two
                    // plank seams across the beam so the road reads as wood from the board camera.
                    float line = Mathf.Clamp01((Mathf.Abs(wave) - 0.86f) / 0.14f);
                    float seam = (x % 43) < 2 ? 0.22f : 0f;
                    float k = 0.80f + 0.11f * wave + 0.10f * fine - 0.16f * line - seam;
                    byte c = (byte)(Mathf.Clamp01(k) * 255f);
                    px[y * w + x] = new Color32(c, c, c, 255);
                }

                _woodGrain.SetPixels32(px);
                _woodGrain.Apply(true);
            }

            foreach (var mat in renderer.sharedMaterials)
                if (mat != null && mat.name != EdgeMaterialName)
                    mat.mainTexture = _woodGrain;
        }

        /// <summary>
        /// Copies <paramref name="mesh"/> and moves the roof
        /// triangles whose (rotated) normal faces away from the scene light into an extra RoofShade sub-mesh.
        /// </summary>
        internal static Mesh SplitRoofByLight(Mesh mesh, string[] slotNames, Quaternion rotation, out string[] slots)
        {
            var light = ScreenTopLeftLightOnBoard();
            var copy = Object.Instantiate(mesh);
            copy.name = mesh.name + "_lit";
            var verts = mesh.vertices;
            var subs = new List<int[]>();
            var names = new List<string>();
            var shade = new List<int>();
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var tris = mesh.GetTriangles(s);
                string slot = s < slotNames.Length ? slotNames[s] : WallMaterialName;
                if (SlotKind(slot) != RoofMaterialName)
                {
                    subs.Add(tris);
                    names.Add(slot);
                    continue;
                }

                var lit = new List<int>();
                for (int t = 0; t < tris.Length; t += 3)
                {
                    var a = verts[tris[t]];
                    var n = rotation * Vector3.Cross(verts[tris[t + 1]] - a, verts[tris[t + 2]] - a);
                    var target = Vector3.Dot(new Vector3(n.x, 0f, n.z), light) >= 0f ? lit : shade;
                    target.Add(tris[t]);
                    target.Add(tris[t + 1]);
                    target.Add(tris[t + 2]);
                }

                subs.Add(lit.ToArray());
                names.Add(slot);
            }

            if (shade.Count > 0)
            {
                subs.Add(shade.ToArray());
                names.Add(RoofShadeMaterialName);
            }

            copy.subMeshCount = subs.Count;
            for (int s = 0; s < subs.Count; s++)
                copy.SetTriangles(subs[s], s, false);
            copy.RecalculateBounds();
            slots = names.ToArray();
            return copy;
        }

        /// <summary>
        /// Horizontal direction toward the scene's directional light (the sun, else the first
        /// directional light, else the GameSceneSetup default Euler(50,-30,0)), so the lit roof
        /// plane really is the one the light hits.
        /// </summary>
        internal static Vector3 ScreenTopLeftLightOnBoard()
        {
            Light sun = RenderSettings.sun;
            if (sun == null || sun.type != LightType.Directional)
            {
                sun = null;
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional)
                    {
                        sun = l;
                        break;
                    }
            }

            var forward = sun != null ? sun.transform.forward : Quaternion.Euler(50f, -30f, 0f) * Vector3.forward;
            var toward = -forward;
            toward.y = 0f;
            return toward.sqrMagnitude > 1e-6f ? toward.normalized : Vector3.forward;
        }

        private static float LowestWorldY(Transform body, Mesh mesh)
        {
            float min = float.MaxValue;
            foreach (var v in mesh.vertices)
                min = Mathf.Min(min, body.TransformPoint(v).y);
            return min;
        }

        private static Transform CreateMeshChild(
            Transform parent,
            string name,
            Mesh mesh,
            string[] slotNames,
            Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = CreateSlotMaterials(slotNames, color);
            return go.transform;
        }

        /// <summary>Wall = player colour, Roof darker, Edge (bevels) darkest, in the mesh's slot order.</summary>
        internal static Material[] CreateSlotMaterials(string[] slotNames, Color color)
        {
            var mats = new Material[slotNames.Length];
            for (int i = 0; i < slotNames.Length; i++)
            {
                var kind = SlotKind(slotNames[i]);
                var tint = kind == RoofShadeMaterialName ? DarkenForRoofShade(color)
                    : kind == RoofMaterialName ? DarkenForRoof(color)
                    : kind == EdgeMaterialName ? DarkenForEdge(color)
                    : WallColor(color);
                var mat = BuiltInMaterials.CreateMatteOpaque(tint);
                mat.name = kind;
                mats[i] = mat;
            }

            return mats;
        }

        internal static string SlotKind(string importedName)
        {
            if (importedName == RoofShadeMaterialName)
                return RoofShadeMaterialName;
            if (importedName != null && importedName.Contains("Roof"))
                return RoofMaterialName;
            if (importedName != null && importedName.Contains("Edge"))
                return EdgeMaterialName;
            return WallMaterialName;
        }

        private static Mesh LoadMesh(string modelName, out string[] slotNames)
        {
            var prefab = Resources.Load<GameObject>($"{ModelRoot}/{modelName}");
            var filter = prefab != null ? prefab.GetComponentInChildren<MeshFilter>() : null;
            var renderer = prefab != null ? prefab.GetComponentInChildren<MeshRenderer>() : null;
            if (filter == null || filter.sharedMesh == null || renderer == null)
                throw new System.InvalidOperationException(
                    $"Player piece model '{ModelRoot}/{modelName}' is missing (run tools/blender/run-pieces.sh).");

            var shared = renderer.sharedMaterials;
            slotNames = new string[shared.Length];
            for (int i = 0; i < shared.Length; i++)
                slotNames[i] = shared[i] != null ? shared[i].name : WallMaterialName;
            return filter.sharedMesh;
        }

        private static void DisableShadows(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
    }
}
