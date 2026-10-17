using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Kenney top-down sprites on terrain hexes; Quaternius bushes on sheep pastures only.</summary>
    public static class ColonistHexTileNatureDecor
    {
        private const string NatureModelRoot = "Art/Models/Nature";
        private const float LiftAboveTop = 0.04f;
        private const float MaxPropRadiusFactor = 0.65f;
        private static readonly string[] ForestTreeSprites =
            { "tree_canopy_b", "tree_canopy_c", "tree_canopy_b", "tree_canopy_c", "tree_canopy_b" };
        private static readonly string[] BushModels = { "Bush_Flowers", "Bush", "Bush_Small_Flowers", "Bush_Large" };
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
            // Dense wood: 4-5 Kenney tree clumps (4-6 trees each) around the ring, then single trees
            // fill the gaps. Single tall-narrow trees alone left most of the hex bare green.
            int clumpTarget = 4 + PositiveMod(seed, 2);
            int treeTarget = 12 + PositiveMod(seed, 5);
            float maxR = MaxPropCenterRadius(hexScale);
            float phase = PositiveMod(seed * 7, 360);
            int placed = 0;
            for (int c = 0; c < clumpTarget; c++)
            {
                float angle = (phase + c * 360f / clumpTarget) * Mathf.Deg2Rad;
                var localXZ = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * maxR;
                string sprite = PositiveMod(seed + c, 2) == 0 ? "tree_cluster_a" : "tree_cluster_b";
                float size = Mathf.Lerp(0.46f, 0.54f, Hash01(seed + c * 23));
                if (PlaceTopDown(hexRoot, propsRoot.transform, hexScale, localXZ, sprite, size, 0f,
                        $"ForestTree_{sprite}_{c}", tokenClear, maxR))
                    placed++;
            }

            for (int i = 0; placed < treeTarget && i < treeTarget * 3; i++)
            {
                float angle = (phase + 180f / clumpTarget + i * 47f) * Mathf.Deg2Rad;
                float r = Mathf.Lerp(tokenClear, maxR, 0.5f + Hash01(seed + i * 31) * 0.5f);
                var localXZ = new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                string sprite = ForestTreeSprites[PositiveMod(seed + i, ForestTreeSprites.Length)];
                float size = Mathf.Lerp(0.34f, 0.44f, Hash01(seed + i * 19));
                if (PlaceTopDown(hexRoot, propsRoot.transform, hexScale, localXZ, sprite, size, 0f,
                        $"ForestTree_{sprite}_{i}", tokenClear, maxR))
                    placed++;
            }
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

            int cactusTarget = 8 + PositiveMod(seed, 3);
            float maxR = MaxPropCenterRadius(hexScale);
            for (int c = 0; c < cactusTarget; c++)
            {
                var pos = SamplePosition(hexScale, seed + 300 + c * 61, tokenClear, MaxPropRadiusFactor);
                string sprite = PositiveMod(seed + c, 2) == 0 ? "cactus_tall" : "cactus_short";
                PlaceTopDown(hexRoot, propsRoot.transform, hexScale, pos, sprite, 0.30f,
                    PositiveMod(seed + c * 91, 24) - 12f, $"Cactus_{sprite}_{c}", tokenClear, maxR);
            }

            for (int r = 0; r < 4 + PositiveMod(seed, 2); r++)
            {
                var pos = SamplePosition(hexScale, seed + 360 + r * 71, tokenClear, MaxPropRadiusFactor);
                PlaceTopDown(hexRoot, propsRoot.transform, hexScale, pos, "desert_rock", 0.26f,
                    PositiveMod(seed + r * 113, 360), $"DesertRock_{r}", tokenClear, maxR);
            }

            ScatterModels(hexRoot, propsRoot, hexScale, seed + 900, 5 + PositiveMod(seed, 2),
                new[] { "Bush", "Bush_Small_Flowers", "Bush" }, 0.14f, 0.20f, tokenClear, ResourceType.Sheep);
        }

        private static void ApplyBrick(
            GameObject hexRoot,
            GameObject propsRoot,
            float hexScale,
            int seed,
            float tokenClear)
        {
            int target = 10 + PositiveMod(seed, 3);
            float maxR = MaxPropCenterRadius(hexScale);
            string[] sprites = { "clay_pit", "clay_pit", "clay_pit", "brick_stack", "brick_stack", "brick_stack" };
            for (int i = 0; i < target; i++)
            {
                var pos = SamplePosition(hexScale, seed + 700 + i * 53, tokenClear, MaxPropRadiusFactor);
                string sprite = sprites[PositiveMod(seed + i * 41, sprites.Length)];
                float size = sprite == "clay_pit" ? Mathf.Lerp(0.36f, 0.44f, Hash01(seed + i * 29)) : 0.32f;
                // Only the round clay mound spins freely: a brick pallet turned ~90° reads as a faint
                // brick wall (v106 review), so stacks stay upright within ±12°.
                float yaw = sprite != "clay_pit"
                    ? PositiveMod(seed + i * 91, 24) - 12f
                    : PositiveMod(seed + i * 91, 360);
                PlaceTopDown(hexRoot, propsRoot.transform, hexScale, pos, sprite, size,
                    yaw, $"BrickProp_{sprite}_{i}", tokenClear, maxR);
            }
        }

        private static void ApplyStone(
            GameObject hexRoot,
            GameObject propsRoot,
            float hexScale,
            int seed,
            float tokenClear)
        {
            int boulderTarget = 10 + PositiveMod(seed, 4);
            float maxR = MaxPropCenterRadius(hexScale);
            string[] rocks = { "rock_cluster_a", "rock_cluster_b", "rock_cluster_c" };
            for (int i = 0; i < boulderTarget; i++)
            {
                var pos = SamplePosition(hexScale, seed + 800 + i * 67, tokenClear, MaxPropRadiusFactor);
                string sprite = rocks[PositiveMod(seed + i * 37, rocks.Length)];
                float size = Mathf.Lerp(0.26f, 0.40f, Hash01(seed + i * 41));
                PlaceTopDown(hexRoot, propsRoot.transform, hexScale, pos, sprite, size,
                    PositiveMod(seed + i * 113, 360), $"StoneBoulder_{i}", tokenClear, maxR);
            }
        }

        private static void ApplyWheat(
            GameObject hexRoot,
            GameObject propsRoot,
            float hexScale,
            int seed,
            float tokenClear)
        {
            float maxR = MaxPropCenterRadius(hexScale);
            string[] fields = { "wheat_field_a", "wheat_field_b", "wheat_field_c" };
            int fieldPatches = 7 + PositiveMod(seed, 3);
            for (int r = 0; r < fieldPatches; r++)
            {
                var pos = SamplePosition(hexScale, seed + 400 + r * 59, tokenClear, MaxPropRadiusFactor);
                string fieldSprite = fields[PositiveMod(seed + r, fields.Length)];
                float size = Mathf.Lerp(0.30f, 0.42f, Hash01(seed + r * 29));
                PlaceTopDown(hexRoot, propsRoot.transform, hexScale, pos, fieldSprite, size,
                    PositiveMod(seed + r * 40, 360), $"WheatField_{r}", tokenClear, maxR);
            }

            for (int row = 0; row < 4; row++)
            {
                var pos = SamplePosition(hexScale, seed + 430 + row * 61, tokenClear, MaxPropRadiusFactor);
                float size = Mathf.Lerp(0.34f, 0.46f, Hash01(seed + row * 23));
                PlaceTopDown(hexRoot, propsRoot.transform, hexScale, pos, "wheat_rows", size,
                    PositiveMod(seed + row * 47, 360), $"WheatRows_{row}", tokenClear, maxR);
            }

            for (int s = 0; s < 5; s++)
            {
                var pos = SamplePosition(hexScale, seed + 460 + s * 63, tokenClear, MaxPropRadiusFactor);
                PlaceTopDown(hexRoot, propsRoot.transform, hexScale, pos, "wheat_sheaf",
                    Mathf.Lerp(0.32f, 0.44f, Hash01(seed + s * 31)),
                    PositiveMod(seed + 461 + s * 17, 360), $"WheatSheaf_{s}", tokenClear, maxR);
            }

            PlaceTopDown(hexRoot, propsRoot.transform, hexScale,
                SamplePosition(hexScale, seed + 520, tokenClear, MaxPropRadiusFactor), "windmill", 0.50f,
                PositiveMod(seed + 521, 24) - 12f, "Windmill_0", tokenClear, maxR);
        }

        private static void ApplySheep(
            GameObject hexRoot,
            GameObject propsRoot,
            float hexScale,
            int seed,
            float tokenClear)
        {
            ScatterModels(hexRoot, propsRoot, hexScale, seed, 8 + PositiveMod(seed, 3), BushModels, 0.16f, 0.28f,
                tokenClear, ResourceType.Sheep);
            float maxR = MaxPropCenterRadius(hexScale);
            var fencePositions = new List<Vector3>(3);
            for (int f = 0; f < 3; f++)
            {
                var fencePos = SamplePosition(hexScale, seed + 600 + f * 40, tokenClear, MaxPropRadiusFactor);
                fencePositions.Add(fencePos);
                PlaceTopDown(hexRoot, propsRoot.transform, hexScale, fencePos, "fence",
                    Mathf.Lerp(0.36f, 0.46f, Hash01(seed + f * 11)), PositiveMod(seed + f * 17, 24) - 12f,
                    $"SheepFence_{f}", tokenClear, maxR);
            }

            float fenceKeepOut = hexScale * 0.28f;
            int sheepCount = 8 + PositiveMod(seed, 3);
            for (int s = 0; s < sheepCount; s++)
            {
                Vector3 pos = Vector3.zero;
                bool found = false;
                for (int attempt = 0; attempt < 16; attempt++)
                {
                    pos = SamplePosition(hexScale, seed + 500 + s * 71 + attempt * 19, tokenClear, MaxPropRadiusFactor);
                    var flat = new Vector2(pos.x, pos.z);
                    bool clearOfFences = true;
                    foreach (var fence in fencePositions)
                    {
                        if (Vector2.Distance(flat, new Vector2(fence.x, fence.z)) < fenceKeepOut)
                        {
                            clearOfFences = false;
                            break;
                        }
                    }

                    if (clearOfFences)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    continue;

                float lift = HexPrismMesh.TopY + LiftAboveTop + s * 0.008f;
                float sheepScale = hexScale * Mathf.Lerp(0.20f, 0.24f, Hash01(seed + s * 41));
                PlacePrimitive(hexRoot, propsRoot.transform,
                    pos + Vector3.up * lift, hexScale,
                    parent =>
                        ColonistHexTilePropPrimitives.CreateSheep(
                            parent, Vector3.zero, sheepScale, seed + s));
            }
        }

        private static bool PlaceTopDown(
            GameObject hexRoot,
            Transform propsParent,
            float hexScale,
            Vector3 localXZ,
            string spriteName,
            float diameterInHexUnits,
            float yawDeg,
            string objectName,
            float tokenClear,
            float maxCenterRadius) =>
            ColonistHexTileTopDownDecor.TryPlace(
                hexRoot, propsParent, hexScale, localXZ, spriteName, diameterInHexUnits, yawDeg, objectName,
                tokenClear, maxCenterRadius);

        internal static bool NudgePropOutwardUntilClear(
            GameObject hexRoot,
            Transform prop,
            float keepOut,
            float hexScale) =>
            NudgeOutwardUntilClear(hexRoot, prop, keepOut, hexScale);

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
            var prefab = Resources.Load<GameObject>($"{NatureModelRoot}/{modelName}");
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

        internal static bool IsForestTreePropName(string propName) =>
            !string.IsNullOrEmpty(propName) && propName.Contains("ForestTree");

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
