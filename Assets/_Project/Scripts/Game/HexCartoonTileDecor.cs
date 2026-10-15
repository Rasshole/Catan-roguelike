using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

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
            var outlineRoot = new GameObject("TileOutline");
            outlineRoot.transform.SetParent(tileRoot.transform, false);
            CreateEdgeOutlines(outlineRoot.transform, hex, hexScale, tileRoot.transform.lossyScale);

            var cap = new GameObject("TopBevelCap");
            cap.transform.SetParent(tileRoot.transform, false);
            cap.transform.localPosition = new Vector3(0f, TopCapLiftY, 0f);
            float capDiameter = TopCapScale;
            var hexLossy = tileRoot.transform.lossyScale;
            cap.transform.localScale = new Vector3(
                capDiameter / hexLossy.x,
                0.08f / hexLossy.y,
                capDiameter / hexLossy.z);

            var meshFilter = cap.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = ThinDiskMesh.Shared;

            var renderer = cap.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = BuiltInMaterials.Create(BoardResourceColors.GetTileTopBevel(data));
            UnityEngine.Object.Destroy(cap.GetComponent<Collider>());
        }

        private static void CreateEdgeOutlines(Transform parent, HexCoord hex, float hexScale, Vector3 hexLossyScale)
        {
            var outlineColor = BoardResourceColors.GetOutline();
            var (centerX, centerZ) = HexMath.ToWorldPosition(hex, hexScale);

            for (int i = 0; i < HexPrismMesh.SideCount; i++)
            {
                int next = (i + 1) % HexPrismMesh.SideCount;
                var a = VertexGraph.Canonicalize(new Vertex(hex, i));
                var b = VertexGraph.Canonicalize(new Vertex(hex, next));
                var (ax, az) = HexMath.VertexToWorldPosition(a, hexScale);
                var (bx, bz) = HexMath.VertexToWorldPosition(b, hexScale);

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
                strip.GetComponent<Renderer>().sharedMaterial = BuiltInMaterials.Create(outlineColor);
                UnityEngine.Object.Destroy(strip.GetComponent<Collider>());
            }
        }
    }
}
