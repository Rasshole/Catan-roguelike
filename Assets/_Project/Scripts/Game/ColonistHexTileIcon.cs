using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatanRoguelike.Game
{
    internal static class ColonistHexTileIcon
    {
        private const float IconDiameterFactor = 0.50f;
        private const float IconLiftAboveHexTop = 0.035f;

        public static void Apply(GameObject tileRoot, HexTileData data, float hexScale)
        {
            if (data.IsDesert)
                return;

            var iconGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            iconGo.name = "ResourceIcon";
            iconGo.transform.SetParent(tileRoot.transform, false);
            UnityEngine.Object.Destroy(iconGo.GetComponent<Collider>());

            var lossy = tileRoot.transform.lossyScale;
            float worldDiameter = hexScale * HexMath.OuterRadius * IconDiameterFactor;
            float localDiameter = worldDiameter / Mathf.Max(lossy.x, 0.001f);
            iconGo.transform.localPosition = new Vector3(0f, HexPrismMesh.TopY + IconLiftAboveHexTop, 0f);
            iconGo.transform.localRotation = Quaternion.Euler(-90f, ColonistBoardViewConstants.CameraYawDegrees, 0f);
            iconGo.transform.localScale = new Vector3(localDiameter, localDiameter, 1f);

            var color = BoardResourceColors.GetHudSwatch(data.Resource);
            var renderer = iconGo.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = BuiltInMaterials.CreateUnlit(color);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial.renderQueue = 3055;
        }
    }
}
