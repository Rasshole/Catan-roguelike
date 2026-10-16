using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    internal static class ColonistHexTileIcon
    {
        private const float IconWorldDiameter = 0.5f;

        public static void Apply(GameObject tileRoot, HexTileData data)
        {
            if (data.IsDesert)
                return;

            var iconGo = new GameObject("ResourceIcon");
            iconGo.transform.SetParent(tileRoot.transform, false);
            iconGo.transform.localPosition = new Vector3(0f, HexPrismMesh.TopY + 0.12f, 0f);
            iconGo.transform.localRotation = Quaternion.identity;

            var lossy = tileRoot.transform.lossyScale;
            float diameter = IconWorldDiameter / lossy.x;
            float thin = 0.12f / Mathf.Max(lossy.y, 0.001f);
            iconGo.transform.localScale = new Vector3(diameter, thin, diameter);

            var meshFilter = iconGo.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = ThinDiskMesh.Shared;
            var renderer = iconGo.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = BuiltInMaterials.CreateUnlit(BoardResourceColors.GetHudSwatch(data.Resource));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
