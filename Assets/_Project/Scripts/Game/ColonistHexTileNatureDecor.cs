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
            float tokenClear = TokenClearRadius(hexScale);

            switch (data.Resource)
            {
                case ResourceType.Wood:
                    ScatterModels(propsRoot, hexScale, seed, 6 + PositiveMod(seed, 4), TreeModels, 0.26f, 0.35f,
                        tokenClear, ResourceType.Wood);
                    break;
                case ResourceType.Wheat:
                    int wheatClumps = 5 + PositiveMod(seed, 4);
                    ScatterModels(propsRoot, hexScale, seed, wheatClumps, new[] { "Flower_2_Clump" }, 0.24f, 0.34f,
                        tokenClear, ResourceType.Wheat);
                    for (int r = 0; r < 4; r++)
                    {
                        var pos = SamplePosition(hexScale, seed + 400 + r * 59, tokenClear, MaxPropRadiusFactor);
                        ColonistHexTilePropPrimitives.CreateWheatRow(
                            propsRoot.transform, pos + Vector3.up * HexPrismMesh.TopY, hexScale, seed + r * 40f);
                    }
                    for (int s = 0; s < 2; s++)
                    {
                        var pos = SamplePosition(hexScale, seed + 450 + s * 61, tokenClear, MaxPropRadiusFactor);
                        ColonistHexTilePropPrimitives.CreateWheatSheaf(
                            propsRoot.transform, pos + Vector3.up * HexPrismMesh.TopY, hexScale);
                    }
                    break;
                case ResourceType.Sheep:
                    ScatterModels(propsRoot, hexScale, seed, 4, BushModels, 0.22f, 0.32f, tokenClear,
                        ResourceType.Sheep);
                    for (int s = 0; s < 2; s++)
                    {
                        var pos = SamplePosition(hexScale, seed + 500 + s * 71, tokenClear, MaxPropRadiusFactor);
                        ColonistHexTilePropPrimitives.CreateSheep(
                            propsRoot.transform, pos + Vector3.up * (HexPrismMesh.TopY + LiftAboveTop),
                            hexScale * 0.22f, seed + s);
                    }
                    ColonistHexTilePropPrimitives.CreateFenceSegment(
                        propsRoot.transform,
                        SamplePosition(hexScale, seed + 600, tokenClear, MaxPropRadiusFactor)
                            + Vector3.up * HexPrismMesh.TopY,
                        hexScale * 0.62f,
                        PositiveMod(seed, 120));
                    break;
                case ResourceType.Stone:
                    ScatterModels(propsRoot, hexScale, seed, 4, RockModels, 0.28f, 0.36f, tokenClear,
                        ResourceType.Stone);
                    var minePos = SamplePosition(hexScale, seed + 800, tokenClear, MaxPropRadiusFactor);
                    ColonistHexTilePropPrimitives.CreateMineEntrance(
                        propsRoot.transform,
                        minePos + Vector3.up * (HexPrismMesh.TopY + LiftAboveTop),
                        hexScale);
                    break;
                case ResourceType.Brick:
                    for (int b = 0; b < 3; b++)
                    {
                        var pos = SamplePosition(hexScale, seed + 700 + b * 53, tokenClear, MaxPropRadiusFactor);
                        ColonistHexTilePropPrimitives.CreateBrickStack(
                            propsRoot.transform, pos + Vector3.up * HexPrismMesh.TopY, hexScale);
                    }
                    ColonistHexTilePropPrimitives.CreateKiln(
                        propsRoot.transform,
                        SamplePosition(hexScale, seed + 750, tokenClear, MaxPropRadiusFactor)
                            + Vector3.up * (HexPrismMesh.TopY + LiftAboveTop),
                        hexScale);
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
            float tokenClear = TokenClearRadius(hexScale);
            ScatterModels(propsRoot, hexScale, seed, 3, DesertPlantModels, 0.22f, 0.32f, tokenClear, ResourceType.Wheat);
        }

        private static float TokenClearRadius(float hexScale) =>
            HexTileView.TokenWorldFaceDiameter * 0.5f + hexScale * 0.14f;

        private static int PositiveMod(int value, int modulus) =>
            ((value % modulus) + modulus) % modulus;

        private static void ScatterModels(
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

                var pos = SamplePosition(hexScale, seed + i * 137, tokenClear, MaxPropRadiusFactor);
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
            }
        }

        private static Vector3 SamplePosition(float hexScale, int seed, float tokenClear, float maxRadiusFactor)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float angle = PositiveMod(seed + attempt * 47, 360) * Mathf.Deg2Rad;
                float r = tokenClear + hexScale * (0.06f + Hash01(seed + attempt) * maxRadiusFactor);
                var pos = new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                if (pos.magnitude <= hexScale * 0.70f && pos.magnitude >= tokenClear)
                    return pos;
            }

            return new Vector3(tokenClear + hexScale * 0.15f, 0f, 0f);
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
