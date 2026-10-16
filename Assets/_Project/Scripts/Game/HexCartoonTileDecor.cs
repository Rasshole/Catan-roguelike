using System.Collections.Generic;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Thin dark edge strips and a lighter top cap for cartoon hex tiles.</summary>
    public static class HexCartoonTileDecor
    {
        private const float OutlineThickness = 0.045f;
        private const float OutlineLiftY = 1.02f;
        private const float TopCapScale = 0.88f;
        private const float TopCapLiftY = 1.015f;

        public static void Apply(GameObject tileRoot, HexCoord hex, float hexScale, HexTileData data)
        {
            ApplyOutline(tileRoot, hex, hexScale);
            ApplyTopCap(tileRoot, data, hexScale, tileRoot.transform.lossyScale);
        }

        public static void ApplyOutline(GameObject tileRoot, HexCoord hex, float hexScale,
            IReadOnlyCollection<HexCoord> landTiles = null)
        {
            if (ColonistPresentation.IsActive)
                return;

            var outlineRoot = new GameObject("TileOutline");
            outlineRoot.transform.SetParent(tileRoot.transform, false);
            CreateEdgeOutlines(outlineRoot.transform, hex, hexScale, tileRoot.transform.lossyScale, landTiles);
        }

        public static void ApplyTopCap(GameObject tileRoot, HexTileData data, float hexScale, Vector3 hexLossy)
        {
            var cap = new GameObject("TopBevelCap");
            cap.transform.SetParent(tileRoot.transform, false);
            cap.transform.localPosition = new Vector3(0f, TopCapLiftY, 0f);
            float capDiameter = TopCapScale * HexMath.OuterRadius * hexScale * 1.7320508f;
            cap.transform.localScale = new Vector3(
                capDiameter / hexLossy.x,
                0.08f / hexLossy.y,
                capDiameter / hexLossy.z);

            var meshFilter = cap.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = ThinDiskMesh.Shared;

            var renderer = cap.AddComponent<MeshRenderer>();
            var bevel = BoardResourceColors.GetTileTopBevel(data);
            renderer.sharedMaterial = ColonistPresentation.IsActive
                ? BuiltInMaterials.CreateUnlit(bevel)
                : BuiltInMaterials.Create(bevel);
            DestroyCollider(cap);
        }

        private static void CreateEdgeOutlines(Transform parent, HexCoord hex, float hexScale, Vector3 hexLossyScale,
            IReadOnlyCollection<HexCoord> landTiles)
        {
            var outlineColor = BoardResourceColors.GetOutline();
            var (centerX, centerZ) = HexMath.ToWorldPosition(hex, hexScale);
            var tileSet = landTiles as HashSet<HexCoord> ?? (landTiles != null ? new HashSet<HexCoord>(landTiles) : null);

            for (int i = 0; i < HexPrismMesh.SideCount; i++)
            {
                if (tileSet != null && !tileSet.Contains(HexMath.GetNeighbor(hex, i)))
                    continue;

                int next = (i + 1) % HexPrismMesh.SideCount;
                var (ax, az) = HexMath.VertexToWorldPosition(hex, i, hexScale);
                var (bx, bz) = HexMath.VertexToWorldPosition(hex, next, hexScale);

                float localAx = ax - centerX;
                float localAz = az - centerZ;
                float localBx = bx - centerX;
                float localBz = bz - centerZ;

                float midX = (localAx + localBx) * 0.5f;
                float midZ = (localAz + localBz) * 0.5f;
                float dx = localBx - localAx;
                float dz = localBz - localAz;
                float length = Mathf.Sqrt(dx * dx + dz * dz);
                float angle = Mathf.Atan2(dz, dx) * Mathf.Rad2Deg;

                var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                strip.name = $"EdgeOutline_{i}";
                strip.transform.SetParent(parent, false);
                strip.transform.localPosition = new Vector3(midX, OutlineLiftY, midZ);
                strip.transform.localRotation = Quaternion.Euler(0f, -angle, 0f);
                strip.transform.localScale = new Vector3(
                    length / hexLossyScale.x,
                    OutlineThickness / hexLossyScale.y,
                    OutlineThickness / hexLossyScale.z);
                strip.GetComponent<Renderer>().sharedMaterial = ColonistPresentation.IsActive
                    ? BuiltInMaterials.CreateUnlit(outlineColor)
                    : BuiltInMaterials.Create(outlineColor);
                DestroyCollider(strip);
            }
        }

        private static void DestroyCollider(GameObject target)
        {
            var collider = target.GetComponent<Collider>();
            if (collider == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEngine.Object.DestroyImmediate(collider);
            else
#endif
                ColonistSceneDestroy.SafeDestroy(collider);
        }
    }
}
