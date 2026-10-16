using CatanRoguelike.Core;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Quaternius CC0 low-poly props on terrain hex tops (trees, rocks, wheat clumps).</summary>
    public static class ColonistHexTileNatureDecor
    {
        private const string ModelRoot = "Art/Models/Nature";
        private const float WoodPropScaleFactor = 0.30f;
        private const float OtherPropScaleFactor = 0.25f;
        private const float LiftAboveTop = 0.04f;
        private const float MinRadiusFromTokenFactor = 0.50f;

        public static void Apply(GameObject hexRoot, HexTileData data, float hexScale)
        {
            if (hexRoot == null || data == null || data.IsDesert || BoardState.IsSeaFishGround(data))
                return;

            if (hexRoot.transform.Find("TileNatureProps") != null)
                return;

            string resourcePath = ModelResourcePath(data.Resource);
            if (string.IsNullOrEmpty(resourcePath))
                return;

            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
                return;

            var propsRoot = new GameObject("TileNatureProps");
            propsRoot.transform.SetParent(hexRoot.transform, false);

            int count = data.Resource switch
            {
                ResourceType.Wood => 3,
                ResourceType.Stone => 2,
                ResourceType.Wheat => 2,
                _ => 0
            };

            if (count == 0)
                return;

            float minRadius = MinRadiusFromTokenFactor * hexScale;
            float scaleFactor = data.Resource == ResourceType.Wood ? WoodPropScaleFactor : OtherPropScaleFactor;
            float scale = scaleFactor * hexScale;
            int seed = data.Coord.Q * 73 + data.Coord.R * 41;

            for (int i = 0; i < count; i++)
            {
                float angle = (seed + i * 137) % 360;
                float rad = angle * Mathf.Deg2Rad;
                float r = minRadius + hexScale * (0.06f + i * 0.05f);
                var prop = Object.Instantiate(prefab, propsRoot.transform, false);
                prop.name = $"NatureProp_{i}";
                prop.transform.localPosition = new Vector3(
                    Mathf.Cos(rad) * r,
                    HexPrismMesh.TopY + LiftAboveTop,
                    Mathf.Sin(rad) * r);
                prop.transform.localRotation = Quaternion.Euler(0f, angle + 22f, 0f);
                prop.transform.localScale = Vector3.one * scale;

                foreach (var collider in prop.GetComponentsInChildren<Collider>())
                    ColonistSceneDestroy.SafeDestroy(collider);

                PruneAndStyleRenderers(prop, data.Resource, hexScale);
            }
        }

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
                else if (n.Contains("rock") && resource != ResourceType.Stone)
                {
                    renderer.enabled = false;
                    continue;
                }

                var bounds = renderer.bounds;
                float size = bounds.size.magnitude;
                if (size > maxExtent * 3f || bounds.extents.y > hexScale * 0.9f)
                {
                    renderer.enabled = false;
                    continue;
                }

                ColonistNaturePropMaterials.ApplyToRenderer(renderer, resource);
            }

            if (resource == ResourceType.Wood)
            {
                foreach (var t in prop.GetComponentsInChildren<Transform>(true))
                {
                    if (t == prop.transform)
                        continue;
                    string n = t.name.ToLowerInvariant();
                    if (IsRockLikeName(n))
                        t.gameObject.SetActive(false);
                }
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

        private static string ModelResourcePath(ResourceType resource) => resource switch
        {
            ResourceType.Wood => $"{ModelRoot}/NormalTree_2",
            ResourceType.Stone => $"{ModelRoot}/Rock_3",
            ResourceType.Wheat => $"{ModelRoot}/Flower_2_Clump",
            _ => null
        };
    }
}
