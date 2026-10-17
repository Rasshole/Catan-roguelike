using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Quaternius + primitive props per terrain type (colonist board).</summary>
    public static class ColonistHexTileNatureDecor
    {
        private const string ModelRoot = "Art/Models/Nature";
        private const float LiftAboveTop = 0.04f;
        private const float MaxPropRadiusFactor = 0.65f;
        private static readonly string[] TreeModels =
        {
            "NormalTree_2", "BirchTree_1", "BirchTree_3", "BirchTree_2", "NormalTree_2"
        };

        private static readonly string[] RockModels = { "Rock_3", "Rock_3", "Rock_3" };
        private static readonly string[] BushModels = { "Bush_Flowers", "Bush", "Bush_Small_Flowers", "Bush_Large" };
        private static readonly string[] DesertPlantModels = { "DeadTree_5", "Bush_Small", "DeadTree_3", "Rock_3" };

        public static void Apply(GameObject hexRoot, HexTileData data, float hexScale)
        {
            if (hexRoot == null || data == null || BoardState.IsSeaFishGround(data))
                return;

            if (hexRoot.transform.Find("TileNatureProps") != null)
                return;

            if (data.IsDesert)
            {
                ApplyDesert(hexRoot, data, hexScale);
                return;
            }

            var propsRoot = new GameObject("TileNatureProps");
            propsRoot.transform.SetParent(hexRoot.transform, false);

            int seed = data.Coord.Q * 73 + data.Coord.R * 41;
            float tokenClear = TokenKeepOutCenterRadius(hexScale);

            switch (data.Resource)
            {
                case ResourceType.Wood:
                    ApplyForest(hexRoot, propsRoot, hexScale, seed, tokenClear);
                    break;
                case ResourceType.Wheat:
                    ApplyWheat(hexRoot, propsRoot, hexScale, seed, tokenClear);
                    break;
                case ResourceType.Sheep:
                    ApplySheep(hexRoot, propsRoot, hexScale, seed, tokenClear);
                    break;
                case ResourceType.Stone:
                    ApplyStone(hexRoot, propsRoot, hexScale, seed, tokenClear);
                    break;
                case ResourceType.Brick:
                    ApplyBrick(hexRoot, propsRoot, hexScale, seed, tokenClear);
                    break;
            }
        }

        private static void ApplyForest(
            GameObject hexRoot,
            GameObject propsRoot,
            float hexScale,
            int seed,
            float tokenClear)
        {
            int treeTarget = 7 + PositiveMod(seed, 4);
            float minR = tokenClear;
            float maxR = MaxPropCenterRadius(hexScale);
            float minSep = hexScale * 0.10f;
            var ring = SamplePoissonAnnulus(seed, treeTarget, minR, maxR, minSep, treeTarget * 90);
            if (ring.Count < treeTarget)
            {
                var relaxed = SamplePoissonAnnulus(seed + 911, treeTarget, minR, maxR, minSep * 0.72f, treeTarget * 120);
                foreach (var p in relaxed)
                {
                    if (ring.Count >= treeTarget)
                        break;
                    if (IsSeparated(ring, p, minSep * 0.72f))
                        ring.Add(p);
                }
            }

            int placed = 0;
            int attempt = 0;
            foreach (var ringPoint in ring)
            {
                if (placed >= treeTarget)
                    break;
                // Poisson points are (x, z) in a Vector2; the implicit Vector2->Vector3 cast would
                // put z into Y (trees floating on a line), so map explicitly onto the XZ plane.
                var localXZ = new Vector3(ringPoint.x, 0f, ringPoint.y);
                string modelName = TreeModels[PositiveMod(seed + attempt, TreeModels.Length)];
                float scaleFactor = Mathf.Lerp(0.22f, 0.26f, Hash01(seed + attempt * 19));
                float yaw = PositiveMod(seed + attempt * 137, 360) + 22f;
                if (TryPlaceNatureModel(hexRoot, propsRoot.transform, hexScale, modelName, localXZ, scaleFactor, yaw,
                        ResourceType.Wood, attempt))
                    placed++;
                attempt++;
            }

            for (int i = placed; i < treeTarget; i++)
            {
                float angle = (i * 37f + PositiveMod(seed, 360)) * Mathf.Deg2Rad;
                float r = Mathf.Lerp(minR, maxR, 0.45f + Hash01(seed + i * 31) * 0.5f);
                var localXZ = new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                string modelName = TreeModels[PositiveMod(seed + i, TreeModels.Length)];
                float scaleFactor = Mathf.Lerp(0.22f, 0.26f, Hash01(seed + i * 19));
                float yaw = PositiveMod(seed + i * 137, 360) + 22f;
                TryPlaceNatureModel(hexRoot, propsRoot.transform, hexScale, modelName, localXZ, scaleFactor, yaw,
                    ResourceType.Wood, i);
            }

            ScatterModels(hexRoot, propsRoot, hexScale, seed + 1200, 2, BushModels, 0.14f, 0.18f, tokenClear,
                ResourceType.Wood);
        }

        private static void ApplyDesert(GameObject hexRoot, HexTileData data, float hexScale)
        {
            // Desert lake (Fishermen) tiles also get cacti/rocks: the keep-out radius already
            // clears the pond + shore (pond shore radius ~0.21 * hex diameter).
            var propsRoot = new GameObject("TileNatureProps");
            propsRoot.transform.SetParent(hexRoot.transform, false);
            int seed = data.Coord.Q * 19 + data.Coord.R * 53;
            float tokenClear = TokenKeepOutCenterRadius(hexScale);
            if (data.IsFishTile)
                tokenClear = Mathf.Max(tokenClear, hexScale * HexMath.OuterRadius * 2f * 0.35f * 0.59f + hexScale * 0.04f);

            int cactusTarget = 5 + PositiveMod(seed, 2);
            for (int c = 0; c < cactusTarget; c++)
            {
                var pos = SamplePosition(hexScale, seed + 300 + c * 61, tokenClear, MaxPropRadiusFactor);
                PlacePrimitive(hexRoot, propsRoot.transform, pos + Vector3.up * HexPrismMesh.TopY, hexScale,
                    parent => ColonistHexTilePropPrimitives.CreateCactus(parent, Vector3.zero, hexScale));
            }

            for (int r = 0; r < 2; r++)
            {
                var pos = SamplePosition(hexScale, seed + 360 + r * 71, tokenClear, MaxPropRadiusFactor);
                PlacePrimitive(hexRoot, propsRoot.transform, pos + Vector3.up * HexPrismMesh.TopY, hexScale,
                    parent => ColonistHexTilePropPrimitives.CreateDesertRock(parent, Vector3.zero, hexScale));
            }
        }

        private static void ApplyBrick(
            GameObject hexRoot,
            GameObject propsRoot,
            float hexScale,
            int seed,
            float tokenClear)
        {
            int target = 4 + PositiveMod(seed, 3);
            for (int i = 0; i < target; i++)
            {
                var pos = SamplePosition(hexScale, seed + 700 + i * 53, tokenClear, MaxPropRadiusFactor);
                int kind = PositiveMod(seed + i * 41, 5);
                if (kind == 0)
                {
                    PlacePrimitive(hexRoot, propsRoot.transform, pos + Vector3.up * HexPrismMesh.TopY, hexScale,
                        parent => ColonistHexTilePropPrimitives.CreateClayPit(parent, Vector3.zero, hexScale));
                }
                else if (kind == 1 || kind == 2)
                {
                    PlacePrimitive(hexRoot, propsRoot.transform, pos + Vector3.up * HexPrismMesh.TopY, hexScale,
                        parent => ColonistHexTilePropPrimitives.CreateBrickStack(parent, Vector3.zero, hexScale));
                }
                else if (kind == 3)
                {
                    PlacePrimitive(hexRoot, propsRoot.transform,
                        pos + Vector3.up * (HexPrismMesh.TopY + LiftAboveTop), hexScale,
                        parent => ColonistHexTilePropPrimitives.CreateKiln(parent, Vector3.zero, hexScale));
                }
                else
                {
                    TryPlaceNatureModel(hexRoot, propsRoot.transform, hexScale, RockModels[0], pos, 0.20f,
                        PositiveMod(seed + i * 91, 360), ResourceType.Brick, i);
                }
            }
        }

        private static void ApplyStone(
            GameObject hexRoot,
            GameObject propsRoot,
            float hexScale,
            int seed,
            float tokenClear)
        {
            int boulderTarget = 4 + PositiveMod(seed, 2);
            int peakTarget = 1 + PositiveMod(seed + 11, 2);
            int index = 0;
            for (int i = 0; i < boulderTarget; i++)
            {
                var pos = SamplePosition(hexScale, seed + 800 + i * 67, tokenClear, MaxPropRadiusFactor);
                int kind = PositiveMod(seed + i * 37, 3);
                if (kind == 0)
                {
                    PlacePrimitive(hexRoot, propsRoot.transform,
                        pos + Vector3.up * (HexPrismMesh.TopY + LiftAboveTop), hexScale,
                        parent => ColonistHexTilePropPrimitives.CreateRockPile(parent, Vector3.zero, hexScale));
                }
                else
                {
                    TryPlaceNatureModel(hexRoot, propsRoot.transform, hexScale, RockModels[0], pos, 0.22f,
                        PositiveMod(seed + i * 113, 360), ResourceType.Stone, index++);
                }
            }

            for (int p = 0; p < peakTarget; p++)
            {
                var pos = SamplePosition(hexScale, seed + 880 + p * 73, tokenClear, MaxPropRadiusFactor);
                PlacePrimitive(hexRoot, propsRoot.transform,
                    pos + Vector3.up * (HexPrismMesh.TopY + LiftAboveTop), hexScale,
                    parent => ColonistHexTilePropPrimitives.CreateMountainPeak(parent, Vector3.zero, hexScale));
            }
        }

        private static void ApplyWheat(
            GameObject hexRoot,
            GameObject propsRoot,
            float hexScale,
            int seed,
            float tokenClear)
        {
            int wheatClumps = 4 + PositiveMod(seed, 3);
            ScatterModels(hexRoot, propsRoot, hexScale, seed, wheatClumps, new[] { "Flower_2_Clump" }, 0.22f, 0.30f,
                tokenClear, ResourceType.Wheat);
            for (int r = 0; r < 3; r++)
            {
                var pos = SamplePosition(hexScale, seed + 400 + r * 59, tokenClear, MaxPropRadiusFactor);
                PlacePrimitive(hexRoot, propsRoot.transform, pos + Vector3.up * HexPrismMesh.TopY, hexScale,
                    parent =>
                        ColonistHexTilePropPrimitives.CreateWheatRow(
                            parent, Vector3.zero, hexScale, seed + r * 40f));
            }

            for (int s = 0; s < 2; s++)
            {
                var pos = SamplePosition(hexScale, seed + 450 + s * 61, tokenClear, MaxPropRadiusFactor);
                PlacePrimitive(hexRoot, propsRoot.transform, pos + Vector3.up * HexPrismMesh.TopY, hexScale,
                    parent => ColonistHexTilePropPrimitives.CreateWheatSheaf(parent, Vector3.zero, hexScale));
            }

            PlacePrimitive(hexRoot, propsRoot.transform,
                SamplePosition(hexScale, seed + 480, tokenClear, MaxPropRadiusFactor) + Vector3.up * HexPrismMesh.TopY,
                hexScale,
                parent => ColonistHexTilePropPrimitives.CreateHayBale(parent, Vector3.zero, hexScale));
            PlacePrimitive(hexRoot, propsRoot.transform,
                SamplePosition(hexScale, seed + 520, tokenClear, MaxPropRadiusFactor)
                    + Vector3.up * (HexPrismMesh.TopY + LiftAboveTop),
                hexScale,
                parent => ColonistHexTilePropPrimitives.CreateWindmill(parent, Vector3.zero, hexScale));
        }

        private static void ApplySheep(
            GameObject hexRoot,
            GameObject propsRoot,
            float hexScale,
            int seed,
            float tokenClear)
        {
            ScatterModels(hexRoot, propsRoot, hexScale, seed, 2, BushModels, 0.18f, 0.26f, tokenClear,
                ResourceType.Sheep);
            int sheepCount = 3 + PositiveMod(seed, 2);
            for (int s = 0; s < sheepCount; s++)
            {
                var pos = SamplePosition(hexScale, seed + 500 + s * 71, tokenClear, MaxPropRadiusFactor);
                PlacePrimitive(hexRoot, propsRoot.transform,
                    pos + Vector3.up * (HexPrismMesh.TopY + LiftAboveTop), hexScale,
                    parent =>
                        ColonistHexTilePropPrimitives.CreateSheep(
                            parent, Vector3.zero, hexScale * 0.22f, seed + s));
            }

            PlacePrimitive(hexRoot, propsRoot.transform,
                SamplePosition(hexScale, seed + 600, tokenClear, MaxPropRadiusFactor)
                    + Vector3.up * HexPrismMesh.TopY,
                hexScale,
                parent =>
                {
                    ColonistHexTilePropPrimitives.CreateFenceSegment(
                        parent, Vector3.zero, hexScale * 0.62f, PositiveMod(seed, 120));
                });
        }

        internal static int CountDecorProps(Transform propsRoot) =>
            propsRoot != null ? propsRoot.childCount : 0;

        internal static int CountDescendantsNamed(Transform propsRoot, string namePart)
        {
            if (propsRoot == null || string.IsNullOrEmpty(namePart))
                return 0;

            int count = 0;
            foreach (var t in propsRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains(namePart))
                    count++;
            }

            return count;
        }

        /// <summary>Minimum center distance before prop footprint (XZ projected bounds) may approach the token.</summary>
        private static float TokenKeepOutCenterRadius(float hexScale) =>
            HexTileView.TokenWorldFaceDiameter * 0.5f + hexScale * 0.08f;

        private static int PositiveMod(int value, int modulus) =>
            ((value % modulus) + modulus) % modulus;

        private static void ScatterModels(
            GameObject hexRoot,
            GameObject propsRoot,
            float hexScale,
            int seed,
            int count,
            IReadOnlyList<string> modelNames,
            float minScaleFactor,
            float maxScaleFactor,
            float tokenClear,
            ResourceType resource)
        {
            for (int i = 0; i < count; i++)
            {
                if (!TryFindClearPosition(hexScale, seed + i * 137, tokenClear, MaxPropRadiusFactor, out var pos))
                    continue;

                string modelName = modelNames[PositiveMod(seed + i, modelNames.Count)];
                float scaleFactor = Mathf.Lerp(minScaleFactor, maxScaleFactor, Hash01(seed + i * 17));
                float yaw = PositiveMod(seed + i * 137, 360) + 22f;
                TryPlaceNatureModel(hexRoot, propsRoot.transform, hexScale, modelName, pos, scaleFactor, yaw, resource,
                    i);
            }
        }

        private static bool TryPlaceNatureModel(
            GameObject hexRoot,
            Transform propsParent,
            float hexScale,
            string modelName,
            Vector3 localXZ,
            float scaleFactor,
            float yawDeg,
            ResourceType resource,
            int index)
        {
            var prefab = Resources.Load<GameObject>($"{ModelRoot}/{modelName}");
            if (prefab == null)
                return false;

            float scale = scaleFactor * hexScale;
            var prop = Object.Instantiate(prefab, propsParent, false);
            prop.name = $"NatureProp_{modelName}_{index}";
            prop.transform.localPosition = localXZ + Vector3.up * (HexPrismMesh.TopY + LiftAboveTop);
            prop.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            prop.transform.localScale = Vector3.one * scale;

            foreach (var collider in prop.GetComponentsInChildren<Collider>())
                ColonistSceneDestroy.SafeDestroy(collider);

            PruneAndStyleRenderers(prop, resource, hexScale);

            if (!NudgeOutwardUntilClear(hexRoot, prop.transform, TokenKeepOutCenterRadius(hexScale), hexScale))
            {
                ColonistSceneDestroy.SafeDestroy(prop);
                return false;
            }

            var flat = new Vector2(prop.transform.localPosition.x, prop.transform.localPosition.z);
            if (flat.magnitude > MaxPropCenterRadius(hexScale))
            {
                ColonistSceneDestroy.SafeDestroy(prop);
                return false;
            }

            if (resource == ResourceType.Wood)
            {
                var shadowPos = new Vector3(prop.transform.localPosition.x, 0f, prop.transform.localPosition.z);
                ColonistHexTilePropPrimitives.CreateTreeShadowDisc(propsParent, shadowPos, hexScale);
            }

            return true;
        }

        private static List<Vector2> SamplePoissonAnnulus(
            int seed,
            int targetCount,
            float minR,
            float maxR,
            float minSep,
            int maxAttempts)
        {
            var points = new List<Vector2>(targetCount);
            for (int attempt = 0; attempt < maxAttempts && points.Count < targetCount; attempt++)
            {
                int s = seed + attempt * 53;
                float angle = Hash01(s) * Mathf.PI * 2f;
                float r = Mathf.Sqrt(Mathf.Lerp(minR * minR, maxR * maxR, Hash01(s + 17)));
                var candidate = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
                if (IsSeparated(points, candidate, minSep))
                    points.Add(candidate);
            }

            return points;
        }

        private static bool IsSeparated(List<Vector2> points, Vector2 candidate, float minSep)
        {
            float minSepSq = minSep * minSep;
            for (int i = 0; i < points.Count; i++)
            {
                if ((points[i] - candidate).sqrMagnitude < minSepSq)
                    return false;
            }

            return true;
        }

        internal static bool IsForestTreePropName(string propName)
        {
            if (string.IsNullOrEmpty(propName))
                return false;

            foreach (var tree in TreeModels)
            {
                if (propName.Contains(tree))
                    return true;
            }

            return propName.Contains("Tree");
        }

        private static void PlacePrimitive(
            GameObject hexRoot,
            Transform propsParent,
            Vector3 localPos,
            float hexScale,
            System.Action<Transform> build)
        {
            var holder = new GameObject("PrimitiveProp");
            holder.transform.SetParent(propsParent, false);
            holder.transform.localPosition = localPos;
            build(holder.transform);

            if (!NudgeOutwardUntilClear(hexRoot, holder.transform, TokenKeepOutCenterRadius(hexScale), hexScale))
                ColonistSceneDestroy.SafeDestroy(holder.gameObject);
        }

        private static bool TryFindClearPosition(
            float hexScale,
            int seed,
            float tokenClear,
            float maxRadiusFactor,
            out Vector3 pos)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                pos = SamplePosition(hexScale, seed + attempt * 47, tokenClear, maxRadiusFactor);
                if (pos.magnitude <= hexScale * 0.70f && pos.magnitude >= tokenClear)
                    return true;
            }

            pos = new Vector3(tokenClear + hexScale * 0.15f, 0f, 0f);
            return true;
        }

        private static Vector3 SamplePosition(float hexScale, int seed, float tokenClear, float maxRadiusFactor)
        {
            // Keep centres within MaxPropCenterRadius so props don't spill over hex edges / into the sea.
            float maxR = MaxPropCenterRadius(hexScale);
            float angle = 0f;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                int s = seed + attempt * 47;
                angle = PositiveMod(s, 360) * Mathf.Deg2Rad;
                float r = tokenClear + hexScale * (0.06f + Hash01(s) * maxRadiusFactor);
                if (r <= maxR)
                    return new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
            }

            float fallback = Mathf.Min(maxR, tokenClear + hexScale * 0.12f);
            return new Vector3(Mathf.Cos(angle) * fallback, 0f, Mathf.Sin(angle) * fallback);
        }

        private static float MaxPropCenterRadius(float hexScale) => hexScale * 0.70f;

        private static bool NudgeOutwardUntilClear(GameObject hexRoot, Transform prop, float keepOut, float hexScale)
        {
            if (PropFootprintClearsToken(hexRoot, prop.gameObject, keepOut))
                return true;

            var start = prop.localPosition;
            var flat = new Vector2(start.x, start.z);
            if (flat.sqrMagnitude < 1e-6f)
                flat = Vector2.right * 0.01f;
            float r0 = flat.magnitude;
            var dir = flat / r0;
            float maxR = MaxPropCenterRadius(hexScale);
            const int steps = 6;
            for (int i = 1; i <= steps; i++)
            {
                float r = Mathf.Lerp(r0, maxR, i / (float)steps);
                prop.localPosition = new Vector3(dir.x * r, start.y, dir.y * r);
                if (PropFootprintClearsToken(hexRoot, prop.gameObject, keepOut))
                    return true;
            }

            prop.localPosition = start;
            return false;
        }

        private static bool PropFootprintClearsToken(GameObject hexRoot, GameObject prop, float keepOutCenterRadius)
        {
            if (prop == null || hexRoot == null)
                return true;

            float closest = float.MaxValue;
            foreach (var renderer in prop.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.enabled)
                    continue;

                closest = Mathf.Min(closest, ClosestXZDistanceToOrigin(hexRoot.transform, renderer.bounds));
            }

            return closest >= keepOutCenterRadius;
        }

        private static float ClosestXZDistanceToOrigin(Transform hexRoot, Bounds worldBounds)
        {
            var min = hexRoot.InverseTransformPoint(worldBounds.min);
            var max = hexRoot.InverseTransformPoint(worldBounds.max);
            float minX = Mathf.Min(min.x, max.x);
            float maxX = Mathf.Max(min.x, max.x);
            float minZ = Mathf.Min(min.z, max.z);
            float maxZ = Mathf.Max(min.z, max.z);

            float dx = minX > 0f ? minX : (maxX < 0f ? -maxX : 0f);
            float dz = minZ > 0f ? minZ : (maxZ < 0f ? -maxZ : 0f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static float Hash01(int seed) => (seed * 1103515245 + 12345 & 0x7fffffff) / (float)0x7fffffff;

        private static void PruneAndStyleRenderers(GameObject prop, ResourceType resource, float hexScale)
        {
            float maxExtent = hexScale * 0.85f;
            foreach (var renderer in prop.GetComponentsInChildren<Renderer>(true))
            {
                string n = renderer.gameObject.name.ToLowerInvariant();
                if (resource == ResourceType.Wood && (IsRockLikeName(n) || UsesRockAlbedo(renderer)))
                {
                    renderer.enabled = false;
                    continue;
                }

                if (n.Contains("rock") && resource != ResourceType.Stone)
                {
                    renderer.enabled = false;
                    continue;
                }

                var bounds = renderer.bounds;
                if (bounds.size.magnitude > maxExtent * 3f || bounds.extents.y > hexScale * 0.9f)
                {
                    renderer.enabled = false;
                    continue;
                }

                ColonistNaturePropMaterials.ApplyToRenderer(renderer, resource);
            }
        }

        private static bool IsRockLikeName(string n) =>
            n.Contains("rock") || n.Contains("stone") || n.Contains("boulder");

        private static bool UsesRockAlbedo(Renderer renderer)
        {
            var mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var tex = mats[i]?.mainTexture;
                if (tex != null && tex.name.ToLowerInvariant().Contains("rock"))
                    return true;
            }

            return false;
        }
    }
}
