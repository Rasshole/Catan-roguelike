using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatanRoguelike.Game
{
    internal static class ColonistHexTileIcon
    {
        private const float IconDiameterFactor = 0.44f;

        public static void Apply(GameObject tileRoot, HexTileData data, float hexScale)
        {
            if (data.IsDesert)
                return;

            var iconGo = new GameObject("ResourceIcon");
            iconGo.transform.SetParent(tileRoot.transform, false);
            iconGo.transform.localPosition = new Vector3(0f, HexPrismMesh.TopY + 0.14f, 0f);
            iconGo.transform.localRotation = Quaternion.identity;

            var lossy = tileRoot.transform.lossyScale;
            float worldDiameter = hexScale * HexMath.OuterRadius * IconDiameterFactor;
            float localDiameter = worldDiameter / Mathf.Max(lossy.x, 0.001f);
            float localThin = 0.22f / Mathf.Max(lossy.y, 0.001f);
            iconGo.transform.localScale = new Vector3(localDiameter, localThin, localDiameter);

            var meshFilter = iconGo.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = ThinDiskMesh.Shared;
            var renderer = iconGo.AddComponent<MeshRenderer>();
            var color = BoardResourceColors.GetHudSwatch(data.Resource);
            renderer.sharedMaterial = BuiltInMaterials.CreateUnlit(Color.Lerp(color, Color.white, 0.25f));
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial.renderQueue = 3050;
        }
    }
}
