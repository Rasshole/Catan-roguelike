using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatanRoguelike.Game
{
    internal static class ColonistHexTileIcon
    {
        private const float IconSizeFactor = 0.30f;
        private const float CornerOffsetFactor = 0.55f;
        private const float IconLiftAboveHexTop = 0.05f;

        public static void Apply(GameObject tileRoot, HexTileData data, float hexScale)
        {
            if (data.IsDesert)
                return;

            float hexSize = hexScale * HexMath.OuterRadius;
            float worldDiameter = hexSize * IconSizeFactor * 2f;
            float corner = hexSize * CornerOffsetFactor;
            var iconGo = ColonistFlatBoardQuad.Create(
                tileRoot.transform,
                "ResourceIcon",
                new Vector3(
                    corner * 0.12f,
                    HexPrismMesh.TopY + IconLiftAboveHexTop,
                    corner),
                worldDiameter,
                worldDiameter,
                ColonistResourceIconTextures.Get(data.Resource),
                3180);
            ColonistFlatBoardQuad.AlignToColonistCamera(iconGo.transform);
            iconGo.layer = 0;
        }
    }

    public static class ColonistResourceIconDiagnostics
    {
        public static void Log(Transform iconTransform, Camera camera, Transform hexTileRoot)
        {
            if (iconTransform == null || camera == null)
                return;

            var renderer = iconTransform.GetComponent<Renderer>();
            float hexTopY = hexTileRoot != null
                ? hexTileRoot.position.y + hexTileRoot.lossyScale.y * HexPrismMesh.TopY
                : 0f;
            Vector3 normal = -iconTransform.forward;
            float facesCamera = Vector3.Dot(normal, -camera.transform.forward);
            int layer = iconTransform.gameObject.layer;
            bool inMask = (camera.cullingMask & (1 << layer)) != 0;
            var mat = renderer != null ? renderer.sharedMaterial : null;
            string shader = mat != null && mat.shader != null ? mat.shader.name : "null";
            BuiltInMaterials.TryGetMainTextureName(mat, out string texName);

            Debug.LogWarning(
                $"ColonistHexTileIcon diagnostics layer={layer} inCullingMask={inMask} " +
                $"mask=0x{camera.cullingMask:X8} worldY={iconTransform.position.y:F4} hexTopY={hexTopY:F4} " +
                $"dotUpVsCameraFwd={facesCamera:F3} shader={shader} queue={mat?.renderQueue} mainTex={texName}");
        }
    }
}
