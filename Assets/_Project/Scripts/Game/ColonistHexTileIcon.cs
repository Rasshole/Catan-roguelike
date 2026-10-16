using CatanRoguelike.Core.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatanRoguelike.Game
{
    internal static class ColonistHexTileIcon
    {
        private const float IconWorldDiameter = 0.62f;

        public static void Apply(GameObject tileRoot, HexTileData data)
        {
            if (data.IsDesert)
                return;

            var iconGo = new GameObject("ResourceIcon");
            iconGo.transform.SetParent(tileRoot.transform, false);
            iconGo.transform.localPosition = new Vector3(0f, HexPrismMesh.TopY + 0.18f, 0f);
            iconGo.transform.localRotation = Quaternion.identity;

            var lossy = tileRoot.transform.lossyScale;
            float diameter = IconWorldDiameter / lossy.x;
            float thin = Mathf.Max(0.14f / lossy.y, 0.04f);
            iconGo.transform.localScale = new Vector3(diameter, thin, diameter);

            var meshFilter = iconGo.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = ThinDiskMesh.Shared;
            var renderer = iconGo.AddComponent<MeshRenderer>();
            var color = BoardResourceColors.GetHudSwatch(data.Resource);
            renderer.sharedMaterial = BuiltInMaterials.Create(color);
            renderer.sharedMaterial.color = Color.Lerp(color, Color.white, 0.18f);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial.renderQueue = 3100;
        }
    }
}
