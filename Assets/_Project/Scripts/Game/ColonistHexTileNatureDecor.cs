using CatanRoguelike.Core;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Quaternius CC0 low-poly props on terrain hex tops (trees, rocks, wheat clumps).</summary>
    public static class ColonistHexTileNatureDecor
    {
        private const string ModelRoot = "Art/Models/Nature";
        private const float PropScale = 0.42f;
        private const float LiftAboveTop = 0.03f;

        public static void Apply(GameObject hexRoot, HexTileData data, float hexScale)
        {
            if (hexRoot == null || data == null || data.IsDesert || BoardState.IsSeaFishGround(data))
                return;

            if (hexRoot.transform.Find("TileNatureProp") != null)
                return;

            string resourcePath = ModelResourcePath(data.Resource);
            if (string.IsNullOrEmpty(resourcePath))
                return;

            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
                return;

            var prop = Object.Instantiate(prefab, hexRoot.transform, false);
            prop.name = "TileNatureProp";

            Vector3 offset = PropOffset(data.Resource, hexScale);
            prop.transform.localPosition = new Vector3(offset.x, HexPrismMesh.TopY + LiftAboveTop, offset.z);
            prop.transform.localRotation = Quaternion.Euler(0f, PropYaw(data), 0f);
            prop.transform.localScale = Vector3.one * PropScale;

            foreach (var collider in prop.GetComponentsInChildren<Collider>())
                ColonistSceneDestroy.SafeDestroy(collider);

            foreach (var renderer in prop.GetComponentsInChildren<Renderer>())
            {
                if (renderer.sharedMaterial == null)
                    renderer.sharedMaterial = BuiltInMaterials.Create(BoardResourceColors.GetTileFace(data));
            }
        }

        private static string ModelResourcePath(ResourceType resource) => resource switch
        {
            ResourceType.Wood => $"{ModelRoot}/NormalTree_2",
            ResourceType.Stone => $"{ModelRoot}/Rock_3",
            ResourceType.Wheat => $"{ModelRoot}/Flower_2_Clump",
            _ => null
        };

        private static Vector3 PropOffset(ResourceType resource, float hexScale)
        {
            float s = hexScale * 0.22f;
            return resource switch
            {
                ResourceType.Wood => new Vector3(-s * 0.6f, 0f, s * 0.35f),
                ResourceType.Stone => new Vector3(s * 0.45f, 0f, -s * 0.2f),
                ResourceType.Wheat => new Vector3(-s * 0.15f, 0f, -s * 0.45f),
                _ => Vector3.zero
            };
        }

        private static float PropYaw(HexTileData data) =>
            (data.Coord.Q * 47 + data.Coord.R * 31) % 360;
    }
}
