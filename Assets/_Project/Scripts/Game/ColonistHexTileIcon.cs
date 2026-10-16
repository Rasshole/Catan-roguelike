using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatanRoguelike.Game
{
    internal static class ColonistHexTileIcon
    {
        private const float IconDiameterFactor = 0.48f;

        public static void Apply(GameObject tileRoot, HexTileData data, float hexScale)
        {
            if (data.IsDesert)
                return;

            var iconGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            iconGo.name = "ResourceIcon";
            iconGo.transform.SetParent(tileRoot.transform, false);
            UnityEngine.Object.Destroy(iconGo.GetComponent<Collider>());

            var lossy = tileRoot.transform.lossyScale;
            float worldDiameter = hexScale * HexMath.OuterRadius * IconDiameterFactor;
            float localDiameter = worldDiameter / Mathf.Max(lossy.x, 0.001f);
            float localHeight = 0.06f / Mathf.Max(lossy.y, 0.001f);
            iconGo.transform.localPosition = new Vector3(0f, HexPrismMesh.TopY + 0.1f, 0f);
            iconGo.transform.localScale = new Vector3(localDiameter, localHeight, localDiameter);

            var color = BoardResourceColors.GetHudSwatch(data.Resource);
            var renderer = iconGo.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = BuiltInMaterials.CreateUnlit(Color.Lerp(color, Color.white, 0.25f));
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
