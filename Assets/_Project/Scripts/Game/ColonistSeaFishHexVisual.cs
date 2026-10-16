using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Sea-frame fishing ground: flat hex in open water, not an extra land prism.</summary>
    public static class ColonistSeaFishHexVisual
    {
        private const float SeaSurfaceLocalY = 0.02f;
        private const float HexDiameterFactor = 0.92f;

        public static Renderer Apply(GameObject root, HexTileData data, float hexScale, float hexHeightScale)
        {
            float diameter = hexScale * HexMath.OuterRadius * 2f * HexDiameterFactor;
            float yScale = 0.04f / Mathf.Max(hexHeightScale, 0.001f);

            var disc = new GameObject("SeaFishGround");
            disc.transform.SetParent(root.transform, false);
            disc.transform.localPosition = new Vector3(0f, SeaSurfaceLocalY, 0f);
            disc.transform.localScale = new Vector3(diameter, yScale, diameter);

            var meshFilter = disc.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = ThinDiskMesh.Shared;

            var renderer = disc.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ColonistBoardTextureLoader.CreateSeaFishGroundMaterial();
            renderer.sharedMaterial.renderQueue = 1985;

            ColonistFishTileVisual.Apply(root, data, hexScale);
            return renderer;
        }
    }
}
