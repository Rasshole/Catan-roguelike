using CatanRoguelike.Core;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Quaternius CC0 low-poly props on terrain hex tops (trees, rocks, wheat clumps).</summary>
    public static class ColonistHexTileNatureDecor
    {
        private const string ModelRoot = "Art/Models/Nature";
        private const float PropScaleFactor = 0.3f;
        private const float LiftAboveTop = 0.04f;

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

            int count = data.Resource == ResourceType.Wood ? 3 : 2;
            float radius = hexScale * 0.38f;
            float scale = PropScaleFactor * hexScale;
            int seed = data.Coord.Q * 73 + data.Coord.R * 41;

            for (int i = 0; i < count; i++)
            {
                float angle = (seed + i * 137) % 360;
                float rad = angle * Mathf.Deg2Rad;
                float r = radius * (0.85f + (i % 3) * 0.08f);
                var prop = Object.Instantiate(prefab, propsRoot.transform, false);
                prop.name = $"NatureProp_{i}";
                prop.transform.localPosition = new Vector3(
                    Mathf.Cos(rad) * r,
                    HexPrismMesh.TopY + LiftAboveTop,
                    Mathf.Sin(rad) * r);
                prop.transform.localRotation = Quaternion.Euler(0f, angle + 18f, 0f);
                prop.transform.localScale = Vector3.one * scale;

                foreach (var collider in prop.GetComponentsInChildren<Collider>())
                    ColonistSceneDestroy.SafeDestroy(collider);

                foreach (var renderer in prop.GetComponentsInChildren<Renderer>())
                    ColonistNaturePropMaterials.ApplyToRenderer(renderer, data.Resource);
            }
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
