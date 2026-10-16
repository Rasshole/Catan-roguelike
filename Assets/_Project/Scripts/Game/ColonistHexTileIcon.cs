using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatanRoguelike.Game
{
    internal static class ColonistHexTileIcon
    {
        private const float IconDiameterFactor = 0.52f;
        private const float IconLiftAboveHexTop = 0.045f;

        public static void Apply(GameObject tileRoot, HexTileData data, float hexScale)
        {
            if (data.IsDesert)
                return;

            var iconGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            iconGo.name = "ResourceIcon";
            iconGo.layer = 0;
            iconGo.transform.SetParent(tileRoot.transform, false);
            UnityEngine.Object.Destroy(iconGo.GetComponent<Collider>());

            var lossy = tileRoot.transform.lossyScale;
            float worldDiameter = hexScale * HexMath.OuterRadius * IconDiameterFactor;
            float localDiameter = worldDiameter / Mathf.Max(lossy.x, 0.001f);
            iconGo.transform.localPosition = new Vector3(0f, HexPrismMesh.TopY + IconLiftAboveHexTop, 0f);
            iconGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            iconGo.transform.localScale = new Vector3(localDiameter, localDiameter, 1f);

            var color = BoardResourceColors.GetHudSwatch(data.Resource);
            var renderer = iconGo.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = BuiltInMaterials.CreateUnlit(color, zWrite: false);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial.renderQueue = 3085;
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

            Debug.LogWarning(
                $"ColonistHexTileIcon diagnostics layer={layer} inCullingMask={inMask} " +
                $"mask=0x{camera.cullingMask:X8} worldY={iconTransform.position.y:F4} hexTopY={hexTopY:F4} " +
                $"dotUpVsCameraFwd={facesCamera:F3} shader={shader} queue={mat?.renderQueue} " +
                $"mainTex={(mat != null && mat.mainTexture != null ? mat.mainTexture.name : "null")}");
        }
    }
}
