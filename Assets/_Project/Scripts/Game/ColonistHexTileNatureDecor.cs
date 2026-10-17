using System.Collections.Generic;
using CatanRoguelike.Core;
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
                    ScatterModels(hexRoot, propsRoot, hexScale, seed, 6 + PositiveMod(seed, 4), TreeModels, 0.26f, 0.35f,
                        tokenClear, ResourceType.Wood);
                    break;
                case ResourceType.Wheat:
                    int wheatClumps = 5 + PositiveMod(seed, 4);
                    ScatterModels(hexRoot, propsRoot, hexScale, seed, wheatClumps, new[] { "Flower_2_Clump" }, 0.24f, 0.34f,
                        tokenClear, ResourceType.Wheat);
                    for (int r = 0; r < 4; r++)
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
                            parent =>
                                ColonistHexTilePropPrimitives.CreateWheatSheaf(parent, Vector3.zero, hexScale));
                    }
                    break;
                case ResourceType.Sheep:
                    ScatterModels(hexRoot, propsRoot, hexScale, seed, 4, BushModels, 0.22f, 0.32f, tokenClear,
                        ResourceType.Sheep);
                    for (int s = 0; s < 2; s++)
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
                    break;
                case ResourceType.Stone:
                    ScatterModels(hexRoot, propsRoot, hexScale, seed, 2, RockModels, 0.28f, 0.36f, tokenClear,
                        ResourceType.Stone);
                    for (int p = 0; p < 4; p++)
                    {
                        var pilePos = SamplePosition(hexScale, seed + 820 + p * 67, tokenClear, MaxPropRadiusFactor);
                        PlacePrimitive(hexRoot, propsRoot.transform,
                            pilePos + Vector3.up * (HexPrismMesh.TopY + LiftAboveTop), hexScale,
                            parent => ColonistHexTilePropPrimitives.CreateRockPile(parent, Vector3.zero, hexScale));
                    }
                    var minePos = SamplePosition(hexScale, seed + 800, tokenClear, MaxPropRadiusFactor);
                    PlacePrimitive(hexRoot, propsRoot.transform,
                        minePos + Vector3.up * (HexPrismMesh.TopY + LiftAboveTop), hexScale,
                        parent => ColonistHexTilePropPrimitives.CreateMineEntrance(parent, Vector3.zero, hexScale));
                    break;
                case ResourceType.Brick:
                    for (int b = 0; b < 3; b++)
                    {
                        var pos = SamplePosition(hexScale, seed + 700 + b * 53, tokenClear, MaxPropRadiusFactor);
                        PlacePrimitive(hexRoot, propsRoot.transform, pos + Vector3.up * HexPrismMesh.TopY, hexScale,
                            parent => ColonistHexTilePropPrimitives.CreateBrickStack(parent, Vector3.zero, hexScale));
                    }
                    PlacePrimitive(hexRoot, propsRoot.transform,
                        SamplePosition(hexScale, seed + 750, tokenClear, MaxPropRadiusFactor)
                            + Vector3.up * (HexPrismMesh.TopY + LiftAboveTop),
                        hexScale,
                        parent => ColonistHexTilePropPrimitives.CreateKiln(parent, Vector3.zero, hexScale));
                    break;
            }
        }

        private static void ApplyDesert(GameObject hexRoot, HexTileData data, float hexScale)
        {
            if (data.IsFishTile)
                return;

            var propsRoot = new GameObject("TileNatureProps");
            propsRoot.transform.SetParent(hexRoot.transform, false);
            int seed = data.Coord.Q * 19 + data.Coord.R * 53;
            float tokenClear = TokenKeepOutCenterRadius(hexScale);
            ScatterModels(hexRoot, propsRoot, hexScale, seed, 3, DesertPlantModels, 0.22f, 0.32f, tokenClear, ResourceType.Wheat);
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
                string modelName = modelNames[PositiveMod(seed + i, modelNames.Count)];
                var prefab = Resources.Load<GameObject>($"{ModelRoot}/{modelName}");
                if (prefab == null)
                    continue;

                if (!TryFindClearPosition(hexScale, seed + i * 137, tokenClear, MaxPropRadiusFactor, out var pos))
                    continue;

                float angle = PositiveMod(seed + i * 137, 360);
                float scaleFactor = Mathf.Lerp(minScaleFactor, maxScaleFactor, Hash01(seed + i * 17));
                float scale = scaleFactor * hexScale;

                var prop = Object.Instantiate(prefab, propsRoot.transform, false);
                prop.name = $"NatureProp_{modelName}_{i}";
                prop.transform.localPosition = pos + Vector3.up * (HexPrismMesh.TopY + LiftAboveTop);
                prop.transform.localRotation = Quaternion.Euler(0f, angle + 22f, 0f);
                prop.transform.localScale = Vector3.one * scale;

                foreach (var collider in prop.GetComponentsInChildren<Collider>())
                    ColonistSceneDestroy.SafeDestroy(collider);

                PruneAndStyleRenderers(prop, resource, hexScale);

                if (!PropFootprintClearsToken(hexRoot, prop, tokenClear))
                {
                    ColonistSceneDestroy.SafeDestroy(prop);
                    continue;
                }
            }
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

            if (!PropFootprintClearsToken(hexRoot, holder, TokenKeepOutCenterRadius(hexScale)))
            {
                float angle = Mathf.Atan2(localPos.z, localPos.x);
                float push = TokenKeepOutCenterRadius(hexScale) + hexScale * 0.12f;
                holder.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * push,
                    localPos.y,
                    Mathf.Sin(angle) * push);
                if (!PropFootprintClearsToken(hexRoot, holder, TokenKeepOutCenterRadius(hexScale)))
                    ColonistSceneDestroy.SafeDestroy(holder.gameObject);
            }
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
            float angle = PositiveMod(seed, 360) * Mathf.Deg2Rad;
            float r = tokenClear + hexScale * (0.06f + Hash01(seed) * maxRadiusFactor);
            return new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
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
